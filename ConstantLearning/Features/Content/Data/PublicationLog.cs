using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class PublicationLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid EntryId { get; set; }
    public Guid? FromVersionId { get; set; }
    public Guid? ToVersionId { get; set; }
    public required string Action { get; set; }
    public required string ActorKind { get; set; }
    public Guid? ActorLearnerId { get; set; }
    public Instant CreatedAt { get; set; }
}
