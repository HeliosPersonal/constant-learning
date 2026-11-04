using NodaTime;

namespace ConstantLearning.Features.Learning.Data;

public sealed class AnswerEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LearnerId { get; set; }
    public Guid EntryId { get; set; }

    // Денормалізований дискримінатор мови (ADR-0001): дорівнює target_language свого entry.
    // TODO(T10): зняти інтеримний дефолт 'pl', коли ізоляція прогресу писатиме мову з активного контексту.
    public string TargetLanguage { get; set; } = "pl";
    public Guid VersionId { get; set; }
    public Guid? SessionItemId { get; set; }
    public required string ExerciseKind { get; set; }
    public required string Outcome { get; set; }
    public string? RawAnswer { get; set; }
    public string? NormalizedAnswer { get; set; }
    public Instant AnsweredAt { get; set; }
    public LocalDate LearnerDay { get; set; }
    public required string TimeZone { get; set; }
    public LocalDate? DueOn { get; set; }
    public int? IntervalBefore { get; set; }
    public required string SchedulerVersion { get; set; }
    public required string StateBefore { get; set; }
    public Instant CreatedAt { get; set; }
    public SessionItem? SessionItem { get; set; }
}
