using NodaTime;

namespace ConstantLearning.Features.Identity.Data;

/// <summary>
/// Повний стан доби навчання для пари (learner, цільова мова). Облік нових слів,
/// межі поточної доби й завершення курсу стають незалежними на мову
/// (ADR-0002). Оптимістична конкурентність — через <see cref="Version"/> (xmin).
/// </summary>
public sealed class LearnerLanguageState
{
    public Guid LearnerId { get; set; }

    /// <summary>Код цільової мови; друга частина складеного ключа (FK → target_languages.code).</summary>
    public required string TargetLanguage { get; set; }

    public short DailyNewWordsLimit { get; set; }
    public short? PendingDailyNewWordsLimit { get; set; }
    public Instant? PendingLimitEffectiveFrom { get; set; }
    public Instant? NewWordsAvailableFrom { get; set; }
    public LocalDate? CurrentDayDate { get; set; }
    public Instant? CurrentDayStartsAt { get; set; }
    public Instant? CurrentDayEndsAt { get; set; }
    public Instant? CurrentDayOriginalEndsAt { get; set; }
    public string? CurrentDayTimeZoneAtStart { get; set; }
    public Instant? CourseCompletedAt { get; set; }
    public Instant CreatedAt { get; set; }
    public uint Version { get; set; }
}
