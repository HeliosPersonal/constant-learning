using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Learning.Words;

internal sealed class AnswerDisputeSourceReader(AppDbContext db) : IAnswerDisputeSourceReader
{
    public Task<AnswerDisputeSource?> ReadAsync(Guid learnerId, Guid answerId, CancellationToken cancellationToken = default) =>
        db.AnswerEvents.AsNoTracking().Where(answer => answer.Id == answerId && answer.LearnerId == learnerId)
            .Select(answer => new AnswerDisputeSource(answer.Id, answer.EntryId, answer.VersionId, answer.RawAnswer))
            .SingleOrDefaultAsync(cancellationToken);
}

public static class AnswerDisputeSourceReaderModule
{
    public static IServiceCollection AddAnswerDisputeSourceReader(this IServiceCollection services) =>
        services.AddScoped<IAnswerDisputeSourceReader, AnswerDisputeSourceReader>();
}
