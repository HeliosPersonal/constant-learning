using ConstantLearning.Features.Learning.Domain.Evaluation;
using NodaTime;

namespace ConstantLearning.Features.Learning.Domain.Scheduling;

/// <summary>Відтворює журнал однієї пари learner–слово в порядку запису, перевіряючи знімки state_before.</summary>
public sealed class ReviewHistoryProjector(IReviewScheduler scheduler)
{
    public ReviewState Project(ReviewState initialState, IEnumerable<ReviewHistoryEvent> history)
    {
        ArgumentNullException.ThrowIfNull(initialState);
        ArgumentNullException.ThrowIfNull(history);
        var state = initialState;
        RecordedReviewAnswer? lastAnswer = null;
        LocalDate? lastEventDay = null;
        var ids = new HashSet<Guid>();

        foreach (var item in history)
        {
            switch (item)
            {
                case RecordedReviewAnswer recorded:
                    ValidateOrder(recorded.Answer.Id, recorded.Answer.LearnerDay);
                    if (recorded.StateBefore != state)
                    {
                        throw new InvalidOperationException("Знімок state_before не відповідає проєкції попередніх подій.");
                    }

                    state = scheduler.Apply(state, recorded.Answer);
                    lastAnswer = recorded;
                    break;
                case NeutralizedReviewAnswer neutralized:
                    ValidateOrder(neutralized.Id, neutralized.LearnerDay);
                    if (lastAnswer is null || neutralized.AnswerEventId != lastAnswer.Answer.Id)
                    {
                        throw new InvalidOperationException("Нейтралізувати можна лише останню відповідь слова без повторної нейтралізації.");
                    }

                    state = Neutralize(state, lastAnswer, neutralized);
                    lastAnswer = null;
                    break;
                default:
                    throw new ArgumentException("Невідомий тип події історії.", nameof(history));
            }
        }

        return state;

        void ValidateOrder(Guid id, LocalDate day)
        {
            if (id == Guid.Empty || !ids.Add(id) || day < initialState.FirstShownDay || day < lastEventDay)
            {
                throw new InvalidOperationException("Історія містить повторні ідентифікатори або порушений порядок діб.");
            }

            lastEventDay = day;
        }
    }

    /// <summary>Відновлює знімок, залишаючи заборону повторного показу до кінця поточної доби.</summary>
    public ReviewState Neutralize(ReviewState currentState, RecordedReviewAnswer recorded, NeutralizedReviewAnswer neutralized)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(neutralized);
        var answer = recorded.Answer;
        if (neutralized.Id == Guid.Empty || neutralized.Id == answer.Id
            || neutralized.AnswerEventId != answer.Id || neutralized.LearnerDay != answer.LearnerDay
            || answer.Outcome != AnswerOutcome.MistakeTypo)
        {
            throw new InvalidOperationException("Оскаржити можна помилку написання цієї картки протягом тієї самої доби.");
        }

        if (scheduler.Apply(recorded.StateBefore, answer) != currentState)
        {
            throw new InvalidOperationException("Після відповіді стан слова вже змінився.");
        }

        // Планове повторення й додаткова практика (ADR-0002) виключають слово до кінця доби;
        // перевірка нового слова відновлює знімок без позначки спроби.
        return answer.ExerciseKind == ReviewExerciseKind.NewWordCheck
            ? recorded.StateBefore
            : recorded.StateBefore with { LastScheduledAttemptDay = answer.LearnerDay };
    }
}
