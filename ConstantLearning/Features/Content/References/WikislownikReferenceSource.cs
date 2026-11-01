using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Content.References;

public sealed class WikislownikReferenceSource(AppDbContext db, IClock clock) : IReferenceSource
{
    public const string SourceName = "wikislownik";
    public const string KaikkiSourceName = "wikislownik_kaikki";
    public const string Attribution = "Автори Wikisłownik; CC BY-SA 4.0; адаптовано переклади та підказки вимови. Історія авторів доступна за URL сторінки.";
    public string Name => SourceName;

    public async Task<ReferenceResult> LookupAsync(ReferenceQuery query, CancellationToken cancellationToken = default)
    {
        if (!ReferenceFields.IsSupported(query.Field)) return ReferenceResult.Manual("unsupported_field");
        if (string.IsNullOrWhiteSpace(query.Lemma) || query.Lemma.Length > 200) return ReferenceResult.Manual("invalid_lemma");
        var folded = SearchTextNormalizer.Fold(query.Lemma);
        var rows = await db.ReferenceEntries.AsNoTracking().Where(entry =>
            (entry.Source == SourceName || entry.Source == KaikkiSourceName) && entry.LemmaFolded == folded &&
            (entry.Field == query.Field || entry.Field == ReferenceFields.ManualReview)).ToListAsync(cancellationToken);
        // Згортання прискорює пошук, але не робить різні польські леми тотожними.
        rows = rows.Where(entry => SearchTextNormalizer.Normalize(entry.Lemma) == SearchTextNormalizer.Normalize(query.Lemma)).ToList();
        if (rows.Count == 0) return ReferenceResult.Manual("reference_missing");
        var source = rows.Any(entry => entry.Source == SourceName) ? SourceName : KaikkiSourceName;
        rows = rows.Where(entry => entry.Source == source).ToList();
        var candidates = rows.Where(entry => entry.Field == query.Field).Select(entry => new ReferenceCandidate(
            entry.Value, entry.Source, entry.Url, entry.RevisionId, entry.RetrievedAt, Attribution, "CC-BY-SA-4.0")).ToArray();
        var manual = rows.FirstOrDefault(entry => entry.Field == ReferenceFields.ManualReview && entry.Value.StartsWith(query.Field + ":", StringComparison.Ordinal));
        if (manual is not null) return ReferenceResult.Manual(manual.Value[(query.Field.Length + 1)..], candidates);
        if (candidates.Length == 0) return ReferenceResult.Manual("reference_missing");
        if (candidates.Any(entry => entry.RevisionId is null)) return ReferenceResult.Manual("revision_missing", candidates);
        var now = clock.GetCurrentInstant();
        if (candidates.Any(entry => entry.SnapshotAt > now || now - entry.SnapshotAt > Duration.FromDays(35)))
            return ReferenceResult.Manual("snapshot_stale", candidates);
        return new(ReferenceStatus.Available, candidates, null);
    }
}
