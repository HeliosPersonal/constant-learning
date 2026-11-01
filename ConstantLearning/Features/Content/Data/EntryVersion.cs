using NodaTime;

namespace ConstantLearning.Features.Content.Data;

public sealed class EntryVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid EntryId { get; set; }
    public Guid? BasedOnVersionId { get; set; }
    public required string LemmaPl { get; set; }
    public required string PartOfSpeech { get; set; }
    public string? Gender { get; set; }
    public string? GenderPairId { get; set; }
    public string? AspectPairId { get; set; }
    public required string PronunciationUk { get; set; }
    public required string ExamplePl { get; set; }
    public required string ExampleUk { get; set; }
    public bool ShowContextBefore { get; set; }
    public int? FrequencyRank { get; set; }
    public int? UtilityRank { get; set; }
    public string? LegalizationStage { get; set; }
    public string? Topic { get; set; }
    public required string ValidationStatus { get; set; }
    public required string CreatedByKind { get; set; }
    public Guid? ContentReportId { get; set; }
    public required string ContentHash { get; set; }
    public string? ReviewNote { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
    public DictionaryEntry Entry { get; set; } = null!;
    public EntryVersion? BasedOnVersion { get; set; }
}
