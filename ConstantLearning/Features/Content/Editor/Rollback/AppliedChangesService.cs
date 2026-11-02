using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Editor.Queue;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Features.Content.Editor.Rollback;

public sealed class AppliedChangesService(AppDbContext db, ContentPublicationService publication)
{
    public async Task<AppliedChangePage?> ListAsync(AppliedChangesRequest request, CancellationToken cancellationToken)
    {
        var changes = EligibleChanges();
        var window = changes;
        if ((request.After ?? request.Before) is { } cursor)
        {
            var anchor = await changes.SingleOrDefaultAsync(row => row.Id == cursor, cancellationToken);
            if (anchor is null) return null;
            window = request.After is not null ? Following(changes, anchor) : Preceding(changes, anchor);
        }
        var ordered = request.Before is null ? window.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id)
            : window.OrderBy(row => row.CreatedAt).ThenBy(row => row.Id);
        var rows = await ordered.Take(request.Limit).ToListAsync(cancellationToken);
        if (request.Before is not null) rows.Reverse();
        var hasNext = rows.Count > 0 && await Following(changes, rows[^1]).AnyAsync(cancellationToken);
        var hasPrev = rows.Count > 0 && await Preceding(changes, rows[0]).AnyAsync(cancellationToken);
        var versionIds = rows.SelectMany(row => new[] { row.FromVersionId, row.ToVersionId })
            .OfType<Guid>().Distinct().ToArray();
        var versions = await db.EntryVersions.AsNoTracking().Where(row => versionIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var translations = await db.VersionTranslations.AsNoTracking().Where(row => versionIds.Contains(row.VersionId)
            && row.Kind == "canonical").ToDictionaryAsync(row => row.VersionId, row => row.TextUk, cancellationToken);
        var reportIds = versions.Values.Select(row => row.ContentReportId).OfType<Guid>().Distinct().ToArray();
        var reports = await db.ContentReports.AsNoTracking().Where(row => reportIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => new { row.Origin, row.Field }, cancellationToken);
        var acceptedAnswers = (await db.VersionAcceptedAnswers.AsNoTracking()
            .Where(row => versionIds.Contains(row.VersionId))
            .Select(row => new { row.VersionId, row.TextPl }).ToListAsync(cancellationToken))
            .ToLookup(row => row.VersionId, row => row.TextPl);
        var entryIds = rows.Select(row => row.EntryId).Distinct().ToArray();
        var published = await db.DictionaryEntries.AsNoTracking().Where(row => entryIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.PublishedVersionId, cancellationToken);
        string? Value(EntryVersion? version, string field) => field switch
        {
            "translation" when version is not null => translations.GetValueOrDefault(version.Id),
            "pronunciation" => version?.PronunciationUk,
            "lemma" => version?.LemmaPl,
            "example" => version?.ExamplePl,
            _ => null
        };
        var items = rows.Select(row =>
        {
            var next = versions[row.ToVersionId!.Value];
            var previous = row.FromVersionId is { } previousId ? versions.GetValueOrDefault(previousId) : null;
            var report = reports[next.ContentReportId!.Value];
            var field = report.Origin == "answer_dispute" ? "accepted_answer" : report.Field!;
            // Різниця версій містить остаточний варіант, навіть якщо редактор виправив пропозицію learner.
            var newValue = field == "accepted_answer"
                ? acceptedAnswers[next.Id].Except(acceptedAnswers[previous?.Id ?? Guid.Empty], StringComparer.Ordinal).Single()
                : Value(next, field)!;
            return new AppliedChange(row.Id, row.EntryId, next.LemmaPl, field, Value(previous, field),
                newValue, row.ActorKind, next.ContentReportId.Value, row.CreatedAt.ToDateTimeOffset(),
                published[row.EntryId] == next.Id && previous?.ValidationStatus == "approved");
        }).ToArray();
        return new(items, hasNext, hasPrev, hasNext ? rows[^1].Id : null);
    }

    internal async Task<RollbackResponse> RollbackAsync(Guid changeId, Guid? editorId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Єдиний порядок блокувань із публікацією: спершу publication, потім версії.
            await publication.AcquireLockAsync(cancellationToken);
            var change = await EligibleChanges().SingleOrDefaultAsync(row => row.Id == changeId, cancellationToken);
            if (change is null) return new(404, Error: NotFound());
            var current = await db.DictionaryEntries.AsNoTracking().Where(row => row.Id == change.EntryId)
                .Select(row => row.PublishedVersionId).SingleAsync(cancellationToken);
            if (current != change.ToVersionId || change.FromVersionId is not { } previousId) return Conflict();
            // Зняття approved також не може випередити перевірку й commit відкату.
            var previous = (await db.EntryVersions.FromSqlInterpolated(
                $"SELECT *, xmin FROM entry_versions WHERE id = {previousId} FOR UPDATE")
                .AsNoTracking().ToListAsync(cancellationToken)).SingleOrDefault();
            if (previous?.ValidationStatus != "approved" || previous.EntryId != change.EntryId) return Conflict();
            await publication.ApplyAsync([new(change.EntryId, change.ToVersionId, previousId, "editor", editorId, "rollback")], cancellationToken);
            var rollback = db.ChangeTracker.Entries<PublicationLog>().Select(row => row.Entity)
                .Single(row => row.Action == "rollback" && row.EntryId == change.EntryId);
            await transaction.CommitAsync(cancellationToken);
            return new(200, new(change.Id, change.EntryId, previousId, rollback.CreatedAt.ToDateTimeOffset()));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Conflict();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally { db.ChangeTracker.Clear(); }
    }

    private IQueryable<PublicationLog> EligibleChanges() =>
        from change in db.PublicationLog.AsNoTracking()
        join version in db.EntryVersions.AsNoTracking() on change.ToVersionId equals version.Id
        join report in db.ContentReports.AsNoTracking() on version.ContentReportId equals report.Id
        where change.Action == "publish" && (change.ActorKind == "auto_verification" || change.ActorKind == "editor")
        select change;

    public static EditorError NotFound() => new("editor.change_not_found", "Зміну не знайдено.");
    private static RollbackResponse Conflict() => new(409, Error: new("editor.rollback_not_current",
        "Відкотити можна лише поточну версію запису до попередньої approved-версії."));
    private static IQueryable<PublicationLog> Following(IQueryable<PublicationLog> query, PublicationLog anchor) =>
        query.Where(row => row.CreatedAt < anchor.CreatedAt || row.CreatedAt == anchor.CreatedAt && row.Id.CompareTo(anchor.Id) < 0);
    private static IQueryable<PublicationLog> Preceding(IQueryable<PublicationLog> query, PublicationLog anchor) =>
        query.Where(row => row.CreatedAt > anchor.CreatedAt || row.CreatedAt == anchor.CreatedAt && row.Id.CompareTo(anchor.Id) > 0);
}
