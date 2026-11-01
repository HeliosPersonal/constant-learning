using NodaTime;

namespace ConstantLearning.Features.Learning.Data;

public sealed class LearningSession
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LearnerId { get; set; }
    public LocalDate LearnerDay { get; set; }
    public Instant EndsAt { get; set; }
    public required string Status { get; set; }
    public Instant? LastActivityAt { get; set; }
    public Instant? CompletedAt { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
}
