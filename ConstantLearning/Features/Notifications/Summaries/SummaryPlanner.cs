using ConstantLearning.Features.Content.Notifications;
using ConstantLearning.Features.Identity.NotificationAudience;
using ConstantLearning.Features.Notifications.Dispatch;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Notifications.Summaries;

public sealed class SummaryPlanner(AppDbContext db, IReportNotificationReader reports,
    INotificationAudienceReader audience, IClock clock)
{
    private const int PageSize = 512;

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetCurrentInstant();
        var processed = new HashSet<Guid>();
        Guid? after = null;
        while (true)
        {
            var page = await reports.ReadRecipientsAsync(after, PageSize, cancellationToken);
            if (page.Count == 0) break;
            foreach (var row in page)
            {
                await PlanLearnerAsync(row.LearnerId, row.EarliestClosedAt, now, cancellationToken);
                processed.Add(row.LearnerId);
            }
            after = page[^1].LearnerId;
        }

        // Незахоплені рядки враховують зміну поясу навіть тоді, коли всі результати вже зарезервовано.
        var pending = await db.NotificationDispatches.AsNoTracking().Where(row => row.Kind == "report_outcomes"
            && row.Status == "pending" && row.Attempts == 0 && row.ClaimedAt == null && row.LearnerId != null)
            .Select(row => row.LearnerId!.Value).Distinct().ToArrayAsync(cancellationToken);
        foreach (var learnerId in pending.Where(id => !processed.Contains(id)))
            await PlanLearnerAsync(learnerId, null, now, cancellationToken);

        var warsawDate = now.InZone(DateTimeZoneProviders.Tzdb["Europe/Warsaw"]).Date;
        var digestDue = SummarySchedule.At(warsawDate, "Europe/Warsaw", 9);
        foreach (var editor in audience.ReadEditorTelegramIds())
        {
            // Дайджест не має learner_id, тому INSERT не бере FK-блокування рядка Identity.
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await AcquireSchedulingLockAsync(db, cancellationToken);
            await UpsertAsync(editor, null, "editor_digest", warsawDate, digestDue, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        db.ChangeTracker.Clear();
    }

    private async Task PlanLearnerAsync(Guid learnerId, Instant? earliestClosedAt, Instant now, CancellationToken cancellationToken)
    {
        // Окрема транзакція на отримувача: ніколи не беремо інший learner під уже взятим advisory.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await audience.ReadForPlanningAsync(learnerId, cancellationToken) is null) return;
        // INSERT dispatch неявно бере такий самий KEY SHARE для FK. Порядок: learner, advisory, dispatch.
        await AcquireSchedulingLockAsync(db, cancellationToken);
        var recipient = await audience.ReadLearnerAsync(learnerId, cancellationToken);
        if (recipient is null) return;
        var current = SummarySchedule.CurrentLearnerDue(recipient);
        if (earliestClosedAt is { } earliest)
        {
            if (current is { } today && earliest <= today)
                await UpsertAsync(recipient.TelegramUserId, recipient.LearnerId, "report_outcomes", recipient.LocalDate, today, now, cancellationToken);
            if (current is null || earliest > current || now > current + NotificationDispatcher.MaximumLateness)
            {
                var next = SummarySchedule.NextLearnerDue(recipient);
                await UpsertAsync(recipient.TelegramUserId, recipient.LearnerId, "report_outcomes", next.Date, next.Due, now, cancellationToken);
            }
        }
        var dates = await db.NotificationDispatches.AsNoTracking().Where(row => row.LearnerId == learnerId
            && row.Kind == "report_outcomes" && row.Status == "pending" && row.Attempts == 0 && row.ClaimedAt == null)
            .Select(row => row.LocalDate).ToArrayAsync(cancellationToken);
        foreach (var date in dates)
            await UpsertAsync(recipient.TelegramUserId, learnerId, "report_outcomes", date,
                SummarySchedule.At(date, recipient.TimeZone, 12), now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    internal static Task AcquireSchedulingLockAsync(AppDbContext db, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(6147202631)", cancellationToken);

    private Task UpsertAsync(long telegramId, Guid? learnerId, string kind, LocalDate date, Instant due, Instant now,
        CancellationToken cancellationToken) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO notification_dispatches
            (id, recipient_telegram_user_id, learner_id, kind, local_date, due_at, status, attempts, created_at)
        VALUES ({Guid.CreateVersion7()}, {telegramId}, {learnerId}, {kind}, {date}, {due}, 'pending', 0, {now})
        ON CONFLICT (recipient_telegram_user_id, kind, local_date, target_language) DO UPDATE SET due_at = EXCLUDED.due_at
        WHERE notification_dispatches.status = 'pending' AND notification_dispatches.attempts = 0
            AND notification_dispatches.claimed_at IS NULL
        """, cancellationToken);
}
