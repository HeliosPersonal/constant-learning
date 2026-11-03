using ConstantLearning.Features.Learning.Domain.Evaluation;
using NodaTime;

namespace ConstantLearning.Features.Learning.Domain.Scheduling;

public enum ReviewExerciseKind
{
    NewWordCheck,
    ScheduledReview,
    // unlimited-learning (ADR-0002): додаткова практика вивчених слів. extra_new_word_check відображається
    // на NewWordCheck; походження (звичайне/extra) розрізняє лише exercise_kind журналу.
    ExtraPractice
}

/// <summary>Id є сталим seed: повторне застосування події не використовує випадковість процесу.</summary>
public sealed record ReviewAnswer(
    Guid Id,
    LocalDate LearnerDay,
    ReviewExerciseKind ExerciseKind,
    AnswerOutcome Outcome,
    string SchedulerVersion);

public abstract record ReviewHistoryEvent;

/// <summary>Доменна частина answer_events; транспортні й контентні поля додає адаптер збереження.</summary>
public sealed record RecordedReviewAnswer(ReviewAnswer Answer, ReviewState StateBefore) : ReviewHistoryEvent;

/// <summary>Окрема append-only подія; початкова відповідь і її знімок лишаються незмінними.</summary>
public sealed record NeutralizedReviewAnswer(Guid Id, LocalDate LearnerDay, Guid AnswerEventId) : ReviewHistoryEvent;
