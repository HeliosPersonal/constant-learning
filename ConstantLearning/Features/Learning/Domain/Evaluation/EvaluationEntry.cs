namespace ConstantLearning.Features.Learning.Domain.Evaluation;

/// <summary>Знімок запису опублікованого курсу та його схвалених польських варіантів.</summary>
public sealed record EvaluationEntry(
    Guid Id,
    string PolishLemma,
    string UkrainianTranslation,
    bool IsApproved,
    IReadOnlyList<string>? ApprovedPolishVariants = null);
