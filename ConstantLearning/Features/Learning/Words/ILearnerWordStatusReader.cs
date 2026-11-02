namespace ConstantLearning.Features.Learning.Words;

public interface ILearnerWordStatusReader
{
    Task<IReadOnlyDictionary<Guid, string>> ReadAsync(Guid learnerId, IReadOnlyCollection<Guid> entryIds,
        CancellationToken cancellationToken);
}
