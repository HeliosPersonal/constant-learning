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
            .Select(schedule => schedule.LearnerId).Distinct()
            .OrderBy(learnerId => learnerId).Take(take).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<ReminderPreferences>> ReadAsync(Guid learnerId, CancellationToken cancellationToken = default)
    {
        var learner = await db.Learners.AsNoTracking()
            .Where(value => value.Id == learnerId && value.OnboardedAt != null && value.TimeZoneConfirmed)
            .Select(value => new { value.DailyNewWordsLimit })
            .SingleOrDefaultAsync(cancellationToken);
        if (learner is null)
        {
            return [];
        }

        var schedules = await db.ReminderSchedules.AsNoTracking().Where(schedule => schedule.LearnerId == learnerId)
            .Select(schedule => new { schedule.TargetLanguage, schedule.LocalTime, schedule.DaysOfWeek })
            .ToArrayAsync(cancellationToken);
        if (schedules.Length == 0)
        {
            return [];
        }

        // Стан мови може ще не існувати (онбординг створює нагадування до першого відкриття мови): тоді ліміт — стартовий з онбордингу.
        var states = (await db.LearnerLanguageStates.AsNoTracking().Where(state => state.LearnerId == learnerId)
                .Select(state => new
                {
                    state.TargetLanguage, state.DailyNewWordsLimit, state.PendingDailyNewWordsLimit,
                    state.PendingLimitEffectiveFrom, state.NewWordsAvailableFrom
                }).ToArrayAsync(cancellationToken))
            .ToDictionary(state => state.TargetLanguage, StringComparer.Ordinal);
        var now = clock.GetCurrentInstant();
        return schedules.Select(schedule =>
        {
            states.TryGetValue(schedule.TargetLanguage, out var state);
            var limit = state is null ? learner.DailyNewWordsLimit
                : state.PendingDailyNewWordsLimit is { } pending && state.PendingLimitEffectiveFrom <= now
                    ? pending : state.DailyNewWordsLimit;
            return new ReminderPreferences(schedule.TargetLanguage, schedule.LocalTime, schedule.DaysOfWeek,
                limit, state?.NewWordsAvailableFrom);
        }).ToArray();
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
