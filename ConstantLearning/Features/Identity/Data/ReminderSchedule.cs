using NodaTime;

namespace ConstantLearning.Features.Identity.Data;

public sealed class ReminderSchedule
{
    public Guid LearnerId { get; set; }
    public LocalTime LocalTime { get; set; }
    public short DaysOfWeek { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
    public Learner Learner { get; set; } = null!;
}
