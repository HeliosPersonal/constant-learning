using System.Text;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Identity.Learners;
using ConstantLearning.Features.Learning.Domain.LearnerDay;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Npgsql;

namespace ConstantLearning.Features.Identity.Onboarding;

public sealed class OnboardingService(AppDbContext db, IClock clock, ILearnerDayService learnerDay)
{
    public async Task<LearnerResponse?> FindAsync(long telegramUserId, CancellationToken cancellationToken) =>
        await db.Learners.AsNoTracking().Include(learner => learner.ReminderSchedules)
            .SingleOrDefaultAsync(learner => learner.TelegramUserId == telegramUserId, cancellationToken)
            is { } learner ? LearnerResponse.From(learner) : null;

    internal async Task<OnboardingResult> CreateAsync(long telegramUserId, string? firstName,
        OnboardingRequest request, CancellationToken cancellationToken)
    {
        var learner = await db.Learners.Include(value => value.ReminderSchedules)
            .SingleOrDefaultAsync(value => value.TelegramUserId == telegramUserId, cancellationToken);
        if (learner?.OnboardedAt is not null)
        {
            return new OnboardingResult(LearnerResponse.From(learner), false, null);
        }

        var timeZone = learnerDay.ResolveTimeZone(request.TimeZone);
        if (request.Reminder is not null && timeZone.UsedFallback)
        {
            return new OnboardingResult(null, false, new IdentityError("settings.time_zone_required",
                "Виберіть часовий пояс, щоб налаштувати нагадування."));
        }

        var now = clock.GetCurrentInstant();
        if (learner is null)
        {
            learner = new Learner
            {
                TelegramUserId = telegramUserId,
                TimeZone = timeZone.Zone.Id,
                CanMessage = true,
                CreatedAt = now
            };
            db.Learners.Add(learner);
        }

        learner.FirstName = firstName is null ? null : string.Concat(firstName.EnumerateRunes().Take(64));
        learner.TimeZone = timeZone.Zone.Id;
        learner.TimeZoneConfirmed = !timeZone.UsedFallback;
        learner.DailyNewWordsLimit = request.DailyNewWordsLimit;
        learner.OnboardedAt = now;
        // Початкове нагадування прив'язане до активної мови (інтерим «pl», як у LearnerDayStateService); ADR-0005.
        var language = learner.ActiveTargetLanguage ?? "pl";
        var schedule = learner.ReminderSchedules.SingleOrDefault(value => value.TargetLanguage == language);
        if (request.Reminder is { } reminder)
        {
            if (schedule is null)
            {
                schedule = new ReminderSchedule
                {
                    LearnerId = learner.Id,
                    TargetLanguage = language,
                    Learner = learner,
                    CreatedAt = now
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

        try
        {
            // Learner і reminder зберігаються однією транзакцією SaveChanges.
            await db.SaveChangesAsync(cancellationToken);
            return new OnboardingResult(LearnerResponse.From(learner), true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ReadConcurrentWinnerAsync(learner, cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation,
                   ConstraintName: "ux_learners_telegram_user_id" or "reminder_schedules_pkey" })
        {
            return await ReadConcurrentWinnerAsync(learner, cancellationToken);
        }
    }

    private async Task<OnboardingResult> ReadConcurrentWinnerAsync(Learner learner, CancellationToken cancellationToken)
    {
        // Конкурентний запит уже завершив онбординг: відкидаємо локальні зміни й повертаємо переможця.
        foreach (var entry in db.ChangeTracker.Entries().Where(entry => ReferenceEquals(entry.Entity, learner)
                     || entry.Entity is ReminderSchedule schedule && schedule.LearnerId == learner.Id).ToArray())
        {
            entry.State = EntityState.Detached;
        }

        var existing = await FindAsync(learner.TelegramUserId, cancellationToken);
        if (existing?.OnboardedAt is null)
        {
            throw new InvalidOperationException("Не знайдено завершений профіль після конфлікту онбордингу.");
        }

        return new OnboardingResult(existing, false, null);
    }
}

internal sealed record OnboardingResult(LearnerResponse? Learner, bool Created, IdentityError? Error);
