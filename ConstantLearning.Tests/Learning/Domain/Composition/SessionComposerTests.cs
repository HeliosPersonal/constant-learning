using ConstantLearning.Features.Learning.Domain.Composition;
using ConstantLearning.Features.Learning.Domain.Scheduling;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Domain.Composition;

public class SessionComposerTests
{
    private static readonly LocalDate Day = new(2026, 11, 1);

    [Fact]
    public void Ac03_ReviewsPrecedeNewWordsAndEachNewWordOccupiesOnePlace()
    {
        var reviews = Reviews(4);

        var result = Compose(reviews, limit: 3);

        Assert.Equal(7, result.Words.Count);
        Assert.All(result.Words.Take(4), word => Assert.Equal(SessionWordKind.Review, word.Kind));
        Assert.All(result.Words.Skip(4), word => Assert.Equal(SessionWordKind.NewWord, word.Kind));
        Assert.Equal(3, result.DailyNewWordsRemaining);
        Assert.False(result.IsCourseFinished);
        Assert.False(result.IsDailyLimitExhausted);
    }

    [Fact]
    public void Ac04_ThirtyFiveDueReviewsProduceTwentyMostOverdueWithoutChangingRemainder()
    {
        var reviews = Reviews(35);
        var snapshot = reviews.Select(candidate => candidate.State).ToArray();

        var result = Compose(reviews, limit: 5);

        Assert.Equal(reviews.AsEnumerable().Reverse().Take(20).Select(candidate => candidate.EntryId),
            result.Words.Select(word => word.EntryId));
        Assert.All(result.Words, word => Assert.Equal(SessionWordKind.Review, word.Kind));
        Assert.Equal(snapshot, reviews.Select(candidate => candidate.State));
        Assert.Equal(15, reviews.Count - result.Words.Count);
    }

    [Fact]
    public void Ac05_SixteenReviewsLeaveFourNewWordsAndSixForLaterSession()
    {
        var first = Compose(Reviews(16), limit: 10);
        var shown = first.Words.Where(word => word.Kind == SessionWordKind.NewWord)
            .Select(word => word.EntryId).ToHashSet();
        var nextCourse = Course(30).Select(word => word with { IsPresented = shown.Contains(word.EntryId) });

        var second = Compose(course: nextCourse, limit: 10, usage: new(4, 0));

        Assert.Equal(20, first.Words.Count);
        Assert.Equal(4, shown.Count);
        Assert.Equal(6, second.Words.Count);
        Assert.All(second.Words, word => Assert.Equal(SessionWordKind.NewWord, word.Kind));
        Assert.DoesNotContain(second.Words, word => shown.Contains(word.EntryId));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(8)]
    public void Ac06_ExhaustedDailyLimitAllowsOnlyReviews(int shown)
    {
        var result = Compose(Reviews(2), limit: 5, usage: new(shown, 0));

        Assert.Equal(2, result.Words.Count);
        Assert.All(result.Words, word => Assert.Equal(SessionWordKind.Review, word.Kind));
        Assert.True(result.IsDailyLimitExhausted);
        Assert.Equal(0, result.DailyNewWordsRemaining);
        Assert.False(result.IsCourseFinished);
    }

    [Fact]
    public void PortionUnavailableAfterTimeZoneChangeAllowsOnlyReviews()
    {
        var result = Compose(Reviews(1), newWordsAvailable: false);

        Assert.Single(result.Words);
        Assert.Equal(SessionWordKind.Review, result.Words[0].Kind);
        Assert.True(result.IsDailyLimitExhausted);
    }

    [Fact]
    public void ReviewPriorityUsesOverdueRatioThenCoursePosition()
    {
        var lowerRatio = Review(1, daysLate: 9, interval: 10);
        var earlierPosition = Review(2, daysLate: 2, interval: 2, position: 10);
        var laterPosition = Review(3, daysLate: 7, interval: 7, position: 20);
        var highestRatio = Review(4, daysLate: 3, interval: 1);
        var dueToday = Review(5, daysLate: 0);

        var result = Compose([lowerRatio, laterPosition, dueToday, earlierPosition, highestRatio], course: []);

        Assert.Equal(new[] { highestRatio, earlierPosition, laterPosition, lowerRatio, dueToday }
            .Select(candidate => candidate.EntryId), result.Words.Select(word => word.EntryId));
    }

    [Fact]
    public void FutureReviewsAndAnyScheduledAttemptTodayAreExcluded()
    {
        var future = Review(1, daysLate: -1);
        var attemptedToday = Review(2, daysLate: 5);
        attemptedToday = attemptedToday with
        {
            State = attemptedToday.State with { LastScheduledAttemptDay = Day }
        };
        var attemptedYesterday = Review(3, daysLate: 5);
        attemptedYesterday = attemptedYesterday with
        {
            State = attemptedYesterday.State with { LastScheduledAttemptDay = Day.PlusDays(-1) }
        };

        var result = Compose([future, attemptedToday, attemptedYesterday], course: []);

        Assert.Equal(attemptedYesterday.EntryId, Assert.Single(result.Words).EntryId);
    }

    [Fact]
    public void Ac09_EqualQuotaSharesChooseEverydayThenRecalculateForEachPlace()
    {
        var course = Course(6).Concat(Course(3, DomainQuota.Legalization, 100))
            .Select(word => word with { IsPresented = word.EntryId == Id(1) || word.EntryId == Id(101) });

        var result = SessionComposer.SelectNewWords(course.Reverse(), 5);

        Assert.Equal(new[] { 2, 3, 102, 4, 5 }.Select(Id), result.Select(word => word.EntryId));
    }

    [Fact]
    public void Ac09_EqualNonzeroSharesWithDifferentTotalsChooseEveryday()
    {
        var course = Course(4).Concat(Course(2, DomainQuota.Legalization, 100))
            .Select(word => word with
            {
                IsPresented = word.EntryId == Id(1) || word.EntryId == Id(2) || word.EntryId == Id(101)
            });

        var result = SessionComposer.SelectNewWords(course, 1);

        Assert.Equal(Id(3), Assert.Single(result).EntryId);
    }

    [Fact]
    public void Ac09_LowestRankWithinQuotaWinsWithCoursePositionAsTieBreaker()
    {
        CourseWordCandidate[] course =
        [
            Word(1) with { Rank = 8 },
            Word(2) with { Rank = 2, CoursePosition = 4 },
            Word(3) with { Rank = 2, CoursePosition = 3 },
            Word(4) with { Rank = null }
        ];

        var result = SessionComposer.SelectNewWords(course, 4);

        Assert.Equal(new[] { 3, 2, 1, 4 }.Select(Id), result.Select(word => word.EntryId));
    }

    [Fact]
    public void Ac09_UnapprovedWordsDoNotCountInEitherQuotaNumeratorOrDenominator()
    {
        CourseWordCandidate[] course =
        [
            Word(1) with { IsPresented = true }, Word(2),
            Word(3) with { IsApproved = false, IsPresented = true, CoursePosition = null },
            Word(101, DomainQuota.Legalization) with { IsPresented = true },
            Word(102, DomainQuota.Legalization),
            Word(103, DomainQuota.Legalization) with { IsApproved = false, CoursePosition = null }
        ];

        var result = SessionComposer.SelectNewWords(course, 1);

        Assert.Equal(Id(2), Assert.Single(result).EntryId);
    }

    [Fact]
    public void Ac09_ReturnedUnshownWordRemainsEligibleAndReplacementUsesUpdatedQuotaShare()
    {
        var course = Course(2).Concat(Course(2, DomainQuota.Legalization, 100)).ToArray();
        var initial = Assert.Single(SessionComposer.SelectNewWords(course, 1));
        var afterKnown = course.Select(word => word with { IsPresented = word.EntryId == initial.EntryId });

        var replacement = Assert.Single(SessionComposer.SelectNewWords(afterKnown, 1));
        var returned = Assert.Single(SessionComposer.SelectNewWords(course.Reverse(), 1));

        Assert.Equal(DomainQuota.Everyday, initial.Quota);
        Assert.Equal(DomainQuota.Legalization, replacement.Quota);
        Assert.Equal(initial.EntryId, returned.EntryId);
        Assert.All(course, word => Assert.False(word.IsPresented));
    }

    [Fact]
    public void ExhaustedOrAbsentQuotaDoesNotPreventFillingFromOtherQuota()
    {
        var exhausted = Course(3).Select(word => word with { IsPresented = true });
        var available = Course(5, DomainQuota.Legalization, 100).ToArray();

        var withExhausted = SessionComposer.SelectNewWords(exhausted.Concat(available), 20);
        var withAbsent = SessionComposer.SelectNewWords(available, 20);

        Assert.Equal(available, withExhausted);
        Assert.Equal(available, withAbsent);
    }

    [Fact]
    public void Ac10_FinishedCourseKeepsScheduledReviewsAndIgnoresUnapprovedNewWords()
    {
        var course = Course(2).Select(word => word with { IsPresented = true })
            .Append(Word(3) with { IsApproved = false, CoursePosition = null });

        var result = Compose(Reviews(2), course);

        Assert.True(result.IsCourseFinished);
        Assert.False(result.IsDailyLimitExhausted);
        Assert.Equal(2, result.Words.Count);
        Assert.All(result.Words, word => Assert.Equal(SessionWordKind.Review, word.Kind));
    }

    [Fact]
    public void Ac10_NewApprovedWordMakesFinishedCourseAvailableAgain()
    {
        var finished = Course(2).Select(word => word with { IsPresented = true }).ToArray();

        Assert.True(Compose(course: finished).IsCourseFinished);
        var replenished = Compose(course: finished.Append(Word(3)));

        Assert.False(replenished.IsCourseFinished);
        Assert.Equal(Id(3), Assert.Single(replenished.Words).EntryId);
    }

    [Fact]
    public void SelectingLastAvailableWordDoesNotFinishCourseBeforeItIsActuallyShown()
    {
        var result = Compose(course: [Word(1)], limit: 20);

        Assert.Single(result.Words);
        Assert.False(result.IsCourseFinished);
    }

    [Fact]
    public void EmptyFinishedCourseReturnsEmptyPlan()
    {
        var result = Compose(course: []);

        Assert.Empty(result.Words);
        Assert.True(result.IsCourseFinished);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 50)]
    [InlineData(19, 50)]
    [InlineData(20, 50)]
    [InlineData(21, 50)]
    [InlineData(35, 0)]
    public void SessionNeverExceedsTwentyWords(int reviewCount, int limit)
    {
        var result = Compose(Reviews(reviewCount), Course(50), limit);

        Assert.InRange(result.Words.Count, 0, SessionComposer.MaximumSessionWords);
        Assert.Equal(result.Words.Count, result.Words.Select(word => word.EntryId).Distinct().Count());
    }

    [Fact]
    public void InvalidSnapshotsFailBeforeProducingPartialPlan()
    {
        Assert.Throws<ArgumentException>(() => Compose(course: [Word(1), Word(1)]));
        Assert.Throws<ArgumentException>(() => Compose(course: [Word(1) with { CoursePosition = null }]));
        Assert.Throws<ArgumentException>(() => Compose(course: [Word(1) with { Quota = (DomainQuota)99 }]));
        Assert.Throws<ArgumentException>(() => Compose(course: [Word(1) with { Rank = 0 }]));
        Assert.Throws<ArgumentException>(() => Compose([Review(1), Review(1)]));
        var review = Review(1);
        Assert.Throws<ArgumentException>(() => Compose([review with { State = review.State with { IntervalDays = 0 } }]));
        Assert.Throws<ArgumentException>(() => Compose([review], [Word(1) with { EntryId = review.EntryId }]));
        Assert.Throws<ArgumentOutOfRangeException>(() => SessionComposer.SelectNewWords([], -1));
    }

    private static SessionComposition Compose(
        IEnumerable<ReviewCandidate>? reviews = null,
        IEnumerable<CourseWordCandidate>? course = null,
        int limit = 3,
        DailyNewWordUsage? usage = null,
        bool newWordsAvailable = true) =>
        SessionComposer.Compose(Day, reviews ?? [], course ?? Course(30), limit, usage ?? new(0, 0), newWordsAvailable);

    private static List<ReviewCandidate> Reviews(int count) => Enumerable.Range(1, count)
        .Select(id => Review(id, daysLate: id)).ToList();

    private static ReviewCandidate Review(int id, int daysLate = 0, int interval = 1, int? position = null)
    {
        var dueOn = Day.PlusDays(-daysLate);
        return new(Id(1000 + id), position ?? id,
            new ReviewState(dueOn.PlusDays(-interval), null, null, dueOn, interval,
                0, false, 0, 0.3, MemorizeReviewScheduler.CurrentVersion));
    }

    private static IEnumerable<CourseWordCandidate> Course(int count,
        DomainQuota quota = DomainQuota.Everyday, int offset = 0) =>
        Enumerable.Range(1, count).Select(id => Word(offset + id, quota));

    private static CourseWordCandidate Word(int id, DomainQuota quota = DomainQuota.Everyday) =>
        new(Id(id), quota, id, id, true, false);

    private static Guid Id(int id) => new(id, 0, 0, new byte[8]);
}
