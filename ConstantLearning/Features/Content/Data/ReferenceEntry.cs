using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class ReferenceEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Source { get; set; }
    public required string Lemma { get; set; }
    public required string LemmaFolded { get; set; }
    public required string Field { get; set; }
    public required string Value { get; set; }
    public required string Url { get; set; }
    public string? RevisionId { get; set; }
    public Instant RetrievedAt { get; set; }
    public Instant CreatedAt { get; set; }
}
