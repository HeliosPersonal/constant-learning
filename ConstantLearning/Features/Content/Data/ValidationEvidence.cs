using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class ValidationEvidence
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VersionId { get; set; }
    public required string Field { get; set; }
    public required string Source { get; set; }
    public required string Url { get; set; }
    public string? RevisionId { get; set; }
    public string? Attribution { get; set; }
    public required string Outcome { get; set; }
    public Instant RetrievedAt { get; set; }
    public Instant CreatedAt { get; set; }
}
