using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class DictionaryEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string ExternalId { get; set; }
    public required string Domain { get; set; }
    public Guid? PublishedVersionId { get; set; }
    public int? CoursePosition { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
    public EntryVersion? PublishedVersion { get; set; }
}
