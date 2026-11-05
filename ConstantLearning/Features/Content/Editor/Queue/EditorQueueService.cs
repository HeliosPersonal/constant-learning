using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Features.Identity.Aliases;
using ConstantLearning.Features.Identity.Languages;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Content.Editor.Queue;

public sealed class EditorQueueService(AppDbContext db, ContentPublicationService publication,
    IReporterAliasService reporterAliases, ITargetLanguageCatalog languages, IClock clock)
{
    public async Task<EditorQueuePage?> ListAsync(EditorQueueRequest request, CancellationToken cancellationToken)
    {
        var reports = db.ContentReports.AsNoTracking().Where(row => row.Status == "in_editor_queue");
        if (request.TargetLanguage is { } language)
        {
            // Невідомий код мови → null → контролер віддає 400 (єдиний шлях валідації, як у startLearnerLanguage).
            if (await languages.FindAsync(language, cancellationToken) is null) return null;
            reports = reports.Where(row => row.TargetLanguage == language);
        }
        var window = reports;
        if ((request.After ?? request.Before) is { } cursor)
        {
            // Закритий рядок лишається стабільним якорем, доки редактор переглядає наступну сторінку.
            var anchor = await db.ContentReports.AsNoTracking().SingleOrDefaultAsync(row => row.Id == cursor, cancellationToken);
            if (anchor is null) return null;
            window = request.After is not null ? Following(reports, anchor) : Preceding(reports, anchor);
        }
        var ordered = request.Before is null ? window.OrderBy(row => row.CreatedAt).ThenBy(row => row.Id)
            : window.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id);
        var rows = await ordered.Take(request.Limit).ToListAsync(cancellationToken);
        if (request.Before is not null) rows.Reverse();
        var hasNext = rows.Count > 0 && await Following(reports, rows[^1]).AnyAsync(cancellationToken);
        var hasPrev = rows.Count > 0 && await Preceding(reports, rows[0]).AnyAsync(cancellationToken);
        var learnerIds = rows.Select(row => row.LearnerId).Distinct().ToArray();
        var aliases = await reporterAliases.ReadAsync(learnerIds, cancellationToken);
        var entryIds = rows.Where(row => row.EntryId != null).Select(row => row.EntryId!.Value).Distinct().ToArray();
        var versions = await (from entry in db.DictionaryEntries.AsNoTracking()
                              join version in db.EntryVersions.AsNoTracking() on entry.PublishedVersionId equals version.Id
                              where entryIds.Contains(entry.Id)
                              select version).ToDictionaryAsync(row => row.EntryId, cancellationToken);
        var versionIds = versions.Values.Select(row => row.Id).ToArray();
        var translations = await db.VersionTranslations.AsNoTracking().Where(row => versionIds.Contains(row.VersionId)
            && row.Kind == "canonical").ToDictionaryAsync(row => row.VersionId, row => row.TextUk, cancellationToken);
        var items = rows.Select(row =>
        {
            var version = row.EntryId is { } id ? versions.GetValueOrDefault(id) : null;
            var current = row.Field switch
            {
                "translation" when version is not null => translations.GetValueOrDefault(version.Id),
                "pronunciation" => version?.PronunciationUk,
                "lemma" => version?.LemmaPl,
                "example" => version?.ExamplePl,
                _ => null
            };
            return new EditorQueueItem(row.Id, row.TargetLanguage, aliases.GetValueOrDefault(row.LearnerId) ?? "R-UNKNOWN", row.Origin,
                row.EntryId, version?.LemmaPl, row.Field, current, row.ProposedValue, row.Comment, row.SearchQuery, row.CreatedAt.ToDateTimeOffset());
        }).ToArray();
        return new(items, hasNext, hasPrev, hasNext ? rows[^1].Id : null);
    }

    internal async Task<EditorDecisionResult> DecideAsync(Guid reportId, Guid? editorId, EditorDecision decision,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Спільний порядок із worker: publication перед report; жодного захоплення job після report.
            if (decision.Decision is "approve" or "add_to_candidates") await publication.AcquireLockAsync(cancellationToken);
            var report = (await db.ContentReports.FromSqlInterpolated(
                $"SELECT *, xmin FROM content_reports WHERE id = {reportId} FOR UPDATE").ToListAsync(cancellationToken)).SingleOrDefault();
            if (report is null) return new(404, Error: NotFound());
            if (report.Status == "closed") return new(409, Error: new("report.already_closed", "Це повідомлення вже розглянуто."));
            if (report.Status != "in_editor_queue") return Invalid("report.not_in_editor_queue", "Повідомлення ще проходить автоматичну звірку.");
            var now = clock.GetCurrentInstant();
            if (decision.Decision == "reject")
            {
                var missingReason = decision.RejectionReason is "already_in_dictionary" or "phrase_not_lemma" or "out_of_scope";
                var normalReason = decision.RejectionReason is "source_not_confirming" or "already_correct" or "out_of_scope" or "duplicate";
                if (!(report.Origin == "missing_word" ? missingReason : normalReason)) return Invalid("editor.invalid_rejection_reason", "Причина не відповідає типу повідомлення.");
                report.Outcome = "rejected";
                report.RejectionReason = decision.RejectionReason;
            }
            else if (decision.Decision == "add_to_candidates")
            {
                if (report.Origin != "missing_word") return Invalid("editor.invalid_decision", "До кандидатів додають лише відсутню лему.");
                var lemma = (decision.CorrectedValue ?? report.SearchQuery ?? "").Trim().Normalize().ToLowerInvariant();
                if (!EditorVersionFactory.IsLemma(lemma)) return Invalid("editor.invalid_value", "Кандидат має бути однією польською лемою.");
                if (await db.EntryVersions.AnyAsync(row => row.LemmaPl == lemma, cancellationToken))
                    return Invalid("editor.duplicate_lemma", "Така лема вже є у словнику або кандидатах.");
                var entry = new DictionaryEntry { ExternalId = "candidate-" + report.Id.ToString("N"), Domain = "everyday", CreatedAt = now };
                var version = new EntryVersion
                {
                    Entry = entry,
                    LemmaPl = lemma,
                    PartOfSpeech = "",
                    PronunciationUk = "",
                    ExamplePl = "",
                    ExampleUk = "",
                    ValidationStatus = "draft",
                    CreatedByKind = "editor",
                    ContentReportId = report.Id,
                    ContentHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(lemma))),
                    CreatedAt = now
                };
                db.AddRange(entry, version);
                report.Outcome = "added_to_candidates";
                report.ResultingVersionId = version.Id;
            }
            else
            {
                if (report.Origin == "missing_word" || report.EntryId is not { } entryId)
                    return Invalid("editor.invalid_decision", "Відсутню лему спершу додають до кандидатів.");
                var entry = await db.DictionaryEntries.SingleOrDefaultAsync(row => row.Id == entryId, cancellationToken);
                if (entry?.PublishedVersionId is not { } publishedId)
                    return Invalid("editor.approval_requirements_not_met", "Запис не має опублікованої approved-версії.");
                var previous = await db.EntryVersions.SingleAsync(row => row.Id == publishedId, cancellationToken);
                if (report.VersionId != previous.Id)
                    return Invalid("editor.published_version_changed", "Опублікована версія змінилася; перевірте актуальний запис.");
                var value = (decision.CorrectedValue ?? report.ProposedValue ?? "").Trim().Normalize();
                var (next, error) = await new EditorVersionFactory(db).CreateAsync(entry, previous, report, value, now, cancellationToken);
                if (error is not null) return new(400, Error: error);
                await publication.ApplyAsync([new(entry.Id, previous.Id, next!.Id, "editor", editorId)], cancellationToken);
                report.Outcome = "applied";
                report.ResultingVersionId = next.Id;
            }
            report.Status = "closed";
            report.ClosedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(200, new(report.Id, report.Status, report.Outcome!, report.RejectionReason, report.ResultingVersionId, now.ToDateTimeOffset()));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally { db.ChangeTracker.Clear(); }
    }

    public static EditorError NotFound() => new("report.not_found", "Повідомлення не знайдено.");
    private static EditorDecisionResult Invalid(string code, string message) => new(400, Error: new(code, message));
    private static IQueryable<ContentReport> Following(IQueryable<ContentReport> query, ContentReport anchor) =>
        query.Where(row => row.CreatedAt > anchor.CreatedAt || row.CreatedAt == anchor.CreatedAt && row.Id.CompareTo(anchor.Id) > 0);
    private static IQueryable<ContentReport> Preceding(IQueryable<ContentReport> query, ContentReport anchor) =>
        query.Where(row => row.CreatedAt < anchor.CreatedAt || row.CreatedAt == anchor.CreatedAt && row.Id.CompareTo(anchor.Id) < 0);
}
