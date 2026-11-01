using ConstantLearning.Features.Learning.Domain.Evaluation;
using ConstantLearning.Features.Learning.Domain.Scheduling;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Domain.Scheduling;

public class MemorizeReviewSchedulerTests
{
    private static readonly LocalDate Start = new(2026, 1, 1);
    private readonly MemorizeReviewScheduler _scheduler = new();

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 7)]
    public void IntroductionSchedulesNextDayOrControlWithoutAwardingRecall(bool known, int days)
    {
        var state = _scheduler.Introduce(Start, known);

        Assert.Equal(Start.PlusDays(days), state.DueOn);
        Assert.Equal(days, state.IntervalDays);
        Assert.Equal(known, state.ControlPending);
        Assert.Equal(ReviewWordStatus.Review, state.Status);
        Assert.Equal(0, state.ConsecutiveScheduledCorrect);
        Assert.Equal(0, state.Lapses);
        Assert.Null(state.LastReviewDay);
    }

    [Theory]
    [InlineData(AnswerOutcome.Correct)]
    [InlineData(AnswerOutcome.DontKnow)]
    [InlineData(AnswerOutcome.MistakeTypo)]
    [InlineData(AnswerOutcome.MistakeDiacritic)]
    [InlineData(AnswerOutcome.Synonym)]
    public void NewWordCheckDoesNotChangeScheduledStateOrMemoryModel(AnswerOutcome outcome)
    {
        var initial = _scheduler.Introduce(Start);
        var answer = Answer(Start, outcome) with { ExerciseKind = ReviewExerciseKind.NewWordCheck };

        Assert.Equal(initial, _scheduler.Apply(initial, answer));
    }

    [Fact]
    public void Ac18_TwoCorrectScheduledAnswersOnDifferentDaysRecallWord()
    {
        var first = _scheduler.Apply(_scheduler.Introduce(Start), Answer(Start.PlusDays(1)));
        Assert.Equal(ReviewWordStatus.Review, first.Status);
        Assert.Equal(1, first.ConsecutiveScheduledCorrect);

        var second = _scheduler.Apply(first, Answer(first.DueOn, id: 2));

        Assert.Equal(ReviewWordStatus.Recalled, second.Status);
        Assert.Equal(2, second.ConsecutiveScheduledCorrect);
        Assert.Equal(0, second.Lapses);
        Assert.False(second.IsDue(first.DueOn));
    }

    [Theory]
    [InlineData(AnswerOutcome.MistakeTypo)]
    [InlineData(AnswerOutcome.MistakeDiacritic)]
    [InlineData(AnswerOutcome.DontKnow)]
    public void Ac19_MistakeRemovesRecallAndRequiresTwoNewSuccesses(AnswerOutcome outcome)
    {
        var initial = _scheduler.Introduce(Start);
        var first = _scheduler.Apply(initial, Answer(Start.PlusDays(1)));
        var recalled = _scheduler.Apply(first, Answer(first.DueOn, id: 2));
        var mistake = _scheduler.Apply(recalled, Answer(recalled.DueOn, outcome, 3));

        Assert.Equal(ReviewWordStatus.Review, mistake.Status);
        Assert.Equal(0, mistake.ConsecutiveScheduledCorrect);
        Assert.Equal(1, mistake.Lapses);
        Assert.Equal(1, mistake.IntervalDays);
        Assert.Equal(recalled.DueOn.PlusDays(1), mistake.DueOn);

        var next = _scheduler.Apply(mistake, Answer(mistake.DueOn, id: 4));
        Assert.Equal(ReviewWordStatus.Review, next.Status);
        var restored = _scheduler.Apply(next, Answer(next.DueOn, id: 5));
        Assert.Equal(ReviewWordStatus.Recalled, restored.Status);
    }

    [Fact]
    public void CorrectMistakeCorrectDoesNotRecallWord()
    {
        var first = _scheduler.Apply(_scheduler.Introduce(Start), Answer(Start.PlusDays(1)));
        var mistake = _scheduler.Apply(first, Answer(first.DueOn, AnswerOutcome.DontKnow, 2));
        var next = _scheduler.Apply(mistake, Answer(mistake.DueOn, id: 3));

        Assert.Equal(1, next.ConsecutiveScheduledCorrect);
        Assert.Equal(ReviewWordStatus.Review, next.Status);
    }

    [Theory]
    [InlineData(AnswerOutcome.MistakeTypo)]
    [InlineData(AnswerOutcome.MistakeDiacritic)]
    [InlineData(AnswerOutcome.DontKnow)]
    public void Ac22_FailedControlReturnsToAdaptiveSchedule(AnswerOutcome outcome)
    {
        var known = _scheduler.Introduce(Start, alreadyKnown: true);
        var result = _scheduler.Apply(known, Answer(known.DueOn, outcome));

        Assert.False(result.ControlPending);
        Assert.Equal(1, result.IntervalDays);
        Assert.Equal(Start.PlusDays(8), result.DueOn);
        Assert.Equal(1, result.Lapses);
        Assert.Equal(ReviewWordStatus.Review, result.Status);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(12)]
    public void Ac23_ControlCountsAsFirstSuccessAndExtendsActualInterval(int days)
    {
        var known = _scheduler.Introduce(Start, alreadyKnown: true);
        var result = _scheduler.Apply(known, Answer(Start.PlusDays(days)));

        Assert.False(result.ControlPending);
        Assert.Equal(1, result.ConsecutiveScheduledCorrect);
        Assert.Equal(ReviewWordStatus.Review, result.Status);
        Assert.InRange(result.IntervalDays, days + 1, 3 * days);
        Assert.Equal(Start.PlusDays(days + result.IntervalDays), result.DueOn);
    }

    [Fact]
    public void Ac08_FiveMissedDaysOnlyIncreasePriorityAndActualElapsedInterval()
    {
        var initial = _scheduler.Introduce(Start);
        var returnDay = initial.DueOn.PlusDays(5);

        Assert.True(initial.IsDue(returnDay));
        Assert.Equal(5, initial.OverdueRatio(returnDay));
        Assert.Equal(0, initial.Lapses);
        var result = _scheduler.Apply(initial, Answer(returnDay));

        Assert.Equal(0, result.Lapses);
        Assert.Equal(1, result.ConsecutiveScheduledCorrect);
        Assert.InRange(result.IntervalDays, 7, 18);
        Assert.Equal(initial.ForgettingRate * 0.7, result.ForgettingRate, 12);
    }

    [Fact]
    public void NeutralAnswerPreservesAnchorStreakControlAndModelButSuppressesSameDay()
    {
        var initial = _scheduler.Introduce(Start, alreadyKnown: true);
        var neutral = _scheduler.Apply(initial, Answer(initial.DueOn, AnswerOutcome.Synonym));

        Assert.Equal(initial with { LastScheduledAttemptDay = initial.DueOn }, neutral);
        Assert.False(neutral.IsDue(initial.DueOn));
        Assert.True(neutral.IsDue(initial.DueOn.PlusDays(1)));
        var success = _scheduler.Apply(neutral, Answer(initial.DueOn.PlusDays(1), id: 2));
        Assert.InRange(success.IntervalDays, 9, 24);
        Assert.Equal(1, success.ConsecutiveScheduledCorrect);
    }

    [Fact]
    public void NeutralBetweenTwoSuccessesDoesNotBreakRecallStreak()
    {
        var first = _scheduler.Apply(_scheduler.Introduce(Start), Answer(Start.PlusDays(1)));
        var neutral = _scheduler.Apply(first, Answer(first.DueOn, AnswerOutcome.Synonym, 2));
        var second = _scheduler.Apply(neutral, Answer(first.DueOn.PlusDays(1), id: 3));

        Assert.Equal(ReviewWordStatus.Recalled, second.Status);
        Assert.InRange(second.IntervalDays, first.IntervalDays + 2, 3 * (first.IntervalDays + 1));
    }

    [Fact]
    public void OverdueRatioUsesPlannedIntervalAndAllowsBaseCourseTieBreak()
    {
        var oneDay = _scheduler.Introduce(Start);
        var sevenDays = _scheduler.Introduce(Start, alreadyKnown: true);
        var today = Start.PlusDays(14);

        Assert.Equal(13, oneDay.OverdueRatio(today));
        Assert.Equal(1, sevenDays.OverdueRatio(today));
        Assert.Equal(0, sevenDays.OverdueRatio(Start));
        Assert.Equal(0, sevenDays.OverdueRatio(sevenDays.DueOn));
        Assert.False(sevenDays.IsDue(sevenDays.DueOn.PlusDays(-1)));
        Assert.True(sevenDays.IsDue(sevenDays.DueOn));
    }

    [Fact]
    public void Property_EveryFeasiblePreviousIntervalAndManySeedsRespectBothPrdBounds()
    {
        var initial = _scheduler.Introduce(Start);
        for (var previous = 1; previous < 180; previous++)
        {
            for (var seed = 1; seed <= 64; seed++)
            {
                var day = Start.PlusDays(previous);
                var result = _scheduler.Apply(initial, Answer(day, id: seed));

                Assert.InRange(result.IntervalDays, Math.Max(2, previous + 1), Math.Min(3 * previous, 180));
                Assert.Equal(day.PlusDays(result.IntervalDays), result.DueOn);
                Assert.True(double.IsFinite(result.ForgettingRate));
            }
        }
    }

    [Fact]
    public void Property_SamplingMatchesMemorizeIntegratedIntensityAcrossInteriorDays()
    {
        const int samples = 8192;
        var initial = _scheduler.Introduce(Start);
        var intervals = Enumerable.Range(1, samples)
            .Select(seed => _scheduler.Apply(initial, Answer(Start.PlusDays(4), id: seed)).IntervalDays)
            .ToArray();

        // Незалежна перевірка CDF для u(t)=(1-exp(-n*t))/sqrt(q), до верхнього обрізання PRD.
        const double rateAfterSuccess = 0.3 * 0.7;
        for (var day = 5; day < 12; day++)
        {
            var intensity = (day - (1 - Math.Exp(-rateAfterSuccess * day)) / rateAfterSuccess) / 4;
            var expected = 1 - Math.Exp(-intensity);
            var observed = intervals.Count(interval => interval <= day) / (double)samples;
            Assert.InRange(observed, expected - 0.025, expected + 0.025);
        }

        Assert.Equal(8, intervals.Distinct().Count());
    }

    [Fact]
    public void Property_SameEventIsStableAcrossInstancesAndUnrelatedAnswers()
    {
        var initial = _scheduler.Introduce(Start);
        for (var seed = 1; seed <= 200; seed++)
        {
            var answer = Answer(Start.PlusDays(4), id: seed);
            var expected = _scheduler.Apply(initial, answer);
            _scheduler.Apply(initial, Answer(Start.PlusDays(12), id: seed + 1000));

            Assert.Equal(expected, new MemorizeReviewScheduler().Apply(initial, answer));
            Assert.Equal(expected, _scheduler.Apply(initial, answer));
        }
    }

    [Fact]
    public void Property_LowerForgettingRateNeverShortensIntervalForTheSameSeed()
    {
        var initial = _scheduler.Introduce(Start);
        var strongMemory = initial with { ForgettingRate = initial.ForgettingRate * 0.1 };
        var strictIncreases = 0;
        for (var seed = 1; seed <= 1000; seed++)
        {
            var answer = Answer(Start.PlusDays(4), id: seed);
            var baseline = _scheduler.Apply(initial, answer);
            var stronger = _scheduler.Apply(strongMemory, answer);
            Assert.True(stronger.IntervalDays >= baseline.IntervalDays);
            strictIncreases += stronger.IntervalDays > baseline.IntervalDays ? 1 : 0;
        }

        Assert.True(strictIncreases > 100);
    }

    [Theory]
    [InlineData(180)]
    [InlineData(181)]
    [InlineData(365)]
    public void ContradictoryPrdBoundsFailExplicitlyWithoutMutatingState(int previous)
    {
        var initial = _scheduler.Introduce(Start);
        var exception = Assert.Throws<ReviewIntervalConflictException>(() =>
            _scheduler.Apply(initial, Answer(Start.PlusDays(previous))));

        Assert.Equal(previous, exception.PreviousIntervalDays);
        Assert.Null(initial.LastReviewDay);
    }

    [Fact]
    public void MistakeAfterLongAbsenceStillSchedulesTomorrow()
    {
        var initial = _scheduler.Introduce(Start);
        var result = _scheduler.Apply(initial, Answer(Start.PlusDays(365), AnswerOutcome.DontKnow));

        Assert.Equal(Start.PlusDays(366), result.DueOn);
        Assert.Equal(1, result.Lapses);
    }

    [Fact]
    public void RefusesEarlyOrRepeatedScheduledAnswer()
    {
        var initial = _scheduler.Introduce(Start);
        Assert.Throws<InvalidOperationException>(() => _scheduler.Apply(initial, Answer(Start)));
        var first = _scheduler.Apply(initial, Answer(initial.DueOn));
        Assert.Throws<InvalidOperationException>(() => _scheduler.Apply(first, Answer(initial.DueOn, id: 2)));
    }

    [Fact]
    public void RefusesUnknownSchedulerVersionOnStateOrEvent()
    {
        var initial = _scheduler.Introduce(Start);
        var answer = Answer(initial.DueOn);

        Assert.Throws<InvalidOperationException>(() =>
            _scheduler.Apply(initial, answer with { SchedulerVersion = "future" }));
        Assert.Throws<InvalidOperationException>(() =>
            _scheduler.Apply(initial with { SchedulerVersion = "future" }, answer));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefusesInvalidMemoryParameter(double rate)
    {
        var initial = _scheduler.Introduce(Start) with { ForgettingRate = rate };

        Assert.Throws<ArgumentException>(() => _scheduler.Apply(initial, Answer(initial.DueOn)));
    }

    [Fact]
    public void LearnerDatesAcrossLeapDayUseCalendarDays()
    {
        var leapEve = new LocalDate(2028, 2, 28);
        var initial = _scheduler.Introduce(leapEve);
        var result = _scheduler.Apply(initial, Answer(new LocalDate(2028, 3, 1)));

        Assert.Equal(new LocalDate(2028, 2, 29), initial.DueOn);
        Assert.InRange(result.IntervalDays, 3, 6);
    }

    internal static ReviewAnswer Answer(LocalDate day, AnswerOutcome outcome = AnswerOutcome.Correct, int id = 1) =>
        new(EventId(id), day, ReviewExerciseKind.ScheduledReview, outcome, MemorizeReviewScheduler.CurrentVersion);

    internal static Guid EventId(int number) => new(number, 0, 0, new byte[8]);
}
