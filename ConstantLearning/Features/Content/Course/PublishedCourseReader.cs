using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstantLearning.Features.Content.Course;

public static class PublishedCourseModule
{
    public static IServiceCollection AddPublishedCourseReader(this IServiceCollection services)
    {
        services.TryAddScoped<IPublishedCourseReader, PublishedCourseReader>();
        return services;
    }
}

internal sealed class PublishedCourseReader(AppDbContext db) : IPublishedCourseReader
{
    public async Task<IReadOnlyList<PublishedCourseWord>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var entries = await db.DictionaryEntries.AsNoTracking()
            .Where(entry => entry.CoursePosition != null && entry.PublishedVersion != null
                && entry.PublishedVersion.EntryId == entry.Id && entry.PublishedVersion.ValidationStatus == "approved")
            .Select(entry => new
            {
                entry.Id,
                entry.Domain,
                Position = entry.CoursePosition!.Value,
                Version = entry.PublishedVersion!,
                Translations = db.VersionTranslations.Where(translation => translation.VersionId == entry.PublishedVersionId)
                    .OrderBy(translation => translation.Position)
                    .Select(translation => new { translation.Kind, translation.TextUk }).ToArray()
            }).ToArrayAsync(cancellationToken);

        return entries.Select(entry => new PublishedCourseWord(entry.Id, entry.Version.Id, entry.Domain,
            entry.Position, entry.Domain == "everyday" ? entry.Version.FrequencyRank : entry.Version.UtilityRank,
            entry.Version.LemmaPl, entry.Translations.First(translation => translation.Kind == "canonical").TextUk,
            entry.Translations.Select(translation => translation.TextUk).ToArray(), entry.Version.PronunciationUk,
            entry.Version.ExamplePl, entry.Version.ExampleUk, entry.Version.Gender, entry.Version.ShowContextBefore)).ToArray();
    }
}
