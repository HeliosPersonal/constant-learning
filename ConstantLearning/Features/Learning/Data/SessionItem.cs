using NodaTime;

namespace ConstantLearning.Features.Learning.Data;

public sealed class SessionItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SessionId { get; set; }
    public Guid EntryId { get; set; }
    public short Position { get; set; }
    public required string Kind { get; set; }
    public required string State { get; set; }
    public string? AnswerPayloadHash { get; set; }
    public string? ResponsePayload { get; set; }
    public Instant? AnsweredAt { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
    public LearningSession Session { get; set; } = null!;
}
