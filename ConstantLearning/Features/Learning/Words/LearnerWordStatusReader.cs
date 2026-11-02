using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Learning.Words;

internal sealed class LearnerWordStatusReader(AppDbContext db) : ILearnerWordStatusReader
{
    public async Task<IReadOnlyDictionary<Guid, string>> ReadAsync(Guid learnerId,
        IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken) =>
        await db.LearnerWords.AsNoTracking()
            .Where(word => word.LearnerId == learnerId && entryIds.Contains(word.EntryId))
            .ToDictionaryAsync(word => word.EntryId, word => word.Status, cancellationToken);
}
