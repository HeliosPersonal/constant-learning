using NodaTime;

namespace ConstantLearning.Features.Identity.ReminderAudience;

public sealed record ReminderPreferences(LocalTime LocalTime, short DaysOfWeek, int DailyNewWordsLimit,
    Instant? NewWordsAvailableFrom);

/// <summary>Налаштування нагадувань без блокування learner у транзакції dispatcher.</summary>
public interface IReminderAudienceReader
{
    Task<IReadOnlyList<Guid>> ReadRecipientsAsync(Guid? after, int take, CancellationToken cancellationToken = default);
    Task<ReminderPreferences?> ReadAsync(Guid learnerId, CancellationToken cancellationToken = default);
}
