using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Aliases;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using Npgsql;

namespace ConstantLearning.Features.Content.Reports;

public sealed class ContentReportService(AppDbContext db, ILearnerDayStateService learnerDays,
    IReporterAliasService aliases, IClock clock) : IContentReportService
{
    internal async Task<ContentReportResult> CreateAsync(Guid learnerId, ContentReportRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var current = await learnerDays.GetForUpdateAsync(learnerId, cancellationToken);
        if (current is null)
        {
            return NotOnboarded();
        }

        var key = ContentReportDedupKey.Create(request);
        if (await FindOpenAsync(learnerId, key, cancellationToken) is { } existing)
        {
            await transaction.CommitAsync(cancellationToken);
            return Accepted(existing);
        }

        Guid? versionId = null;
        if (request.EntryId is { } entryId)
        {
            versionId = await db.DictionaryEntries.AsNoTracking()
                .Where(entry => entry.Id == entryId && entry.PublishedVersionId != null
                                && entry.PublishedVersion!.ValidationStatus == "approved")
                .Select(entry => entry.PublishedVersionId).SingleOrDefaultAsync(cancellationToken);
            if (versionId is null)
            {
                return new ContentReportResult(null, 404, new ContentReportError("dictionary.entry_not_found", "Слово не знайдено."));
            }
        }

        var count = await db.ContentReports.CountAsync(report => report.LearnerId == learnerId
            && (report.Origin == "card" || report.Origin == "missing_word")
            && report.CreatedAt >= current.Day.StartsAt && report.CreatedAt < current.Day.EndsAt, cancellationToken);
        if (count >= 20)
        {
            return new ContentReportResult(null, 429, new ContentReportError("report.daily_limit_reached",
                "Сьогоднішній ліміт 20 повідомлень вичерпано — нові можна надіслати після 00:00 за вашим часом."));
        }

        await aliases.GetOrCreateAsync(learnerId, cancellationToken);
        var verify = request.Field is "translation" or "pronunciation" && request.ProposedValue is not null;
        var now = clock.GetCurrentInstant();
        var report = new ContentReport
        {
            LearnerId = learnerId, EntryId = request.EntryId, VersionId = versionId,
            Origin = request.Origin, Field = request.Field, ProposedValue = request.ProposedValue,
            Comment = request.Comment, SearchQuery = request.SearchQuery,
            Status = verify ? "pending_verification" : "in_editor_queue", DedupKey = key, CreatedAt = now
        };
        db.ContentReports.Add(report);
        VerificationJob? job = null;
        if (verify)
        {
            job = new VerificationJob
            {
                ContentReportId = report.Id, Status = "pending", Attempts = 0, NextAttemptAt = now, CreatedAt = now
            };
            db.VerificationJobs.Add(job);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_content_reports_open_dedup" })
        {
            // SaveChanges відкотив власний savepoint; унікальний індекс страхує інші шляхи запису.
            if (job is not null) db.Entry(job).State = EntityState.Detached;
            db.Entry(report).State = EntityState.Detached;
            report = await FindOpenAsync(learnerId, key, cancellationToken)
                     ?? throw new InvalidOperationException("Не знайдено відкритий report після конфлікту ключа.");
        }

        await transaction.CommitAsync(cancellationToken);
        return Accepted(report);
    }

    public async Task<Guid?> CreateAnswerDisputeAsync(Guid learnerId, Guid answerEventId, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Report оскарження потребує транзакції викликувача.");
        }

        var answer = await db.AnswerEvents.AsNoTracking().SingleOrDefaultAsync(value =>
            value.Id == answerEventId && value.LearnerId == learnerId, cancellationToken);
        if (answer is null)
        {
            return null;
        }

        if (await learnerDays.GetForUpdateAsync(learnerId, cancellationToken) is null)
        {
            return null;
        }

        var existingId = await db.ContentReports.Where(report => report.LearnerId == learnerId && report.AnswerEventId == answerEventId)
            .Select(report => (Guid?)report.Id).SingleOrDefaultAsync(cancellationToken);
        if (existingId is not null)
        {
            return existingId;
        }

        if (string.IsNullOrWhiteSpace(answer.RawAnswer))
        {
            throw new ArgumentException("Оскарження потребує введеного варіанта відповіді.", nameof(answerEventId));
        }

        await aliases.GetOrCreateAsync(learnerId, cancellationToken);
        var report = new ContentReport
        {
            LearnerId = learnerId, EntryId = answer.EntryId, VersionId = answer.VersionId, AnswerEventId = answer.Id,
            Origin = "answer_dispute", ProposedValue = answer.RawAnswer,
            Status = "in_editor_queue", CreatedAt = clock.GetCurrentInstant()
        };
        db.ContentReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        return report.Id;
    }

    private Task<ContentReport?> FindOpenAsync(Guid learnerId, string key, CancellationToken cancellationToken) =>
        db.ContentReports.AsNoTracking().SingleOrDefaultAsync(report => report.LearnerId == learnerId
            && report.DedupKey == key && report.ClosedAt == null, cancellationToken);

    private static ContentReportResult Accepted(ContentReport report) => new(new ContentReportAccepted(report.Id, report.Status), 202, null);
    private static ContentReportResult NotOnboarded() => new(null, 404, new ContentReportError("learner.not_onboarded", "Спершу налаштуйте тренер."));
}

internal sealed record ContentReportResult(ContentReportAccepted? Report, int StatusCode, ContentReportError? Error);
