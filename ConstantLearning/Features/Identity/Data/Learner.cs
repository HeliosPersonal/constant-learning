using NodaTime;

namespace ConstantLearning.Features.Identity.Data;

public sealed class Learner
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public long TelegramUserId { get; set; }
    public string? FirstName { get; set; }
    public required string TimeZone { get; set; }
    public bool TimeZoneConfirmed { get; set; }
    public short DailyNewWordsLimit { get; set; }
    public short? PendingDailyNewWordsLimit { get; set; }
    public Instant? PendingLimitEffectiveFrom { get; set; }
    public Instant? NewWordsAvailableFrom { get; set; }
    public Instant? CourseCompletedAt { get; set; }
    public LocalDate? CurrentDayDate { get; set; }
    public Instant? CurrentDayStartsAt { get; set; }
    public Instant? CurrentDayEndsAt { get; set; }
    public Instant? CurrentDayOriginalEndsAt { get; set; }
    public string? CurrentDayTimeZoneAtStart { get; set; }
    public string? ReporterAlias { get; set; }
    public bool CanMessage { get; set; }
    public Instant? OnboardedAt { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
    public ReminderSchedule? ReminderSchedule { get; set; }
}
