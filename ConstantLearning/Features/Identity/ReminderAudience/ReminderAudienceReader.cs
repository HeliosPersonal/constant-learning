using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NodaTime;

namespace ConstantLearning.Features.Identity.ReminderAudience;

internal sealed class ReminderAudienceReader(AppDbContext db, IClock clock) : IReminderAudienceReader
{
    public async Task<IReadOnlyList<Guid>> ReadRecipientsAsync(Guid? after, int take, CancellationToken cancellationToken = default) =>
        await db.ReminderSchedules.AsNoTracking().Where(schedule => schedule.Learner.OnboardedAt != null
                && (after == null || schedule.LearnerId.CompareTo(after.Value) > 0))
            .OrderBy(schedule => schedule.LearnerId).Take(take).Select(schedule => schedule.LearnerId).ToArrayAsync(cancellationToken);

    public async Task<ReminderPreferences?> ReadAsync(Guid learnerId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetCurrentInstant();
        return await db.ReminderSchedules.AsNoTracking().Where(schedule => schedule.LearnerId == learnerId
                && schedule.Learner.OnboardedAt != null && schedule.Learner.TimeZoneConfirmed)
            .Select(schedule => new ReminderPreferences(schedule.LocalTime, schedule.DaysOfWeek,
                schedule.Learner.PendingDailyNewWordsLimit != null && schedule.Learner.PendingLimitEffectiveFrom <= now
                    ? schedule.Learner.PendingDailyNewWordsLimit.Value : schedule.Learner.DailyNewWordsLimit,
                schedule.Learner.NewWordsAvailableFrom)).SingleOrDefaultAsync(cancellationToken);
    }
}

public static class ReminderAudienceModule
{
    public static IServiceCollection AddReminderAudience(this IServiceCollection services)
    {
        services.TryAddScoped<IReminderAudienceReader, ReminderAudienceReader>();
        return services;
    }
}
