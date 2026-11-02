using ConstantLearning.Features.Identity.NotificationAudience;
using ConstantLearning.Features.Identity.Settings;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Notifications.Summaries;

public sealed class SummarySettingsChangedHandler(AppDbContext db, INotificationAudienceReader audience) : ILearnerSettingsChangedHandler
{
    public async Task HandleAsync(LearnerSettingsChanged change, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Перерахунок підсумку потребує транзакції налаштувань.");
        // Планувальник не може перезаписати новий пояс знімком, прочитаним до цієї зміни.
        await SummaryPlanner.AcquireSchedulingLockAsync(db, cancellationToken);
        var recipient = await audience.ReadLearnerAsync(change.LearnerId, cancellationToken);
        if (recipient is null) return;
        var dates = await db.NotificationDispatches.AsNoTracking().Where(row => row.LearnerId == change.LearnerId
            && row.Kind == "report_outcomes" && row.Status == "pending" && row.Attempts == 0 && row.ClaimedAt == null)
            .Select(row => row.LocalDate).ToArrayAsync(cancellationToken);
        foreach (var date in dates)
        {
            var due = SummarySchedule.At(date, recipient.TimeZone, 12);
            // Умова повторно перевіряється після очікування блокування: sending та retry не змінюємо.
            await db.NotificationDispatches.Where(row => row.LearnerId == change.LearnerId && row.LocalDate == date
                && row.Kind == "report_outcomes" && row.Status == "pending" && row.Attempts == 0 && row.ClaimedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.DueAt, due), cancellationToken);
        }
    }
}
