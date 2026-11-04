using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class DictionaryEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string ExternalId { get; set; }
    public required string Domain { get; set; }

    // Мова запису (ADR-0003): дискримінатор колонки target_language, NOT NULL.
    // TODO(T07a): зняти інтеримний дефолт 'pl' і зробити властивість required,
    // коли узагальнення мови прибере хардкод 'pl' з місць створення записів.
    public string TargetLanguage { get; set; } = "pl";
    public Guid? PublishedVersionId { get; set; }
    public int? CoursePosition { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
    public EntryVersion? PublishedVersion { get; set; }
}
