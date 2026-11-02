using NodaTime;

namespace ConstantLearning.Features.Learning.Data;

public sealed class LearnerWord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LearnerId { get; set; }
    public Guid EntryId { get; set; }
    public required string Status { get; set; }
    public short ConsecutiveScheduledCorrect { get; set; }
    public LocalDate? FirstShownDay { get; set; }
    public LocalDate? LastReviewDay { get; set; }
    public int? IntervalDays { get; set; }
    public LocalDate? DueOn { get; set; }
    public bool ControlPending { get; set; }
    public int Lapses { get; set; }
    public required string ModelState { get; set; }
    public required string SchedulerVersion { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
}
