using ConstantLearning.Features.Content.Course;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstantLearning.Features.Content.Learning;

public static class AnswerContentModule
{
    public static IServiceCollection AddAnswerContentReader(this IServiceCollection services)
    {
        services.TryAddScoped<IAnswerContentReader, AnswerContentReader>();
        return services;
    }
}

internal sealed class AnswerContentReader(AppDbContext db, IPublishedCourseReader courseReader) : IAnswerContentReader
{
    public async Task<AnswerContentSnapshot> ReadAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        var course = await courseReader.ReadAsync(cancellationToken);
        var target = course.SingleOrDefault(word => word.EntryId == entryId);
        // Прийняті форми належать саме прочитаній незмінній версії, навіть при новій публікації.
        var answers = target is null ? [] : await db.VersionAcceptedAnswers.AsNoTracking()
            .Where(answer => answer.VersionId == target.VersionId).OrderBy(answer => answer.Id)
            .Select(answer => answer.TextPl).ToArrayAsync(cancellationToken);
        return new AnswerContentSnapshot(course, target, answers);
    }
}
