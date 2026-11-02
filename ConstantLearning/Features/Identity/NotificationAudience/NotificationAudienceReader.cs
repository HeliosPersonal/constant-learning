using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Domain.LearnerDay;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConstantLearning.Features.Identity.NotificationAudience;

internal sealed class NotificationAudienceReader(AppDbContext db, ILearnerDayService days,
    IOptionsMonitor<IdentityOptions> options) : INotificationAudienceReader
{
    public async Task<IReadOnlyList<LearnerNotificationRecipient>> ReadLearnersAsync(IReadOnlyCollection<Guid> learnerIds,
        CancellationToken cancellationToken = default) =>
        (await db.Learners.AsNoTracking().Where(learner => learnerIds.Contains(learner.Id) && learner.OnboardedAt != null)
            .ToListAsync(cancellationToken)).Select(ToRecipient).ToArray();

    public async Task<LearnerNotificationRecipient?> ReadLearnerAsync(Guid learnerId, CancellationToken cancellationToken = default) =>
        (await ReadLearnersAsync([learnerId], cancellationToken)).SingleOrDefault();

    public async Task<LearnerNotificationRecipient?> ReadForPlanningAsync(Guid learnerId, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Блокування отримувача потребує транзакції planner.");
        var learner = await db.Learners.FromSqlInterpolated($"""
            SELECT *, xmin FROM learners WHERE id = {learnerId} AND onboarded_at IS NOT NULL FOR KEY SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return learner is null ? null : ToRecipient(learner);
    }

    public IReadOnlyList<long> ReadEditorTelegramIds() =>
        options.CurrentValue.ContentEditorTelegramUserIds.Where(id => id > 0).Distinct().Order().ToArray();

    private LearnerNotificationRecipient ToRecipient(Learner learner)
    {
        var snapshot = learner.CurrentDayDate is { } date && learner.CurrentDayStartsAt is { } start
            && learner.CurrentDayEndsAt is { } end && learner.CurrentDayOriginalEndsAt is { } originalEnd
            && learner.CurrentDayTimeZoneAtStart is { } zone
                ? new LearnerDayPeriod(date, start, end, zone, originalEnd) : null;
        var day = days.GetCurrentDay(learner.TimeZone, snapshot);
        return new(learner.Id, learner.TelegramUserId, days.ResolveTimeZone(learner.TimeZone).Zone.Id,
            day.Date, day.StartsAt, day.EndsAt);
    }
}
