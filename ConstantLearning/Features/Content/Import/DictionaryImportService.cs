using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Content.Import;

public sealed record DictionaryImportResult(int EntriesCreated, int VersionsCreated, int Unchanged,
    int Published, int Unpublished);

public sealed class DictionaryImportService(AppDbContext db, DictionaryImportReader reader,
    ContentPublicationService publication, IClock clock, ILogger<DictionaryImportService> logger)
{
    public Task<DictionaryImportResult> ImportPackagedAsync(CancellationToken cancellationToken = default)
        => ImportAsync(Path.Combine(AppContext.BaseDirectory, "Resources", "Dictionary"), cancellationToken);

    public async Task<DictionaryImportResult> ImportAsync(string directory, CancellationToken cancellationToken = default)
    {
        var rows = await reader.ReadAsync(directory, cancellationToken);
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Імпорт має власну атомарну транзакцію.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await publication.AcquireLockAsync(cancellationToken);
            var externalIds = rows.Select(row => row["id"]).ToArray();
            var entries = await db.DictionaryEntries.Where(entry => externalIds.Contains(entry.ExternalId))
                .ToDictionaryAsync(entry => entry.ExternalId, cancellationToken);
            var entryIds = entries.Values.Select(entry => entry.Id).ToArray();
            var history = await db.EntryVersions.AsNoTracking().Where(version => entryIds.Contains(version.EntryId))
                .OrderByDescending(version => version.CreatedAt).ThenByDescending(version => version.Id)
                .ToListAsync(cancellationToken);
            var latestImport = history.Where(version => version.CreatedByKind == "import")
                .GroupBy(version => version.EntryId).ToDictionary(group => group.Key, group => group.First());
            var latest = history.GroupBy(version => version.EntryId)
                .ToDictionary(group => group.Key, group => group.First());
            var changes = new List<PublicationChange>();
            var entriesCreated = 0;
            var versionsCreated = 0;
            foreach (var row in rows)
            {
                if (!entries.TryGetValue(row["id"], out var entry))
                {
                    entry = new DictionaryEntry { ExternalId = row["id"], Domain = row["domain"], CreatedAt = clock.GetCurrentInstant() };
                    db.DictionaryEntries.Add(entry);
                    entries.Add(entry.ExternalId, entry);
                    entriesCreated++;
                }
                // Порівнюємо з останнім імпортом, а не з редакторською або відкоченою публікацією.
                if (latestImport.TryGetValue(entry.Id, out var previous) && previous.ContentHash == row.ContentHash)
                    continue;
                entry.Domain = row["domain"];
                var createdAt = clock.GetCurrentInstant();
                if (latest.TryGetValue(entry.Id, out var last) && createdAt <= last.CreatedAt)
                    createdAt = last.CreatedAt + Duration.FromMilliseconds(1);
                var version = new EntryVersion
                {
                    Entry = entry, EntryId = entry.Id,
                    BasedOnVersionId = latest.GetValueOrDefault(entry.Id)?.Id,
                    LemmaPl = row["lemma_pl"], PartOfSpeech = row["part_of_speech"], Gender = row.Optional("gender"),
                    GenderPairId = row.Optional("gender_pair_id"), AspectPairId = row.Optional("aspect_pair_id"),
                    PronunciationUk = row["pronunciation_uk"], ExamplePl = row["example_pl"], ExampleUk = row["example_uk"],
                    ShowContextBefore = row["show_context_before"] == "true", FrequencyRank = row.Rank("frequency_rank"),
                    UtilityRank = row.Rank("utility_rank"), LegalizationStage = row.Optional("legalization_stage"),
                    Topic = row.Optional("topic"), ValidationStatus = row["validation_status"], CreatedByKind = "import",
                    ContentHash = row.ContentHash, ReviewNote = row.Optional("review_note"), CreatedAt = createdAt
                };
                db.EntryVersions.Add(version);
                var translations = row.Values("translation_uk");
                for (short index = 0; index < translations.Length; index++)
                    db.VersionTranslations.Add(new VersionTranslation
                    {
                        Version = version, VersionId = version.Id, TextUk = translations[index], Position = index,
                        Kind = index == 0 ? "canonical" : "variant", CreatedAt = clock.GetCurrentInstant()
                    });
                foreach (var answer in row.Values("accepted_answers_pl"))
                    db.VersionAcceptedAnswers.Add(new VersionAcceptedAnswer
                    {
                        Version = version, VersionId = version.Id, TextPl = answer,
                        IsCanonical = answer == row["lemma_pl"], CreatedAt = clock.GetCurrentInstant()
                    });
                AddEvidence(row, version.Id);
                changes.Add(new PublicationChange(entry.Id, entry.PublishedVersionId,
                    version.ValidationStatus == "approved" ? version.Id : null, "import",
                    Action: version.ValidationStatus == "approved" ? "publish" : "unpublish"));
                versionsCreated++;
            }
            await db.SaveChangesAsync(cancellationToken);
            await publication.ApplyAsync(changes, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var result = new DictionaryImportResult(entriesCreated, versionsCreated, rows.Count - versionsCreated,
                changes.Count(change => change.ToVersionId is not null),
                changes.Count(change => change.ExpectedPublishedVersionId is not null && change.ToVersionId is null));
            logger.LogInformation("Імпорт словника завершено: {EntriesCreated} записів, {VersionsCreated} версій, {Unchanged} без змін, {Published} публікацій, {Unpublished} знять публікації",
                result.EntriesCreated, result.VersionsCreated, result.Unchanged, result.Published, result.Unpublished);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private void AddEvidence(DictionaryImportRow row, Guid versionId)
    {
        if (row.Evidence is not { } proof) return;
        var sourceUrl = DictionaryImportReader.Text(proof, "source_url");
        var sense = DictionaryImportReader.Text(proof, "sense_index");
        void Add(string field, string attribution, string? url = null) => db.ValidationEvidence.Add(new ValidationEvidence
        {
            VersionId = versionId, Field = field, Source = "Wikisłownik", Url = url ?? sourceUrl,
            RevisionId = sense.Length is > 0 and <= 32 ? sense : null, Attribution = attribution,
            Outcome = row["validation_status"] == "approved" ? "confirmed" : "unconfirmed",
            RetrievedAt = clock.GetCurrentInstant(), CreatedAt = clock.GetCurrentInstant()
        });
        Add("translation_uk", "Wikisłownik contributors; CC BY-SA; " + row["validation_sources"]);
        Add("source_record_sha256", DictionaryImportReader.Text(proof, "source_record_sha256"));
        Add("usage_example_sha256", DictionaryImportReader.Text(proof, "usage_example_sha256"));
        Add("pronunciation_uk", DictionaryImportReader.Text(proof, "ipa"));
        Add("reviewed_by", row["reviewed_by"]);
        Add("reviewer_batch", DictionaryImportReader.Text(proof, "reviewer_batch"));
        if (proof.TryGetProperty("translation_correction", out var corrections))
            foreach (var correction in corrections.EnumerateArray())
                Add("translation_uk", DictionaryImportReader.Text(correction, "corrected"),
                    DictionaryImportReader.Text(correction, "source_url"));
    }
}
