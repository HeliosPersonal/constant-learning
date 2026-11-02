using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class VersionTranslation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VersionId { get; set; }
    public required string Kind { get; set; }
    public required string TextUk { get; set; }
    public short Position { get; set; }
    public Instant CreatedAt { get; set; }
    public EntryVersion Version { get; set; } = null!;
}
