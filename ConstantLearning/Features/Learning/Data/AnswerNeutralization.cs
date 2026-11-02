using NodaTime;

namespace ConstantLearning.Features.Learning.Data;

public sealed class AnswerNeutralization
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid AnswerEventId { get; set; }
    public Guid ContentReportId { get; set; }
    public Instant CreatedAt { get; set; }
}
