using System.Text.Json;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Content.Search.Forms;

public sealed record MissingPolishMorphology(Guid EntryId, Guid VersionId, string ExternalId, string LemmaPl);

public sealed class SearchFormsMaintenance(AppDbContext db, ContentPublicationService publication,
    SearchFormsProjection projection, PolishMorphologyImporter importer, IConfiguration configuration,
    ILogger<SearchFormsMaintenance> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var path = configuration["Morphology:PolishLexiconPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "Resources", "Morphology", "polimorf-course.tab.gz");
        if (File.Exists(path))
        {
            var expectedHash = configuration["Morphology:PolishLexiconSha256"];
            if (configuration["Morphology:PolishLexiconPath"] is null)
            {
                using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
                    Path.Combine(AppContext.BaseDirectory, "Resources", "Morphology", "manifest.json"), cancellationToken));
                expectedHash = manifest.RootElement.GetProperty("subset_sha256").GetString();
            }
            await importer.ImportAsync(path, expectedHash, cancellationToken);
        }
        else if (configuration["Morphology:PolishLexiconPath"] is not null)
            throw new FileNotFoundException("Налаштований польський лексикон не знайдено.", path);
        else
            logger.LogWarning("Польського лексикону немає: пошук міститиме лише леми, відповіді та записані переклади.");
        var rebuilt = await RebuildAsync(onlyMissing: true, cancellationToken);
        var missing = await GetMissingPolishMorphologyAsync(cancellationToken);
        logger.LogInformation("Пошукові форми: відновлено {RebuiltEntries} записів; {MissingMorphology} опублікованих лем без польської морфології",
            rebuilt, missing.Count);
    }

    /// <summary>Відновлює похідні дані без нових версій, журналу публікації чи зміни порядку курсу.</summary>
    public async Task<int> RebuildAsync(bool onlyMissing = false, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Перебудова має власну транзакцію.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await publication.AcquireLockAsync(cancellationToken);
            var count = await projection.RebuildInTransactionAsync(onlyMissing, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return count;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>Редакторський індикатор: лема індексується, але її відмінювані форми відсутні в локальному лексиконі.</summary>
    public async Task<List<MissingPolishMorphology>> GetMissingPolishMorphologyAsync(CancellationToken cancellationToken = default)
    {
        var published = await db.DictionaryEntries.AsNoTracking()
            .Where(entry => entry.PublishedVersionId != null && entry.PublishedVersion!.ValidationStatus == "approved")
            .OrderBy(entry => entry.ExternalId)
            .Select(entry => new MissingPolishMorphology(entry.Id, entry.PublishedVersionId!.Value,
                entry.ExternalId, entry.PublishedVersion!.LemmaPl)).ToListAsync(cancellationToken);
        var seeds = published.Select(value => SearchTextNormalizer.Normalize(value.LemmaPl)).Distinct().ToArray();
        var covered = (await db.MorphForms.AsNoTracking().Where(form => form.Lang == "pl" && seeds.Contains(form.Lemma))
            .Select(form => form.Lemma).Distinct().ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        return published.Where(value => !covered.Contains(SearchTextNormalizer.Normalize(value.LemmaPl))).ToList();
    }
}
