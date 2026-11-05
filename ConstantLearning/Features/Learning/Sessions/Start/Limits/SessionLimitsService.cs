using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Domain.Composition;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Learning.Sessions.Start.Limits;

public sealed record SessionLimits(DailyNewWordUsage Usage, bool NewWordsAvailable,
    IReadOnlyList<CourseWordCandidate> Course)
{
    public bool CourseCompleted => !Course.Any(word => !word.IsPresented);
}

/// <summary>Читає підтверджені покази й квоти після блокування learner у транзакції старту.</summary>
public sealed class SessionLimitsService(AppDbContext db)
{
    /// <summary>
    /// Оновлює маркер завершення й споживає одноразове повідомлення в транзакції викликувача.
    /// Викликувач утримує блокування learner і зберігає агрегат разом зі своєю операцією.
    /// </summary>
    public IReadOnlyList<string> ApplyNotices(LearnerDayState current, SessionLimits limits, Instant now)
    {
        RequireTransaction();
        var notices = new List<string>();
        if (!limits.NewWordsAvailable || limits.Usage.Remaining(current.State.DailyNewWordsLimit) == 0)
        {
            notices.Add("new_words_limit_reached");
        }

        if (limits.CourseCompleted)
        {
            current.State.CourseCompletedAt ??= now;
            notices.Add("course_completed");
        }
        else if (current.State.CourseCompletedAt is not null)
        {
            current.State.CourseCompletedAt = null;
            notices.Add("course_replenished");
        }

        return notices;
    }

    public async Task<SessionLimits> ReadAsync(LearnerDayState current, string targetLanguage,
        IReadOnlyList<LearnerWord> words, IReadOnlyList<PublishedCourseWord> course, Instant now,
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();

        // План session не є показом. Вилучення approved не скасовує вже витрачений денний ліміт.
        // Денний ліміт нових слів незалежний на мову (OQ-1а): рахуємо only already_known активної мови.
        var known = await db.AnswerEvents.AsNoTracking()
            .Where(answer => answer.LearnerId == current.Learner.Id && answer.TargetLanguage == targetLanguage
                && answer.Outcome == "already_known")
            .Select(answer => new { answer.EntryId, answer.LearnerDay }).Distinct()
            .ToArrayAsync(cancellationToken);
        var knownToday = known.Where(answer => answer.LearnerDay == current.Day.Date)
            .Select(answer => answer.EntryId).ToHashSet();
        var shownToday = words.Where(word => word.FirstShownDay == current.Day.Date)
            .Select(word => word.EntryId).Union(knownToday).Count();
        var presented = words.Where(word => word.FirstShownDay != null)
            .Select(word => word.EntryId).ToHashSet();
        presented.UnionWith(known.Select(answer => answer.EntryId));
        var candidates = course.Select(word => new CourseWordCandidate(word.EntryId,
            word.Domain == "everyday" ? DomainQuota.Everyday : DomainQuota.Legalization,
            word.Rank, word.CoursePosition, true, presented.Contains(word.EntryId))).ToArray();
        return new SessionLimits(new DailyNewWordUsage(shownToday, knownToday.Count),
            current.State.NewWordsAvailableFrom is null || current.State.NewWordsAvailableFrom <= now,
            candidates);
    }

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Ліміти session потребують транзакції заблокованого learner.");
        }
    }
}
