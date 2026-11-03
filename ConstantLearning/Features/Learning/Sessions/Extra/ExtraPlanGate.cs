using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Sessions.Start.Limits;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Learning.Sessions.Extra;

/// <summary>
/// Спільні перевірки старту extra-session під блокуванням learner: план дня виконано й ліміт частоти дій.
/// Викликають обидва стартові сервіси (додаткове слово й практика) у власних транзакціях.
/// </summary>
internal static class ExtraPlanGate
{
    public const int RateLimitPerMinute = 30;

    /// <summary>План дня виконано (session тут немає, бо extra стартує лише без активної session).</summary>
    public static async Task<bool> PlanDoneAsync(AppDbContext db, Guid learnerId, LearnerDayState current,
        IReadOnlyList<LearnerWord> words, IReadOnlyDictionary<Guid, PublishedCourseWord> published,
        SessionLimits limits, CancellationToken ct)
    {
        var day = current.Day.Date;
        var attempted = (await db.AnswerEvents.AsNoTracking().Where(answer => answer.LearnerId == learnerId
                && answer.LearnerDay == day
                && (answer.ExerciseKind == "review" || answer.ExerciseKind == "control_repetition"))
            .Select(answer => answer.EntryId).Distinct().ToArrayAsync(ct)).ToHashSet();
        var due = words.Count(word => published.ContainsKey(word.EntryId) && word.Status != "not_started"
            && word.FirstShownDay != null && word.IntervalDays > 0 && word.DueOn <= day
            && !attempted.Contains(word.EntryId));
        var newWordsForPlan = limits.NewWordsAvailable
            && limits.Usage.Remaining(current.Learner.DailyNewWordsLimit) > 0 && !limits.CourseCompleted;
        return ExtraEligibility.PlanDone(false, due, newWordsForPlan);
    }

    /// <summary>Понад 30 стартів extra-session за хвилину — захист від автоматичних дотиків (PRD §6.1).</summary>
    public static async Task<bool> RateLimitedAsync(AppDbContext db, Guid learnerId, Instant now, CancellationToken ct)
    {
        var since = now - Duration.FromMinutes(1);
        var recent = await db.LearningSessions.AsNoTracking().CountAsync(session => session.LearnerId == learnerId
            && (session.Kind == "extra_word" || session.Kind == "extra_practice") && session.CreatedAt >= since, ct);
        return recent >= RateLimitPerMinute;
    }

    public static async Task<Dictionary<Guid, PublishedCourseWord>> PublishedAsync(
        IPublishedCourseReader courseReader, CancellationToken ct) =>
        (await courseReader.ReadAsync(ct)).ToDictionary(word => word.EntryId);
}
