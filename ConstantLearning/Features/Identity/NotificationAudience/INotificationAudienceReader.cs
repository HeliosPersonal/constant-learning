using NodaTime;

namespace ConstantLearning.Features.Identity.NotificationAudience;

/// <summary>Мінімальні дані для розкладу сповіщень без імен та псевдонімів.</summary>
public sealed record LearnerNotificationRecipient(Guid LearnerId, long TelegramUserId, string TimeZone,
    LocalDate LocalDate, Instant DayStartsAt, Instant DayEndsAt);

public interface INotificationAudienceReader
{
    Task<IReadOnlyList<LearnerNotificationRecipient>> ReadLearnersAsync(IReadOnlyCollection<Guid> learnerIds,
        CancellationToken cancellationToken = default);
    Task<LearnerNotificationRecipient?> ReadLearnerAsync(Guid learnerId, CancellationToken cancellationToken = default);
    /// <summary>Лише для planner: транзакційний KEY SHARE перед advisory та INSERT із FK; dispatcher використовує звичайне читання.</summary>
    Task<LearnerNotificationRecipient?> ReadForPlanningAsync(Guid learnerId, CancellationToken cancellationToken = default);
    IReadOnlyList<long> ReadEditorTelegramIds();
}
