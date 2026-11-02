using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class VersionAcceptedAnswer
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VersionId { get; set; }
    public required string TextPl { get; set; }
    public bool IsCanonical { get; set; }
    public Instant CreatedAt { get; set; }
    public EntryVersion Version { get; set; } = null!;
}
