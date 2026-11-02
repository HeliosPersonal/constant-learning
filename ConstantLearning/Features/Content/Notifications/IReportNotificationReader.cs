using NodaTime;

namespace ConstantLearning.Features.Content.Notifications;

public sealed record UnsummarizedReportRecipient(Guid LearnerId, Instant EarliestClosedAt);
public sealed record ReportNotificationOutcome(Guid Id, string Label, string Outcome, string? RejectionReason,
    string Origin, string? Field);
public sealed record ReportNotificationBatch(IReadOnlyList<ReportNotificationOutcome> Items, int TotalCount);
public sealed record EditorReportCounts(int NewlyReceived, int Unresolved);

public interface IReportNotificationReader
{
    Task<IReadOnlyList<UnsummarizedReportRecipient>> ReadRecipientsAsync(Guid? after, int limit,
        CancellationToken cancellationToken = default);
    /// <summary>Блокує доступні результати в транзакції dispatcher; уже зарезервовані цим dispatch ідуть першими.</summary>
    Task<ReportNotificationBatch> ReadForReservationAsync(Guid learnerId, Guid dispatchId, Instant through,
        CancellationToken cancellationToken = default);
    Task ReserveAsync(Guid learnerId, Guid dispatchId, IReadOnlyCollection<Guid> reportIds,
        CancellationToken cancellationToken = default);
    Task<EditorReportCounts> ReadEditorCountsAsync(Instant? after, Instant through,
        CancellationToken cancellationToken = default);
}
