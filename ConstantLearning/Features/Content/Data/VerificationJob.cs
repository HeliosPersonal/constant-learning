using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class VerificationJob
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ContentReportId { get; set; }
    public required string Status { get; set; }
    public short Attempts { get; set; }
    public Instant NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public Instant? ClaimedAt { get; set; }
    public Instant? CompletedAt { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
}
