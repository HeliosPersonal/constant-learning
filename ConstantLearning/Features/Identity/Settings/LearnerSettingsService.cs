using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Identity.Learners;
using ConstantLearning.Features.Learning.Domain.LearnerDay;
using ConstantLearning.Platform.Persistence;
using NodaTime;

namespace ConstantLearning.Features.Identity.Settings;

public sealed class LearnerSettingsService(AppDbContext db, ILearnerDayStateService learnerDays,
    ILearnerDayService days, IClock clock, IEnumerable<ILearnerSettingsChangedHandler> handlers)
{
    internal async Task<LearnerSettingsResult> UpdateAsync(Guid learnerId, LearnerSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.TimeZone is not null && days.ResolveTimeZone(request.TimeZone).UsedFallback)
        {
            return new LearnerSettingsResult(null, LearnerSettingsRequest.InvalidTimeZone());
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await learnerDays.GetForUpdateAsync(learnerId, cancellationToken);
        if (current is null)
        {
            return new LearnerSettingsResult(null, null);
        }

        if (request.Reminder is not null && request.TimeZone is null && !current.Learner.TimeZoneConfirmed)
        {
            return new LearnerSettingsResult(null, new IdentityError("settings.time_zone_required",
                "Виберіть часовий пояс, щоб налаштувати нагадування."));
        }

        if (request.TimeZone is not null)
        {
            current = learnerDays.ChangeTimeZone(current, request.TimeZone);
        }

        var learner = current.Learner;
        if (request.DailyNewWordsLimit is { } limit)
        {
            current.State.PendingDailyNewWordsLimit = limit == current.State.DailyNewWordsLimit ? null : limit;
            current.State.PendingLimitEffectiveFrom =
                current.State.PendingDailyNewWordsLimit is null ? null : current.Day.EndsAt;
        }

        if (request.HasReminder)
        {
            if (request.Reminder is { } reminder)
            {
                learner.ReminderSchedule ??= new ReminderSchedule
                {
                    LearnerId = learner.Id,
                    Learner = learner,
                    CreatedAt = clock.GetCurrentInstant()
                };
                learner.ReminderSchedule.LocalTime = reminder.LocalTime;
                learner.ReminderSchedule.DaysOfWeek = reminder.DaysOfWeek;
            }
            else if (learner.ReminderSchedule is { } previous)
            {
                db.ReminderSchedules.Remove(previous);
                learner.ReminderSchedule = null;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        var change = new LearnerSettingsChanged(learner.Id, clock.GetCurrentInstant(), current.Day);
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(change, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LearnerSettingsResult(LearnerResponse.From(learner), null);
    }
}

internal sealed record LearnerSettingsResult(LearnerResponse? Learner, IdentityError? Error);
