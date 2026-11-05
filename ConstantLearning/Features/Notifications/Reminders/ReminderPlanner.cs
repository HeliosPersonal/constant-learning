using ConstantLearning.Features.Identity.NotificationAudience;
using ConstantLearning.Features.Identity.ReminderAudience;
using ConstantLearning.Features.Learning.Notifications;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Notifications.Reminders;

public sealed class ReminderPlanner(AppDbContext db, INotificationAudienceReader audience,
    IReminderAudienceReader reminders, IReminderLearningReader learning, IClock clock)
{
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        Guid? after = null;
        while (true)
        {
            var page = await reminders.ReadRecipientsAsync(after, 512, cancellationToken);
            if (page.Count == 0) break;
            foreach (var learnerId in page)
            {
                // Порядок блокувань: learner KEY SHARE, advisory, dispatch; окрема транзакція для кожного learner.
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                if (await audience.ReadForPlanningAsync(learnerId, cancellationToken) is null) continue;
                await AcquireSchedulingLockAsync(db, cancellationToken);
                await PlanLanguagesAsync(learnerId, null, false, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            after = page[^1];
        }
    }

    internal static Task AcquireSchedulingLockAsync(AppDbContext db, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6147202631)", cancellationToken);

    /// <summary>
    /// Перераховує нагадування learner-а: <paramref name="languages"/> = <c>null</c> — усі мови з нагадуваннями,
    /// інакше лише вказані (мова без нагадування скасовує її відкладене сповіщення на сьогодні). ADR-0005.
    /// </summary>
    internal async Task PlanLanguagesAsync(Guid learnerId, IReadOnlyCollection<string>? languages, bool settingsChanged,
        CancellationToken cancellationToken)
    {
        var recipient = await audience.ReadLearnerAsync(learnerId, cancellationToken);
        if (recipient is null) return;
        var preferences = await reminders.ReadAsync(learnerId, cancellationToken);
        foreach (var language in languages ?? preferences.Select(item => item.TargetLanguage).ToArray())
        {
            var current = preferences.SingleOrDefault(item => item.TargetLanguage == language);
            await PlanLanguageAsync(recipient, language, current, settingsChanged, cancellationToken);
        }
    }

    private async Task PlanLanguageAsync(LearnerNotificationRecipient recipient, string language,
        ReminderPreferences? preferences, bool settingsChanged, CancellationToken cancellationToken)
    {
        var learnerId = recipient.LearnerId;
        var due = preferences is null ? null : ReminderSchedule.Due(recipient, preferences);
        if (due is null)
        {
            await db.NotificationDispatches.Where(row => row.LearnerId == learnerId && row.Kind == "reminder"
                    && row.TargetLanguage == language && row.LocalDate == recipient.LocalDate
                    && row.Status == "pending" && row.Attempts == 0 && row.ClaimedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "skipped")
                    .SetProperty(row => row.Reason, "reminder_not_scheduled_today"), cancellationToken);
            return;
        }
        var now = clock.GetCurrentInstant();
        if (settingsChanged)
        {
            var state = await learning.ReadAsync(learnerId, language, recipient.LocalDate, preferences!.DailyNewWordsLimit,
                preferences.NewWordsAvailableFrom, cancellationToken);
            // Зміна ліміту чи поясу не губить уже належну паузу після останньої відповіді цієї мови.
            if (state.LastAnsweredAt is { } answer && answer + Duration.FromMinutes(30) > due)
                due = Instant.Min(answer + Duration.FromMinutes(30),
                    Instant.Min(due.Value + Duration.FromHours(2), recipient.DayEndsAt));
        }
        // Звичайний тик не скидає відкладення після активності. Settings hook перераховує лише незахоплений рядок.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO notification_dispatches
                (id, recipient_telegram_user_id, learner_id, kind, local_date, due_at, status, attempts, created_at, target_language)
            VALUES ({Guid.CreateVersion7()}, {recipient.TelegramUserId}, {learnerId}, 'reminder', {recipient.LocalDate}, {due.Value}, 'pending', 0, {now}, {language})
            ON CONFLICT (recipient_telegram_user_id, kind, local_date, target_language) DO UPDATE SET due_at = EXCLUDED.due_at, reason = NULL, status = 'pending'
            WHERE {settingsChanged} AND (notification_dispatches.status = 'pending'
                OR (notification_dispatches.status = 'skipped' AND notification_dispatches.reason = 'reminder_not_scheduled_today'))
                AND notification_dispatches.attempts = 0 AND notification_dispatches.claimed_at IS NULL
            """, cancellationToken);
    }
}
