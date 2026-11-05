using System.Collections.Concurrent;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConstantLearning.Features.Content.Course;

public static class PublishedCourseModule
{
    public static IServiceCollection AddPublishedCourseReader(this IServiceCollection services)
    {
        services.TryAddSingleton<PublishedVersionCache>();
        services.TryAddScoped<IPublishedCourseReader, PublishedCourseReader>();
        return services;
    }
}

internal sealed record PublishedVersionPayload(Guid Id, int? FrequencyRank, int? UtilityRank,
    string LemmaPl, string PromptUk, IReadOnlyList<string> TranslationsUk, string PronunciationUk,
    string ExamplePl, string ExampleUk, string? Gender, bool ShowContextBefore);

// Кеш містить лише незмінні дані версій; публікацію та статус завжди читаємо з бази.
internal sealed class PublishedVersionCache
{
    private const int Capacity = 4096;
    private readonly ConcurrentDictionary<Guid, PublishedVersionPayload> _values = new();
    private readonly ConcurrentQueue<Guid> _insertionOrder = new();
    private readonly SemaphoreSlim _missGate = new(1, 1);

    public async Task<IReadOnlyDictionary<Guid, PublishedVersionPayload>> ReadAsync(AppDbContext db,
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, PublishedVersionPayload>(ids.Count);
        var missing = new List<Guid>();
        foreach (var id in ids)
        {
            if (_values.TryGetValue(id, out var value)) result[id] = value;
            else missing.Add(id);
        }
        if (missing.Count == 0) return result;

        await _missGate.WaitAsync(cancellationToken);
        try
        {
            missing = missing.Where(id =>
            {
                if (!_values.TryGetValue(id, out var value)) return true;
                result[id] = value;
                return false;
            }).ToList();
            if (missing.Count == 0) return result;

            var versions = await db.EntryVersions.AsNoTracking().Where(version => missing.Contains(version.Id))
                .Select(version => new
                {
                    version.Id,
                    version.FrequencyRank,
                    version.UtilityRank,
                    version.LemmaPl,
                    version.PronunciationUk,
                    version.ExamplePl,
                    version.ExampleUk,
                    version.Gender,
                    version.ShowContextBefore
                })
                .ToArrayAsync(cancellationToken);
            var translations = await db.VersionTranslations.AsNoTracking().Where(row => missing.Contains(row.VersionId))
                .OrderBy(row => row.Position).Select(row => new { row.VersionId, row.Kind, row.TextUk })
                .ToArrayAsync(cancellationToken);
            var byVersion = translations.ToLookup(row => row.VersionId);
            foreach (var version in versions)
            {
                var rows = byVersion[version.Id].ToArray();
                var payload = new PublishedVersionPayload(version.Id, version.FrequencyRank, version.UtilityRank,
                    version.LemmaPl, rows.First(row => row.Kind == "canonical").TextUk,
                    Array.AsReadOnly(rows.Select(row => row.TextUk).ToArray()), version.PronunciationUk,
                    version.ExamplePl, version.ExampleUk, version.Gender, version.ShowContextBefore);
                result[version.Id] = payload;
                if (_values.TryAdd(version.Id, payload)) _insertionOrder.Enqueue(version.Id);
                while (_values.Count > Capacity && _insertionOrder.TryDequeue(out var oldest))
                    _values.TryRemove(oldest, out _);
            }
            return result;
        }
        finally { _missGate.Release(); }
    }
}

internal sealed class PublishedCourseReader(AppDbContext db, PublishedVersionCache cache) : IPublishedCourseReader
{
    public Task<IReadOnlyList<PublishedCourseWord>> ReadAsync(CancellationToken cancellationToken = default) =>
        ReadInternalAsync(targetLanguage: null, cancellationToken);

    public Task<IReadOnlyList<PublishedCourseWord>> ReadAsync(string targetLanguage, CancellationToken cancellationToken = default) =>
        ReadInternalAsync(targetLanguage, cancellationToken);

    private async Task<IReadOnlyList<PublishedCourseWord>> ReadInternalAsync(string? targetLanguage, CancellationToken cancellationToken)
    {
        var entries = await db.DictionaryEntries.AsNoTracking()
            .Where(entry => entry.CoursePosition != null && entry.PublishedVersion != null
                && entry.PublishedVersion.EntryId == entry.Id && entry.PublishedVersion.ValidationStatus == "approved")
            // null → усі мови (lookup/прогрів); задана мова → власний course_position цієї мови (T06, AC-06).
            .Where(entry => targetLanguage == null || entry.TargetLanguage == targetLanguage)
            .Select(entry => new { entry.Id, entry.Domain, Position = entry.CoursePosition!.Value, VersionId = entry.PublishedVersionId!.Value })
            .ToArrayAsync(cancellationToken);
        var payloads = await cache.ReadAsync(db, entries.Select(entry => entry.VersionId).Distinct().ToArray(), cancellationToken);
        return entries.Select(entry =>
        {
            var version = payloads[entry.VersionId];
            return new PublishedCourseWord(entry.Id, version.Id, entry.Domain, entry.Position,
                entry.Domain == "everyday" ? version.FrequencyRank : version.UtilityRank,
                version.LemmaPl, version.PromptUk, version.TranslationsUk, version.PronunciationUk,
                version.ExamplePl, version.ExampleUk, version.Gender, version.ShowContextBefore);
        }).ToArray();
    }
}
