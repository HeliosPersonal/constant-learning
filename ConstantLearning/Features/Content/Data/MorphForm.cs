using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class MorphForm
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Lang { get; set; }
    public required string Source { get; set; }
    public required string Lemma { get; set; }
    public required string Form { get; set; }
    public string? Tags { get; set; }
    public Instant CreatedAt { get; set; }
}
