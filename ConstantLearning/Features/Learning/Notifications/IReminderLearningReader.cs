using NodaTime;

namespace ConstantLearning.Features.Learning.Notifications;

public sealed record ReminderLearningState(bool CompletedToday, bool HasWork, Instant? LastAnsweredAt);

/// <summary>Знімок умов нагадування; не змінює session і не блокує агрегати під захопленим dispatch.</summary>
public interface IReminderLearningReader
{
    /// <summary>Стан дня для однієї цільової мови: усі читання обмежені <paramref name="targetLanguage"/> (ADR-0005).</summary>
    Task<ReminderLearningState> ReadAsync(Guid learnerId, string targetLanguage, LocalDate day, int dailyNewWordsLimit,
        Instant? newWordsAvailableFrom, CancellationToken cancellationToken = default);
}
