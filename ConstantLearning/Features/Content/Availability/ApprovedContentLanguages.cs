using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstantLearning.Features.Content.Availability;

public static class ContentAvailabilityModule
{
    public static IServiceCollection AddContentAvailability(this IServiceCollection services)
    {
        services.TryAddScoped<IApprovedContentLanguages, ApprovedContentLanguages>();
        return services;
    }
}

/// <summary>
/// Мови, що мають бодай одне схвалене опубліковане слово base course (T06, AC-02). Разом із
/// <see cref="LanguageAvailability"/> дає обчислюваний прапорець <c>available</c> для каталогу мов (T11).
/// </summary>
public interface IApprovedContentLanguages
{
    Task<IReadOnlySet<string>> ReadAsync(CancellationToken cancellationToken = default);
}

internal sealed class ApprovedContentLanguages(AppDbContext db) : IApprovedContentLanguages
{
    public async Task<IReadOnlySet<string>> ReadAsync(CancellationToken cancellationToken = default)
    {
        // DISTINCT target_language по записах зі схваленою опублікованою версією — той самий предикат
        // «approved published», що й у PublishedCourseReader, але без прив'язки до course_position.
        var languages = await db.DictionaryEntries.AsNoTracking()
            .Where(entry => entry.PublishedVersion != null
                && entry.PublishedVersion.EntryId == entry.Id && entry.PublishedVersion.ValidationStatus == "approved")
            .Select(entry => entry.TargetLanguage).Distinct().ToArrayAsync(cancellationToken);
        return languages.ToHashSet();
    }
}
