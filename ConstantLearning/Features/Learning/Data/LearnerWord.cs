using NodaTime;

namespace ConstantLearning.Features.Learning.Data;

public sealed class LearnerWord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid LearnerId { get; set; }
    public Guid EntryId { get; set; }

    // Денормалізований дискримінатор мови (ADR-0001): дорівнює target_language свого entry.
    // T10: продакшн-запис (NewWordService) завжди проставляє мову з активного контексту learner. Дефолт 'pl'
    // лишається запобіжним для тест-фабрик/оснастки, які ще не мово-свідомі; зняти, коли і вони проставлятимуть.
    public string TargetLanguage { get; set; } = "pl";
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
