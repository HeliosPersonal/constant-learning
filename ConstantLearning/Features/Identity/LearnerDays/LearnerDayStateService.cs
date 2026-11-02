using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Domain.LearnerDay;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Identity.LearnerDays;

internal sealed class LearnerDayStateService(AppDbContext db, ILearnerDayService days, IClock clock) : ILearnerDayStateService
{
    public async Task<LearnerDayState?> GetForUpdateAsync(Guid learnerId, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        var learner = await db.Learners.FromSqlInterpolated($"""
            SELECT *, xmin FROM learners WHERE id = {learnerId} AND onboarded_at IS NOT NULL FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);
        if (learner is null)
        {
            return null;
        }

        // Оновлюємо агрегат, якщо викликувач уже читав його до отримання блокування.
        await db.Entry(learner).ReloadAsync(cancellationToken);
        await db.Entry(learner).Reference(value => value.ReminderSchedule).LoadAsync(cancellationToken);
        var day = days.GetCurrentDay(learner.TimeZone, ReadSnapshot(learner));
        if (learner.PendingDailyNewWordsLimit.HasValue != learner.PendingLimitEffectiveFrom.HasValue)
        {
            throw new InvalidOperationException("Pending limit і його межа повинні бути задані разом.");
        }

        if (learner.PendingDailyNewWordsLimit is { } limit && learner.PendingLimitEffectiveFrom <= clock.GetCurrentInstant())
        {
            learner.DailyNewWordsLimit = limit;
            learner.PendingDailyNewWordsLimit = null;
            learner.PendingLimitEffectiveFrom = null;
        }

        StoreSnapshot(learner, day);
        await db.SaveChangesAsync(cancellationToken);
        return new LearnerDayState(learner, day);
    }

    public LearnerDayState ChangeTimeZone(LearnerDayState current, string timeZone)
    {
        RequireTransaction();
        var resolved = days.ResolveTimeZone(timeZone);
        if (resolved.UsedFallback)
        {
            throw new ArgumentException("Потрібен відомий IANA-пояс.", nameof(timeZone));
        }

        var day = days.GetCurrentDay(resolved.Zone.Id, current.Day);
        current.Learner.TimeZone = resolved.Zone.Id;
        current.Learner.TimeZoneConfirmed = true;
        StoreSnapshot(current.Learner, day);
        if (current.Learner.PendingDailyNewWordsLimit is not null)
        {
            current.Learner.PendingLimitEffectiveFrom = day.EndsAt;
        }

        return new LearnerDayState(current.Learner, day);
    }

    private static LearnerDayPeriod? ReadSnapshot(Learner learner)
    {
        if (learner.CurrentDayDate is null && learner.CurrentDayStartsAt is null && learner.CurrentDayEndsAt is null
            && learner.CurrentDayOriginalEndsAt is null && learner.CurrentDayTimeZoneAtStart is null)
        {
            return null;
        }

        if (learner.CurrentDayDate is not { } date || learner.CurrentDayStartsAt is not { } start
            || learner.CurrentDayEndsAt is not { } end || learner.CurrentDayOriginalEndsAt is not { } originalEnd
            || string.IsNullOrWhiteSpace(learner.CurrentDayTimeZoneAtStart))
        {
            throw new InvalidOperationException("Знімок доби learner має містити всі п'ять полів або жодного.");
        }

        return new LearnerDayPeriod(date, start, end, learner.CurrentDayTimeZoneAtStart, originalEnd);
    }

    private static void StoreSnapshot(Learner learner, LearnerDayPeriod day)
    {
        learner.CurrentDayDate = day.Date;
        learner.CurrentDayStartsAt = day.StartsAt;
        learner.CurrentDayEndsAt = day.EndsAt;
        learner.CurrentDayOriginalEndsAt = day.OriginalEndsAt;
        learner.CurrentDayTimeZoneAtStart = day.TimeZoneAtStart;
    }

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Сталий стан доби потребує транзакції викликувача.");
        }
    }
}
