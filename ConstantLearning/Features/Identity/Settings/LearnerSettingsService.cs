using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Identity.Learners;
using ConstantLearning.Features.Learning.Domain.LearnerDay;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
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
        // Налаштування доби й нагадування стосуються активної мови (стан доби визначено нею).
        var language = current.State.TargetLanguage;
        if (request.DailyNewWordsLimit is { } limit)
        {
            current.State.PendingDailyNewWordsLimit = limit == current.State.DailyNewWordsLimit ? null : limit;
            current.State.PendingLimitEffectiveFrom =
                current.State.PendingDailyNewWordsLimit is null ? null : current.Day.EndsAt;
        }

        if (request.HasReminder)
        {
            var schedule = await db.ReminderSchedules.SingleOrDefaultAsync(
                value => value.LearnerId == learner.Id && value.TargetLanguage == language, cancellationToken);
            if (request.Reminder is { } reminder)
            {
                if (schedule is null)
                {
                    schedule = new ReminderSchedule
                    {
                        LearnerId = learner.Id,
                        TargetLanguage = language,
                        Learner = learner,
                        CreatedAt = clock.GetCurrentInstant()
                    };
                    db.ReminderSchedules.Add(schedule);
                }

                schedule.LocalTime = reminder.LocalTime;
                schedule.DaysOfWeek = reminder.DaysOfWeek;
            }
            else if (schedule is not null)
            {
                db.ReminderSchedules.Remove(schedule);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        var change = new LearnerSettingsChanged(learner.Id, clock.GetCurrentInstant(), current.Day, language,
            request.TimeZone is not null);
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(change, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await db.Entry(learner).Collection(value => value.ReminderSchedules).LoadAsync(cancellationToken);
        return new LearnerSettingsResult(LearnerResponse.From(learner, current.State), null);
    }
}

internal sealed record LearnerSettingsResult(LearnerResponse? Learner, IdentityError? Error);
