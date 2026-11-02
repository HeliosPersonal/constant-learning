using NodaTime;

namespace ConstantLearning.Features.Learning.Domain.Composition;

/// <summary>Чиста композиція: повторення за простроченням, потім нові слова за поточними частками квот.</summary>
public static class SessionComposer
{
    public const int MaximumSessionWords = 20;

    /// <summary>
    /// Course має містити повний поточний знімок base course, включно з уже поданими лемами обох квот.
    /// Споживач передає лічильники тієї самої доби learner та доступність нової порції після зміни поясу.
    /// Побудова плану не зараховує покази й не змінює стан повторень.
    /// </summary>
    public static SessionComposition Compose(
        LocalDate learnerDay,
        IEnumerable<ReviewCandidate> reviewCandidates,
        IEnumerable<CourseWordCandidate> course,
        int dailyNewWordsLimit,
        DailyNewWordUsage usage,
        bool newWordsAvailable = true)
    {
        ArgumentNullException.ThrowIfNull(reviewCandidates);
        ArgumentNullException.ThrowIfNull(usage);
        var courseSnapshot = ReadCourse(course);
        var reviews = reviewCandidates.ToArray();
        ValidateReviews(reviews);

        var reviewIds = reviews.Select(candidate => candidate.EntryId).ToHashSet();
        if (courseSnapshot.Any(word => word.IsApproved && !word.IsPresented && reviewIds.Contains(word.EntryId)))
        {
            throw new ArgumentException("Слово з історією повторень не може бути непоказаним новим словом.", nameof(course));
        }

        var remaining = usage.Remaining(dailyNewWordsLimit);
        if (!newWordsAvailable)
        {
            remaining = 0;
        }

        var words = reviews.Where(candidate => candidate.State.IsDue(learnerDay))
            .OrderByDescending(candidate => candidate.State.OverdueRatio(learnerDay))
            .ThenBy(candidate => candidate.CoursePosition)
            .ThenBy(candidate => candidate.EntryId)
            .Take(MaximumSessionWords)
            .Select(candidate => new SessionWord(candidate.EntryId, SessionWordKind.Review))
            .ToList();

        var newWordCount = Math.Min(MaximumSessionWords - words.Count, remaining);
        words.AddRange(SelectNewWordsCore(courseSnapshot, newWordCount)
            .Select(word => new SessionWord(word.EntryId, SessionWordKind.NewWord)));

        return new SessionComposition(words.AsReadOnly(), remaining,
            !courseSnapshot.Any(word => word.IsApproved && !word.IsPresented));
    }

    /// <summary>
    /// Добір нових слів, зокрема заміни після «Вже знаю», за повним актуальним знімком курсу.
    /// Для кожного наступного місця вибрані тут леми тимчасово додаються до чисельника своєї квоти.
    /// </summary>
    public static IReadOnlyList<CourseWordCandidate> SelectNewWords(
        IEnumerable<CourseWordCandidate> course, int maximumCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCount);
        return SelectNewWordsCore(ReadCourse(course), maximumCount).AsReadOnly();
    }

    private static List<CourseWordCandidate> SelectNewWordsCore(CourseWordCandidate[] course, int maximumCount)
    {
        var approved = course.Where(word => word.IsApproved).ToArray();
        var everydayTotal = approved.Count(word => word.Quota == DomainQuota.Everyday);
        var legalizationTotal = approved.Length - everydayTotal;
        var everydayPresented = approved.Count(word => word.Quota == DomainQuota.Everyday && word.IsPresented);
        var legalizationPresented = approved.Count(word => word.Quota == DomainQuota.Legalization && word.IsPresented);
        var everyday = Candidates(DomainQuota.Everyday);
        var legalization = Candidates(DomainQuota.Legalization);
        var selected = new List<CourseWordCandidate>();

        while (selected.Count < maximumCount && (everyday.Count > 0 || legalization.Count > 0))
        {
            // Перемноження цілих часток уникає похибки й віддає рівність Everyday.
            var takeEveryday = everyday.Count > 0 && (legalization.Count == 0
                || (long)everydayPresented * legalizationTotal <= (long)legalizationPresented * everydayTotal);
            selected.Add((takeEveryday ? everyday : legalization).Dequeue());
            if (takeEveryday) everydayPresented++;
            else legalizationPresented++;
        }

        return selected;

        Queue<CourseWordCandidate> Candidates(DomainQuota quota) => new(approved
            .Where(word => word.Quota == quota && !word.IsPresented)
            .OrderBy(word => word.Rank ?? int.MaxValue)
            .ThenBy(word => word.CoursePosition)
            .ThenBy(word => word.EntryId));
    }

    private static CourseWordCandidate[] ReadCourse(IEnumerable<CourseWordCandidate> course)
    {
        ArgumentNullException.ThrowIfNull(course);
        var snapshot = course.ToArray();
        var ids = new HashSet<Guid>();
        foreach (var word in snapshot)
        {
            if (word is null || word.EntryId == Guid.Empty || !ids.Add(word.EntryId)
                || !Enum.IsDefined(word.Quota) || word.Rank is <= 0
                || word.IsApproved && word.CoursePosition is not > 0)
            {
                throw new ArgumentException("Курс має містити унікальні непорожні Id, відомі квоти та додатні ранги й позиції схвалених лем.", nameof(course));
            }
        }

        return snapshot;
    }

    private static void ValidateReviews(ReviewCandidate[] reviews)
    {
        var ids = new HashSet<Guid>();
        foreach (var candidate in reviews)
        {
            if (candidate is null || candidate.EntryId == Guid.Empty || !ids.Add(candidate.EntryId)
                || candidate.CoursePosition <= 0 || candidate.State is null || candidate.State.IntervalDays <= 0)
            {
                throw new ArgumentException("Повторення мають містити унікальні непорожні Id, стан і додатні інтервали та позиції.", nameof(reviews));
            }
        }
    }
}
