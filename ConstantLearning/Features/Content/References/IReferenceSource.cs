using NodaTime;

namespace ConstantLearning.Features.Content.References;

public enum ReferenceStatus { Available, ManualReview }

public sealed record ReferenceQuery(string Lemma, string Field);

public sealed record ReferenceCandidate(
    string Value, string Source, string Url, string? RevisionId, Instant SnapshotAt,
    string Attribution, string? License);

public sealed record ReferenceResult(ReferenceStatus Status, IReadOnlyList<ReferenceCandidate> Candidates, string? Reason)
{
    public static ReferenceResult Manual(string reason, IReadOnlyList<ReferenceCandidate>? candidates = null) =>
        new(ReferenceStatus.ManualReview, candidates ?? [], reason);
}

/// <summary>Повертає джерельні кандидати; рішення про публікацію ухвалює валідатор звіту.</summary>
public interface IReferenceSource
{
    string Name { get; }
    Task<ReferenceResult> LookupAsync(ReferenceQuery query, CancellationToken cancellationToken = default);
}

public static class ReferenceFields
{
    public const string Translation = "translation";
    public const string Transcription = "transcription";
    internal const string ManualReview = "manual_review";
    public static bool IsSupported(string field) => field is Translation or Transcription;
}
