using NodaTime;

namespace ConstantLearning.Features.Learning.Notifications;

public sealed record ReminderLearningState(bool CompletedToday, bool HasWork, Instant? LastAnsweredAt);

/// <summary>Знімок умов нагадування; не змінює session і не блокує агрегати під захопленим dispatch.</summary>
public interface IReminderLearningReader
{
    Task<ReminderLearningState> ReadAsync(Guid learnerId, LocalDate day, int dailyNewWordsLimit,
        Instant? newWordsAvailableFrom, CancellationToken cancellationToken = default);
}
