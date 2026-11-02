using System.Buffers.Binary;
using System.Security.Cryptography;
using ConstantLearning.Features.Learning.Domain.Evaluation;
using NodaTime;

namespace ConstantLearning.Features.Learning.Domain.Scheduling;

/// <summary>
/// MEMORIZE з експоненційним забуванням і межами PRD.
/// Формули моделі: https://doi.org/10.1073/pnas.1815156116.
/// Параметри пілоту апріорні; їхня зміна потребує нової версії планувальника.
/// </summary>
public sealed class MemorizeReviewScheduler : IReviewScheduler
{
    public const string CurrentVersion = "memorize-v1";
    private const double InitialForgettingRate = 0.3;
    private const double SuccessMultiplier = 0.7;
    private const double MistakeMultiplier = 1.2;
    private const double SqrtReviewCost = 4;
    private const double MinimumForgettingRate = 1e-9;
    private const double MaximumForgettingRate = 1e6;

    public string Version => CurrentVersion;

    public ReviewState Introduce(LocalDate learnerDay, bool alreadyKnown = false)
    {
        var interval = alreadyKnown ? 7 : 1;
        return new ReviewState(learnerDay, null, null, learnerDay.PlusDays(interval), interval,
            0, alreadyKnown, 0, InitialForgettingRate, Version);
    }

    public ReviewState Apply(ReviewState state, ReviewAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(answer);
        Validate(state, answer);

        // Перевірка після показу не є запланованим повторенням і не зсуває його опорну дату.
        if (answer.ExerciseKind == ReviewExerciseKind.NewWordCheck)
        {
            if (state.ControlPending || state.LastReviewDay is not null || answer.LearnerDay != state.FirstShownDay)
            {
                throw new InvalidOperationException("Перевірка нового слова дозволена лише в добу першого показу до запланованих повторень.");
            }

            return state;
        }

        if (!state.IsDue(answer.LearnerDay))
        {
            throw new InvalidOperationException("Слово ще не потребує повторення або вже опрацьоване цієї доби.");
        }

        if (answer.Outcome == AnswerOutcome.Synonym)
        {
            return state with { LastScheduledAttemptDay = answer.LearnerDay };
        }

        var correct = answer.Outcome == AnswerOutcome.Correct;
        var forgettingRate = Math.Clamp(state.ForgettingRate * (correct ? SuccessMultiplier : MistakeMultiplier),
            MinimumForgettingRate, MaximumForgettingRate);
        var previousInterval = Period.Between(state.LastReviewDay ?? state.FirstShownDay,
            answer.LearnerDay, PeriodUnits.Days).Days;
        var interval = correct ? SampleInterval(answer.Id, forgettingRate, previousInterval) : 1;

        return state with
        {
            LastReviewDay = answer.LearnerDay,
            LastScheduledAttemptDay = answer.LearnerDay,
            DueOn = answer.LearnerDay.PlusDays(interval),
            IntervalDays = interval,
            ConsecutiveScheduledCorrect = correct ? Math.Min(2, state.ConsecutiveScheduledCorrect + 1) : 0,
            ControlPending = false,
            Lapses = correct ? state.Lapses : checked(state.Lapses + 1),
            ForgettingRate = forgettingRate
        };
    }

    private static int SampleInterval(Guid seed, double forgettingRate, int previousInterval)
    {
        var minimum = Math.Min(180, Math.Max(2, previousInterval + 1));
        var maximum = Math.Min(3 * previousInterval, 180);

        // SHA-256 і явний порядок байтів забезпечують відтворюваність незалежно від реалізації Random.
        var digest = SHA256.HashData(seed.ToByteArray());
        var bits = BinaryPrimitives.ReadUInt64LittleEndian(digest) >> 12;
        var uniform = (bits + 0.5) / 4503599627370496d;
        var target = -Math.Log(uniform);

        // Обернення H(t)=∫u(s)ds, u(t)=(1-exp(-n*t))/sqrt(q).
        // Пошук першої цілої доби еквівалентний ceil неперервної вибірки з подальшим обмеженням.
        var low = minimum;
        var high = maximum;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (IntegratedIntensity(middle, forgettingRate) >= target)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }

    private static double IntegratedIntensity(int days, double forgettingRate)
    {
        var x = days * forgettingRate;
        // Ряд запобігає втраті точності при майже нульовій швидкості забування.
        var integral = x < 0.001
            ? days * (x / 2 - x * x / 6 + x * x * x / 24 - x * x * x * x / 120)
            : days + (Math.Exp(-x) - 1) / forgettingRate;
        return integral / SqrtReviewCost;
    }

    private void Validate(ReviewState state, ReviewAnswer answer)
    {
        if (state.SchedulerVersion != Version || answer.SchedulerVersion != Version)
        {
            throw new InvalidOperationException("Версія події та стану має відповідати версії планувальника.");
        }

        if (answer.Id == Guid.Empty || !Enum.IsDefined(answer.ExerciseKind) || !Enum.IsDefined(answer.Outcome))
        {
            throw new ArgumentException("Подія має містити непорожній Id і відомі тип вправи та результат.", nameof(answer));
        }

        if (state.IntervalDays is < 1 or > 180 || state.ConsecutiveScheduledCorrect is < 0 or > 2
            || state.Lapses < 0 || !double.IsFinite(state.ForgettingRate) || state.ForgettingRate <= 0
            || state.LastReviewDay < state.FirstShownDay || answer.LearnerDay < state.FirstShownDay
            || state.DueOn <= (state.LastReviewDay ?? state.FirstShownDay))
        {
            throw new ArgumentException("Стан слова містить некоректні інтервали, дати або параметри моделі.", nameof(state));
        }
    }
}
