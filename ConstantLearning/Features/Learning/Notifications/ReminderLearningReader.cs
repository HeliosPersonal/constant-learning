using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Learning.Domain.Composition;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace ConstantLearning.Features.Learning.Notifications;

internal sealed class ReminderLearningReader(AppDbContext db, IPublishedCourseReader courseReader, IClock clock)
    : IReminderLearningReader
{
    public async Task<ReminderLearningState> ReadAsync(Guid learnerId, string targetLanguage, LocalDate day,
        int dailyNewWordsLimit, Instant? newWordsAvailableFrom, CancellationToken cancellationToken = default)
    {
        var now = clock.GetCurrentInstant();
        // Лише планова session рахується як виконаний план дня; extra-дії не впливають на нагадування (AC-18).
        var completed = await db.LearningSessions.AsNoTracking().AnyAsync(session => session.LearnerId == learnerId
            && session.TargetLanguage == targetLanguage
            && session.LearnerDay == day && session.Kind == "plan" && session.Status == "completed", cancellationToken);
        var answers = await db.AnswerEvents.AsNoTracking().Where(answer => answer.LearnerId == learnerId
                && answer.TargetLanguage == targetLanguage
                && (answer.LearnerDay == day || answer.Outcome == "already_known"))
            .Select(answer => new { answer.EntryId, answer.LearnerDay, answer.ExerciseKind, answer.Outcome, answer.AnsweredAt })
            .ToArrayAsync(cancellationToken);
        var today = answers.Where(answer => answer.LearnerDay == day).ToArray();
        // Підтверджений показ нової картки теж є опрацюванням, хоча він не створює AnswerEvent.
        var lastCard = await db.SessionItems.AsNoTracking().Where(item => item.Session.LearnerId == learnerId
                && item.Session.TargetLanguage == targetLanguage
                && item.Session.LearnerDay == day && item.State == "answered")
            .MaxAsync(item => item.AnsweredAt, cancellationToken);
        var lastAnswer = today.Select(answer => (Instant?)answer.AnsweredAt).Append(lastCard).Max();
        if (completed) return new(true, false, lastAnswer);

        // Нагадування рахуємо для мови свого розкладу (ADR-0005): base course іншої мови не впливає
        // на «є що робити сьогодні» цієї мови.
        var course = await courseReader.ReadAsync(targetLanguage, cancellationToken);
        var published = course.Select(word => word.EntryId).ToHashSet();
        // Незавершені вправи вже розпочатої session теж залишаються роботою, навіть коли квота нових слів вичерпана.
        var pending = await db.SessionItems.AsNoTracking().Where(item => item.Session.LearnerId == learnerId
                && item.Session.TargetLanguage == targetLanguage
                && item.Session.LearnerDay == day && item.Session.Kind == "plan"
                && item.Session.Status == "active" && item.Session.EndsAt > now
                && item.State != "answered" && item.State != "skipped")
            .Select(item => item.EntryId).ToArrayAsync(cancellationToken);
        var words = await db.LearnerWords.AsNoTracking().Where(word => word.LearnerId == learnerId
            && word.TargetLanguage == targetLanguage).ToArrayAsync(cancellationToken);
        var attempted = today.Where(answer => answer.ExerciseKind is "review" or "control_repetition")
            .Select(answer => answer.EntryId).ToHashSet();
        var due = words.Any(word => published.Contains(word.EntryId) && word.Status != "not_started"
            && word.FirstShownDay != null && word.IntervalDays > 0 && word.DueOn <= day && !attempted.Contains(word.EntryId));
        var known = answers.Where(answer => answer.Outcome == "already_known").ToArray();
        var knownToday = known.Where(answer => answer.LearnerDay == day).Select(answer => answer.EntryId).ToHashSet();
        var shownToday = words.Where(word => word.FirstShownDay == day).Select(word => word.EntryId).Union(knownToday).Count();
        var presented = words.Where(word => word.FirstShownDay != null).Select(word => word.EntryId)
            .Union(known.Select(answer => answer.EntryId)).ToHashSet();
        var available = (newWordsAvailableFrom is null || newWordsAvailableFrom <= now)
            && new DailyNewWordUsage(shownToday, knownToday.Count).Remaining(dailyNewWordsLimit) > 0
            && published.Any(id => !presented.Contains(id));
        return new(false, due || available || pending.Any(published.Contains), lastAnswer);
    }
}

public static class LearningNotificationsModule
{
    public static IServiceCollection AddLearningNotifications(this IServiceCollection services)
    {
        services.TryAddScoped<IReminderLearningReader, ReminderLearningReader>();
        return services;
    }
}
