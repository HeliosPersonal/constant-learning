using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class ContentReport
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LearnerId { get; set; }
    public Guid? EntryId { get; set; }
    public Guid? VersionId { get; set; }
    public Guid? AnswerEventId { get; set; }
    public required string Origin { get; set; }
    public string? Field { get; set; }
    public string? ProposedValue { get; set; }
    public string? Comment { get; set; }
    public string? SearchQuery { get; set; }
    public required string Status { get; set; }
    public string? Outcome { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ResultingVersionId { get; set; }
    public Instant? ClosedAt { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
}
