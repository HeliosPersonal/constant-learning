using NodaTime;

namespace ConstantLearning.Features.Identity.Data;

/// <summary>
/// Розклад нагадувань learner для однієї цільової мови (ADR-0005). Пара (learner, мова) — природний складений
/// ключ: кожна мова має власний час і дні, а нагадування однієї мови не змінюють розклад іншої.
/// </summary>
public sealed class ReminderSchedule
{
    public Guid LearnerId { get; set; }

    /// <summary>Код цільової мови; друга частина складеного ключа (FK → target_languages.code).</summary>
    public required string TargetLanguage { get; set; }

    public LocalTime LocalTime { get; set; }
    public short DaysOfWeek { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
    public Learner Learner { get; set; } = null!;
}
