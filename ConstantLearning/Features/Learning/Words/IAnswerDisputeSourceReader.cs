namespace ConstantLearning.Features.Learning.Words;

/// <summary>Мінімальний знімок власної відповіді для створення content report.</summary>
public sealed record AnswerDisputeSource(Guid AnswerId, Guid EntryId, Guid VersionId, string? RawAnswer);

public interface IAnswerDisputeSourceReader
{
    Task<AnswerDisputeSource?> ReadAsync(Guid learnerId, Guid answerId, CancellationToken cancellationToken = default);
}
