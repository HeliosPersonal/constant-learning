using NodaTime;

namespace ConstantLearning.Features.Notifications.Data;

public sealed class NotificationDispatch
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public long RecipientTelegramUserId { get; set; }
    public Guid? LearnerId { get; set; }
    public required string Kind { get; set; }
    public LocalDate LocalDate { get; set; }
    public Instant DueAt { get; set; }
    public required string Status { get; set; }
    public short Attempts { get; set; }
    public Instant? ClaimedAt { get; set; }
    public Instant? SentAt { get; set; }
    public string? Reason { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
}
