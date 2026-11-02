namespace ConstantLearning.Features.Learning.Domain.Evaluation;

public enum AnswerOutcome
{
    Correct,
    MistakeTypo,
    MistakeDiacritic,
    DontKnow,
    Synonym
}

/// <summary>Доменний результат без зміни стану слова, розкладу чи створення content report.</summary>
public sealed record AnswerEvaluation(
    AnswerOutcome? Outcome,
    string? ErrorCode,
    string CorrectForm,
    string NormalizedAnswer,
    IReadOnlyList<DiffSegment> Diff)
{
    public bool IsEvaluated => Outcome.HasValue;

    public bool IsMistake => Outcome is AnswerOutcome.MistakeTypo
        or AnswerOutcome.MistakeDiacritic or AnswerOutcome.DontKnow;

    public bool IsNeutral => Outcome is AnswerOutcome.Synonym;

    public bool DisputeAllowed => Outcome is AnswerOutcome.MistakeTypo;
}
