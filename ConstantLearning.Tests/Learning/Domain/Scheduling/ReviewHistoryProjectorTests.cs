using ConstantLearning.Features.Learning.Domain.Evaluation;
using ConstantLearning.Features.Learning.Domain.Scheduling;
using NodaTime;
using static ConstantLearning.Tests.Learning.Domain.Scheduling.MemorizeReviewSchedulerTests;

namespace ConstantLearning.Tests.Learning.Domain.Scheduling;

public class ReviewHistoryProjectorTests
{
    private static readonly LocalDate Start = new(2026, 1, 1);
    private readonly MemorizeReviewScheduler _scheduler = new();

    [Fact]
    public void Ac17_NeutralizationRestoresRecalledStatusModelAndPriorDueDate()
    {
        var initial = _scheduler.Introduce(Start);
        var first = new RecordedReviewAnswer(Answer(initial.DueOn), initial);
        var afterFirst = _scheduler.Apply(initial, first.Answer);
        var second = new RecordedReviewAnswer(Answer(afterFirst.DueOn, id: 2), afterFirst);
        var recalled = _scheduler.Apply(afterFirst, second.Answer);
        var mistake = new RecordedReviewAnswer(Answer(recalled.DueOn, AnswerOutcome.MistakeTypo, 3), recalled);
        var neutralized = new NeutralizedReviewAnswer(EventId(4), mistake.Answer.LearnerDay, mistake.Answer.Id);

        var result = new ReviewHistoryProjector(_scheduler).Project(initial, [first, second, mistake, neutralized]);

        Assert.Equal(recalled with { LastScheduledAttemptDay = mistake.Answer.LearnerDay }, result);
        Assert.Equal(ReviewWordStatus.Recalled, result.Status);
        Assert.Equal(0, result.Lapses);
        Assert.False(result.IsDue(mistake.Answer.LearnerDay));
        Assert.True(result.IsDue(mistake.Answer.LearnerDay.PlusDays(1)));
    }

    [Fact]
    public void NeutralizedControlRemainsPendingAndNextSuccessUsesOriginalAnchor()
    {
        var initial = _scheduler.Introduce(Start, alreadyKnown: true);
        var mistake = new RecordedReviewAnswer(Answer(initial.DueOn, AnswerOutcome.MistakeTypo), initial);
        var neutralized = new NeutralizedReviewAnswer(EventId(2), initial.DueOn, mistake.Answer.Id);
        var projector = new ReviewHistoryProjector(_scheduler);
        var restored = projector.Project(initial, [mistake, neutralized]);
        var success = new RecordedReviewAnswer(Answer(initial.DueOn.PlusDays(1), id: 3), restored);

        var result = projector.Project(initial, [mistake, neutralized, success]);

        Assert.True(restored.ControlPending);
        Assert.False(result.ControlPending);
        Assert.InRange(result.IntervalDays, 9, 24);
        Assert.Equal(1, result.ConsecutiveScheduledCorrect);
    }

    [Fact]
    public void NewWordCheckAndItsNeutralizationDoNotChangeProjection()
    {
        var initial = _scheduler.Introduce(Start);
        var answer = Answer(Start, AnswerOutcome.MistakeTypo) with { ExerciseKind = ReviewExerciseKind.NewWordCheck };
        var recorded = new RecordedReviewAnswer(answer, initial);
        var neutralized = new NeutralizedReviewAnswer(EventId(2), Start, answer.Id);

        var result = new ReviewHistoryProjector(_scheduler).Project(initial, [recorded, neutralized]);

        Assert.Equal(initial, result);
    }

    [Fact]
    public void Property_ReplayMatchesIncrementalProjectionForGeneratedHistories()
    {
        for (var seed = 1; seed <= 80; seed++)
        {
            var initial = _scheduler.Introduce(Start, alreadyKnown: seed % 2 == 0);
            var state = initial;
            var history = new List<ReviewHistoryEvent>();
            var random = new Random(seed);
            for (var index = 1; index <= 50; index++)
            {
                var outcome = index % 4 == 0 ? AnswerOutcome.DontKnow
                    : random.Next(3) == 0 ? AnswerOutcome.Synonym : AnswerOutcome.Correct;
                var day = state.DueOn.PlusDays(random.Next(3));
                if (state.LastScheduledAttemptDay >= day)
                {
                    day = state.LastScheduledAttemptDay.Value.PlusDays(1);
                }

                var answer = Answer(day, outcome, seed * 1000 + index);
                history.Add(new RecordedReviewAnswer(answer, state));
                state = _scheduler.Apply(state, answer);
            }

            Assert.Equal(state, new ReviewHistoryProjector(new MemorizeReviewScheduler()).Project(initial, history));
        }
    }

    [Fact]
    public void ReplayPreservesCappedIntervalsAfterLongAbsencesAndNeutralization()
    {
        var initial = _scheduler.Introduce(Start);
        var first = new RecordedReviewAnswer(Answer(Start.PlusDays(180)), initial);
        var afterFirst = _scheduler.Apply(initial, first.Answer);
        var second = new RecordedReviewAnswer(Answer(first.Answer.LearnerDay.PlusDays(365), id: 2), afterFirst);
        var afterSecond = _scheduler.Apply(afterFirst, second.Answer);
        var mistake = new RecordedReviewAnswer(Answer(afterSecond.DueOn, AnswerOutcome.MistakeTypo, 3), afterSecond);
        var neutralized = new NeutralizedReviewAnswer(EventId(4), mistake.Answer.LearnerDay, mistake.Answer.Id);
        var projector = new ReviewHistoryProjector(_scheduler);
        var restored = projector.Project(initial, [first, second, mistake, neutralized]);
        var third = new RecordedReviewAnswer(Answer(mistake.Answer.LearnerDay.PlusDays(1), id: 5), restored);

        var result = projector.Project(initial, [first, second, mistake, neutralized, third]);

        Assert.Equal(180, afterFirst.IntervalDays);
        Assert.Equal(180, afterSecond.IntervalDays);
        Assert.Equal(180, result.IntervalDays);
        Assert.Equal(third.Answer.LearnerDay.PlusDays(180), result.DueOn);
        Assert.Equal(ReviewWordStatus.Recalled, result.Status);
        Assert.Equal(0, result.Lapses);
        Assert.Equal(afterSecond.ForgettingRate * 0.7, result.ForgettingRate, 12);
    }

    [Fact]
    public void EmptyHistoryDoesNotInventAnswersForMissedDays()
    {
        var initial = _scheduler.Introduce(Start);

        Assert.Equal(initial, new ReviewHistoryProjector(_scheduler).Project(initial, []));
    }

    [Fact]
    public void RefusesInconsistentStateBeforeAndDuplicateEventIds()
    {
        var initial = _scheduler.Introduce(Start);
        var first = new RecordedReviewAnswer(Answer(initial.DueOn), initial);
        var afterFirst = _scheduler.Apply(initial, first.Answer);
        var projector = new ReviewHistoryProjector(_scheduler);
        var badSnapshot = new RecordedReviewAnswer(Answer(afterFirst.DueOn, id: 2), initial);
        var repeatedId = new RecordedReviewAnswer(Answer(afterFirst.DueOn), afterFirst);

        Assert.Throws<InvalidOperationException>(() => projector.Project(initial, [first, badSnapshot]));
        Assert.Throws<InvalidOperationException>(() => projector.Project(initial, [first, repeatedId]));
    }

    [Fact]
    public void RefusesDoubleNeutralizationOrUnknownTarget()
    {
        var initial = _scheduler.Introduce(Start);
        var mistake = new RecordedReviewAnswer(Answer(initial.DueOn, AnswerOutcome.MistakeTypo), initial);
        var neutralized = new NeutralizedReviewAnswer(EventId(2), initial.DueOn, mistake.Answer.Id);
        var duplicate = neutralized with { Id = EventId(3) };
        var projector = new ReviewHistoryProjector(_scheduler);

        Assert.Throws<InvalidOperationException>(() => projector.Project(initial, [mistake, neutralized, duplicate]));
        Assert.Throws<InvalidOperationException>(() =>
            projector.Project(initial, [mistake, neutralized with { AnswerEventId = EventId(999) }]));
    }

    [Theory]
    [InlineData(AnswerOutcome.Correct)]
    [InlineData(AnswerOutcome.MistakeDiacritic)]
    [InlineData(AnswerOutcome.DontKnow)]
    [InlineData(AnswerOutcome.Synonym)]
    public void RefusesNeutralizationForNonDisputableOutcome(AnswerOutcome outcome)
    {
        var initial = _scheduler.Introduce(Start);
        var answer = new RecordedReviewAnswer(Answer(initial.DueOn, outcome), initial);
        var neutralized = new NeutralizedReviewAnswer(EventId(2), initial.DueOn, answer.Answer.Id);

        Assert.Throws<InvalidOperationException>(() =>
            new ReviewHistoryProjector(_scheduler).Project(initial, [answer, neutralized]));
    }

    [Fact]
    public void RefusesLateNeutralizationOrOverwritingSubsequentAnswer()
    {
        var initial = _scheduler.Introduce(Start);
        var mistake = new RecordedReviewAnswer(Answer(initial.DueOn, AnswerOutcome.MistakeTypo), initial);
        var afterMistake = _scheduler.Apply(initial, mistake.Answer);
        var late = new NeutralizedReviewAnswer(EventId(2), initial.DueOn.PlusDays(1), mistake.Answer.Id);
        var next = new RecordedReviewAnswer(Answer(afterMistake.DueOn, id: 3), afterMistake);
        var projector = new ReviewHistoryProjector(_scheduler);

        Assert.Throws<InvalidOperationException>(() => projector.Project(initial, [mistake, late]));
        Assert.Throws<InvalidOperationException>(() => projector.Project(initial, [mistake, next, late]));
        Assert.Throws<InvalidOperationException>(() =>
            projector.Neutralize(_scheduler.Apply(afterMistake, next.Answer), mistake,
                late with { LearnerDay = initial.DueOn }));
    }
}
