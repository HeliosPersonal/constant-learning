using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using NodaTime.Text;

namespace ConstantLearning.Features.Content.References;

public sealed record ReferenceImportIssue(string Lemma, string Reason);
public sealed record ReferenceImportResult(int Imported, int Unchanged, int ManualRecords, IReadOnlyList<ReferenceImportIssue> Issues);

public sealed class ReferenceSnapshotImporter(AppDbContext db, WikislownikParser parser, PolishIpaConverter converter, IClock clock)
{
    public async Task<ReferenceImportResult> ImportConfiguredDumpAsync(IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var path = configuration["References:WikislownikDumpPath"];
        var timestamp = configuration["References:SnapshotAt"];
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(timestamp)) return new(0, 0, 0, []);
        var parsed = InstantPattern.ExtendedIso.Parse(timestamp ?? "");
        if (string.IsNullOrWhiteSpace(path) || !parsed.Success || parsed.Value > clock.GetCurrentInstant())
            return new(0, 0, 1, [new("", "configured_snapshot_metadata_invalid")]);
        if (!File.Exists(path)) return new(0, 0, 1, [new("", "configured_dump_missing")]);
        try
        {
            await using var stream = File.OpenRead(path);
            return await ImportWikislownikDumpAsync(stream, parsed.Value, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException)
        {
            return new(0, 0, 1, [new("", "configured_dump_unreadable")]);
        }
    }

    public async Task<ReferenceImportResult> ImportPackagedAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Resources", "Dictionary");
        return await ImportEvidenceFileAsync(Path.Combine(directory, "evidence.jsonl"),
            Path.Combine(directory, "sources-manifest.json"), cancellationToken);
    }

    public async Task<ReferenceImportResult> ImportEvidenceFileAsync(string evidencePath, string manifestPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(evidencePath) || !File.Exists(manifestPath))
            return new(0, 0, 1, [new("", "packaged_snapshot_missing")]);
        await using var manifestStream = File.OpenRead(manifestPath);
        using var manifest = await JsonDocument.ParseAsync(manifestStream, cancellationToken: cancellationToken);
        var retrieved = manifest.RootElement.GetProperty("retrieved_on").GetString();
        var date = DateOnly.ParseExact(retrieved!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var snapshotAt = Instant.FromUtc(date.Year, date.Month, date.Day, 0, 0);
        using var evidence = File.OpenText(evidencePath);
        return await ImportEvidenceAsync(evidence, snapshotAt, cancellationToken);
    }

    /// <summary>Імпортує локальний експорт доказів Kaikki; без revid він лишається матеріалом для редактора.</summary>
    public async Task<ReferenceImportResult> ImportEvidenceAsync(TextReader reader, Instant snapshotAt,
        CancellationToken cancellationToken = default)
    {
        var imported = 0;
        var unchanged = 0;
        var manual = 0;
        var issues = new List<ReferenceImportIssue>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            string lemma = "";
            ReferenceParseResult? parsed = null;
            try
            {
                if (line.Length > 1_000_000) throw new FormatException();
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                lemma = root.GetProperty("lemma_pl").GetString() ?? "";
                if (!ValidText(lemma, 200)) throw new FormatException();
                var url = root.GetProperty("source_url").GetString();
                if (url != PageUrl(lemma)) throw new FormatException();
                var values = new List<ReferenceValue>();
                foreach (var item in root.GetProperty("translation_candidates").EnumerateArray())
                {
                    var translation = item.GetString();
                    if (!ValidText(translation, 500)) throw new FormatException();
                    values.Add(new(ReferenceFields.Translation, translation));
                }
                var ipa = root.GetProperty("ipa").GetString() ?? "";
                if (!ValidText(ipa, 500)) throw new FormatException();
                values.Add(new("ipa", ipa));
                var converted = converter.Convert(ipa);
                if (converted.Value is not null) values.Add(new(ReferenceFields.Transcription, converted.Value));
                var decisions = new List<ReferenceValue>
                {
                    new(ReferenceFields.Translation, "revision_missing"),
                    new(ReferenceFields.Transcription, converted.ManualReason ?? "revision_missing")
                };
                parsed = new(values, decisions);
                manual++;
            }
            catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
            {
                manual++;
                if (issues.Count < 100) issues.Add(new(lemma, "invalid_evidence_record"));
                if (ValidText(lemma, 200))
                    parsed = Failure("invalid_evidence_record");
            }
            if (parsed is null) continue;
            if (await StoreAsync(lemma, WikislownikReferenceSource.KaikkiSourceName, null, snapshotAt, parsed, cancellationToken)) imported++;
            else unchanged++;
        }
        return new(imported, unchanged, manual, issues);
    }

    /// <summary>Читає розпакований pages-articles XML локально; DTD та зовнішні сутності заборонено.</summary>
    public async Task<ReferenceImportResult> ImportWikislownikDumpAsync(Stream stream, Instant snapshotAt,
        CancellationToken cancellationToken = default)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersFromEntities = 0, IgnoreComments = true
        });
        var imported = 0;
        var unchanged = 0;
        var manual = 0;
        var issues = new List<ReferenceImportIssue>();
        while (!reader.EOF)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "page")
            {
                await reader.ReadAsync();
                continue;
            }
            var page = (XElement)await XNode.ReadFromAsync(reader, cancellationToken);
            var ns = page.Name.Namespace;
            if (page.Element(ns + "ns")?.Value != "0") continue;
            var lemma = page.Element(ns + "title")?.Value;
            if (string.IsNullOrWhiteSpace(lemma) || lemma.Length > 200)
            {
                manual++;
                if (issues.Count < 100) issues.Add(new("", "invalid_page_title"));
                continue;
            }
            var revisions = page.Elements(ns + "revision").ToArray();
            var revision = revisions.Length == 1 ? revisions[0] : null;
            var revisionId = revision?.Element(ns + "id")?.Value;
            var text = revision?.Element(ns + "text")?.Value;
            var validRevision = revisionId is { Length: > 0 and <= 32 } && revisionId.All(char.IsAsciiDigit);
            var parsed = validRevision && text is not null ? parser.Parse(text) : Failure("invalid_revision_or_text");
            if (parsed.ManualDecisions.Count > 0)
            {
                manual++;
                foreach (var decision in parsed.ManualDecisions)
                    if (issues.Count < 100) issues.Add(new(lemma, decision.Field + ":" + decision.Value));
            }
            if (await StoreAsync(lemma, WikislownikReferenceSource.SourceName, validRevision ? revisionId : null,
                    snapshotAt, parsed, cancellationToken)) imported++;
            else unchanged++;
        }
        return new(imported, unchanged, manual, issues);
    }

    private async Task<bool> StoreAsync(string lemma, string source, string? revisionId, Instant snapshotAt,
        ReferenceParseResult parsed, CancellationToken cancellationToken)
    {
        var values = parsed.Values.Concat(parsed.ManualDecisions.Select(decision =>
            new ReferenceValue(ReferenceFields.ManualReview, decision.Field + ":" + decision.Value))).Distinct().ToArray();
        var folded = SearchTextNormalizer.Fold(lemma);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Імпорти серіалізовано між процесами; читач бачить цілу попередню або цілу нову лему.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(253501)", cancellationToken);
        var existing = await db.ReferenceEntries.Where(entry => entry.Source == source && entry.LemmaFolded == folded && entry.Lemma == lemma)
            .ToListAsync(cancellationToken);
        if (existing.Any(entry => entry.RetrievedAt > snapshotAt) ||
            (existing.Count == values.Length && existing.All(entry => entry.RetrievedAt == snapshotAt &&
                entry.RevisionId == revisionId && values.Contains(new ReferenceValue(entry.Field, entry.Value)))))
        {
            await transaction.CommitAsync(cancellationToken);
            foreach (var entry in existing) db.Entry(entry).State = EntityState.Detached;
            return false;
        }
        db.ReferenceEntries.RemoveRange(existing);
        db.ReferenceEntries.AddRange(values.Select(value => new ReferenceEntry
        {
            Source = source, Lemma = lemma, LemmaFolded = folded, Field = value.Field, Value = value.Value,
            Url = PageUrl(lemma), RevisionId = revisionId, RetrievedAt = snapshotAt, CreatedAt = clock.GetCurrentInstant()
        }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        foreach (var entry in existing) db.Entry(entry).State = EntityState.Detached;
        foreach (var entry in db.ChangeTracker.Entries<ReferenceEntry>().ToArray()) entry.State = EntityState.Detached;
        return true;
    }

    private static ReferenceParseResult Failure(string reason) => new([], [
        new(ReferenceFields.Translation, reason), new(ReferenceFields.Transcription, reason)]);
    private static string PageUrl(string lemma) => "https://pl.wiktionary.org/wiki/" + Uri.EscapeDataString(lemma.Replace(' ', '_'));
    private static bool ValidText([NotNullWhen(true)] string? text, int maximum) => !string.IsNullOrWhiteSpace(text) && text.Length <= maximum && !text.Any(char.IsControl);
}
