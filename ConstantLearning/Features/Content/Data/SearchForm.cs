using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class SearchForm
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid EntryId { get; set; }
    public Guid VersionId { get; set; }
    public required string Lang { get; set; }
    public required string FormDisplay { get; set; }
    public required string FormFolded { get; set; }
    public bool IsLemma { get; set; }
    public Instant CreatedAt { get; set; }
}
