using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Content.Notifications;

internal sealed class ReportNotificationReader(AppDbContext db) : IReportNotificationReader
{
    public async Task<IReadOnlyList<UnsummarizedReportRecipient>> ReadRecipientsAsync(Guid? after, int limit,
        CancellationToken cancellationToken = default)
    {
        var reports = db.ContentReports.AsNoTracking().Where(report => report.Status == "closed"
            && report.ClosedAt != null && report.SummaryDispatchId == null);
        if (after is { } anchor) reports = reports.Where(report => report.LearnerId.CompareTo(anchor) > 0);
        return await reports.GroupBy(report => report.LearnerId).OrderBy(group => group.Key).Take(limit)
            .Select(group => new UnsummarizedReportRecipient(group.Key, group.Min(report => report.ClosedAt)!.Value))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ReportNotificationBatch> ReadForReservationAsync(Guid learnerId, Guid dispatchId, Instant through,
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        var rows = await db.ContentReports.FromSqlInterpolated($"""
            SELECT *, xmin FROM content_reports
            WHERE learner_id = {learnerId} AND status = 'closed' AND closed_at <= {through}
              AND (summary_dispatch_id IS NULL OR summary_dispatch_id = {dispatchId})
            ORDER BY (summary_dispatch_id = {dispatchId}) DESC NULLS LAST, closed_at, id
            LIMIT 256 FOR UPDATE
            """).AsNoTracking().ToListAsync(cancellationToken);
        var versionIds = rows.SelectMany(report => new[] { report.VersionId, report.ResultingVersionId })
            .Where(id => id != null).Select(id => id!.Value).Distinct().ToArray();
        var lemmas = await db.EntryVersions.AsNoTracking().Where(version => versionIds.Contains(version.Id))
            .ToDictionaryAsync(version => version.Id, version => version.LemmaPl, cancellationToken);
        var total = await db.ContentReports.CountAsync(report => report.LearnerId == learnerId && report.Status == "closed"
            && report.ClosedAt <= through && (report.SummaryDispatchId == null || report.SummaryDispatchId == dispatchId), cancellationToken);
        return new(rows.Select(report => new ReportNotificationOutcome(report.Id,
            (report.VersionId is { } version ? lemmas.GetValueOrDefault(version) : null)
                ?? (report.ResultingVersionId is { } resulting ? lemmas.GetValueOrDefault(resulting) : null)
                ?? report.SearchQuery ?? "Запис словника", report.Outcome!, report.RejectionReason, report.Origin, report.Field)).ToArray(), total);
    }

    public async Task ReserveAsync(Guid learnerId, Guid dispatchId, IReadOnlyCollection<Guid> reportIds,
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        // Позначка означає саме рядок у повідомленні; надлишок попереднього резервування повертаємо в чергу.
        await db.ContentReports.Where(report => report.LearnerId == learnerId && report.SummaryDispatchId == dispatchId
            && !reportIds.Contains(report.Id))
            .ExecuteUpdateAsync(update => update.SetProperty(report => report.SummaryDispatchId, (Guid?)null), cancellationToken);
        var reserved = await db.ContentReports.Where(report => report.LearnerId == learnerId && reportIds.Contains(report.Id)
            && report.Status == "closed" && (report.SummaryDispatchId == null || report.SummaryDispatchId == dispatchId))
            .ExecuteUpdateAsync(update => update.SetProperty(report => report.SummaryDispatchId, dispatchId), cancellationToken);
        if (reserved != reportIds.Count) throw new InvalidOperationException("Не вдалося атомарно зарезервувати результати підсумку.");
    }

    public async Task<EditorReportCounts> ReadEditorCountsAsync(Instant? after, Instant through,
        CancellationToken cancellationToken = default) => new(
        await db.ContentReports.CountAsync(report => report.CreatedAt <= through && (after == null || report.CreatedAt > after), cancellationToken),
        await db.ContentReports.CountAsync(report => report.Status == "in_editor_queue", cancellationToken));

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Резервування потребує транзакції dispatcher.");
    }
}
