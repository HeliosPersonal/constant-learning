using ConstantLearning.Features.Identity.Settings;
using ConstantLearning.Platform.Persistence;

namespace ConstantLearning.Features.Notifications.Reminders;

public sealed class ReminderSettingsChangedHandler(AppDbContext db, ReminderPlanner planner) : ILearnerSettingsChangedHandler
{
    public async Task HandleAsync(LearnerSettingsChanged change, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Перерахунок нагадування потребує транзакції налаштувань.");
        await ReminderPlanner.AcquireSchedulingLockAsync(db, cancellationToken);
        // Пояс глобальний: зміна перераховує нагадування всіх мов; інакше — лише мову, для якої змінено налаштування (ADR-0005).
        var languages = change.TimeZoneChanged ? null : new[] { change.TargetLanguage };
        await planner.PlanLanguagesAsync(change.LearnerId, languages, true, cancellationToken);
    }
}
