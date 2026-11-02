using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Features.Content.References;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Content.Verification;

/// <summary>Стійка черга PostgreSQL: окреме захоплення та атомарне застосування результату.</summary>
public sealed class ContentVerificationWorker(AppDbContext db, ContentPublicationService publication,
    IEnumerable<IReferenceSource> sources, IClock clock, ILogger<ContentVerificationWorker> logger)
{
    public const int BatchSize = 32;
    public const int MaximumAttempts = 3;
    public static readonly Duration ClaimLifetime = Duration.FromMinutes(2);
    private static readonly Meter Meter = new("ConstantLearning.Content.Verification");
    private static readonly Counter<long> Transitions = Meter.CreateCounter<long>("content.verification.transitions");
    private readonly IReferenceSource[] _sources = sources.ToArray();

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        for (var index = 0; index < BatchSize && await ProcessNextAsync(cancellationToken); index++) { }
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var claim = await ClaimAsync(cancellationToken);
        if (claim is null) return false;
        try
        {
            var report = await db.ContentReports.AsNoTracking().SingleAsync(value => value.Id == claim.ReportId, cancellationToken);
            VerificationDecision decision = new(null, "report_not_eligible");
            if (VerificationPolicy.IsEligible(report))
            {
                var entry = await db.DictionaryEntries.AsNoTracking().SingleAsync(value => value.Id == report.EntryId, cancellationToken);
                var version = await db.EntryVersions.AsNoTracking().SingleAsync(value => value.Id == report.VersionId, cancellationToken);
                var reason = entry.PublishedVersionId != version.Id || version.ValidationStatus != "approved"
                    ? "published_version_changed" : await ValidateAsync(version, entry, report, cancellationToken);
                if (reason is not null) decision = new(null, reason);
                else
                {
                    var results = new List<(string Source, ReferenceResult Result)>();
                    var applicable = _sources.Where(source => report.Field == "pronunciation"
                        ? source.Name is "wikislownik" or "wsjp"
                        : source.Name == (entry.Domain == "legalization" ? "government" : "wikislownik"));
                    // Загальний бюджет джерел коротший за lease; жодного HTTP всередині транзакції публікації.
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    foreach (var source in applicable)
                        results.Add((source.Name, await source.LookupAsync(new(version.LemmaPl,
                            report.Field == "pronunciation" ? ReferenceFields.Transcription : ReferenceFields.Translation), timeout.Token)));
                    decision = VerificationPolicy.Decide(report, entry, results, clock.GetCurrentInstant());
                }
            }
            await CompleteAsync(claim, decision, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Під час зупинки залишаємо durable running; після lease його підхопить наступний worker.
            db.ChangeTracker.Clear();
            throw;
        }
        catch (Exception exception)
        {
            db.ChangeTracker.Clear();
            var reason = exception is OperationCanceledException ? "source_timeout"
                : exception is HttpRequestException or IOException ? "source_unavailable" : "worker_failure";
            // Не записуємо текст зовнішнього винятку, URL чи запропоноване learner значення.
            logger.LogWarning("Звірка {JobId}: {Reason}, тип {FailureType}", claim.JobId, reason, exception.GetType().Name);
            await CompleteAsync(claim, new(null, reason, true), cancellationToken);
        }
        return true;
    }

    private async Task<Claim?> ClaimAsync(CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var now = clock.GetCurrentInstant();
        var expired = now - ClaimLifetime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var jobs = await db.VerificationJobs.FromSqlInterpolated($"""
            SELECT *, xmin FROM verification_jobs
            WHERE (status = 'pending' AND next_attempt_at <= {now})
               OR (status = 'running' AND (claimed_at IS NULL OR claimed_at <= {expired}))
            ORDER BY next_attempt_at, id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        if (jobs.SingleOrDefault() is not { } job) return null;
        if (job.Attempts >= MaximumAttempts)
        {
            var report = await LockedReportAsync(job.ContentReportId, cancellationToken);
            QueueEditor(job, report, "attempts_exhausted_after_restart", true);
        }
        else
        {
            job.Status = "running";
            job.Attempts++;
            // PostgreSQL зберігає мікросекунди; fence має пережити округлення Instant у БД.
            job.ClaimedAt = Instant.FromUnixTimeTicks(now.ToUnixTimeTicks() / 10 * 10);
            job.CompletedAt = null;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        Log(job);
        var claim = new Claim(job.Id, job.ContentReportId, job.Attempts, job.ClaimedAt);
        db.ChangeTracker.Clear();
        return claim;
    }

    private async Task CompleteAsync(Claim claim, VerificationDecision decision, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var jobs = await db.VerificationJobs.FromSqlInterpolated(
                $"SELECT *, xmin FROM verification_jobs WHERE id = {claim.JobId} FOR UPDATE").ToListAsync(cancellationToken);
            var job = jobs.Single();
            // Спроба і timestamp — fence: старий worker не завершує задачу нового власника lease.
            if (job.Status != "running" || job.Attempts != claim.Attempt || job.ClaimedAt != claim.ClaimedAt) return;
            if (decision.Evidence is not null) await publication.AcquireLockAsync(cancellationToken);
            var report = await LockedReportAsync(job.ContentReportId, cancellationToken);
            if (report.Status != "pending_verification") Finish(job, "done", "report_already_processed");
            else if (decision.Retry && job.Attempts < MaximumAttempts)
            {
                job.Status = "pending";
                job.LastError = decision.Reason;
                job.NextAttemptAt = clock.GetCurrentInstant() + Duration.FromSeconds(30 * (1 << (job.Attempts - 1)));
                job.ClaimedAt = null;
            }
            else if (decision.Evidence is null) QueueEditor(job, report, decision.Reason!, decision.Retry);
            else await PublishAsync(job, report, decision.Evidence, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            Log(job);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally { db.ChangeTracker.Clear(); }
    }

    private async Task PublishAsync(VerificationJob job, ContentReport report, ReferenceCandidate evidence,
        CancellationToken cancellationToken)
    {
        var entry = await db.DictionaryEntries.SingleAsync(value => value.Id == report.EntryId, cancellationToken);
        var previous = await db.EntryVersions.SingleAsync(value => value.Id == report.VersionId, cancellationToken);
        var reason = entry.PublishedVersionId != previous.Id || previous.ValidationStatus != "approved"
            ? "published_version_changed" : await ValidateAsync(previous, entry, report, cancellationToken);
        if (reason is not null) { QueueEditor(job, report, reason, false); return; }
        var now = clock.GetCurrentInstant();
        var versionCreatedAt = Instant.FromUnixTimeTicks(now.ToUnixTimeTicks() / 10 * 10);
        var proposed = report.ProposedValue!.Trim().Normalize();
        var next = new EntryVersion
        {
            EntryId = entry.Id,
            BasedOnVersionId = previous.Id,
            LemmaPl = previous.LemmaPl,
            PartOfSpeech = previous.PartOfSpeech,
            Gender = previous.Gender,
            GenderPairId = previous.GenderPairId,
            AspectPairId = previous.AspectPairId,
            PronunciationUk = report.Field == "pronunciation" ? proposed : previous.PronunciationUk,
            ExamplePl = previous.ExamplePl,
            ExampleUk = previous.ExampleUk,
            ShowContextBefore = previous.ShowContextBefore,
            FrequencyRank = previous.FrequencyRank,
            UtilityRank = previous.UtilityRank,
            LegalizationStage = previous.LegalizationStage,
            Topic = previous.Topic,
            ValidationStatus = "approved",
            CreatedByKind = "auto_verification",
            ContentReportId = report.Id,
            ReviewNote = previous.ReviewNote,
            CreatedAt = versionCreatedAt > previous.CreatedAt ? versionCreatedAt : previous.CreatedAt + Duration.FromMilliseconds(1),
            ContentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new { previous.ContentHash, report.Field, Value = proposed }))))
        };
        db.EntryVersions.Add(next);
        var translations = await db.VersionTranslations.AsNoTracking().Where(value => value.VersionId == previous.Id).ToListAsync(cancellationToken);
        foreach (var value in translations)
            db.VersionTranslations.Add(new VersionTranslation
            {
                VersionId = next.Id,
                TextUk = report.Field == "translation" && value.Kind == "canonical" ? proposed : value.TextUk,
                Kind = value.Kind,
                Position = value.Position,
                CreatedAt = now
            });
        var answers = await db.VersionAcceptedAnswers.AsNoTracking().Where(value => value.VersionId == previous.Id).ToListAsync(cancellationToken);
        foreach (var answer in answers)
            db.VersionAcceptedAnswers.Add(new VersionAcceptedAnswer
            {
                VersionId = next.Id,
                TextPl = answer.TextPl,
                IsCanonical = answer.IsCanonical,
                CreatedAt = now
            });
        var proofs = await db.ValidationEvidence.AsNoTracking().Where(value => value.VersionId == previous.Id).ToListAsync(cancellationToken);
        foreach (var proof in proofs)
            db.ValidationEvidence.Add(new ValidationEvidence
            {
                VersionId = next.Id,
                Field = proof.Field,
                Source = proof.Source,
                Url = proof.Url,
                RevisionId = proof.RevisionId,
                Attribution = proof.Attribution,
                Outcome = proof.Outcome,
                RetrievedAt = proof.RetrievedAt,
                CreatedAt = now
            });
        db.ValidationEvidence.Add(new ValidationEvidence
        {
            VersionId = next.Id,
            Field = report.Field == "translation" ? "translation_uk" : "pronunciation_uk",
            Source = evidence.Source,
            Url = evidence.Url,
            RevisionId = evidence.RevisionId,
            Attribution = evidence.Attribution,
            Outcome = "confirmed",
            RetrievedAt = evidence.SnapshotAt,
            CreatedAt = now
        });
        await publication.ApplyAsync([new(entry.Id, previous.Id, next.Id, "auto_verification")], cancellationToken);
        report.Status = "closed";
        report.Outcome = "applied";
        report.ResultingVersionId = next.Id;
        report.ClosedAt = now;
        Finish(job, "done", null);
    }

    private async Task<string?> ValidateAsync(EntryVersion version, DictionaryEntry entry, ContentReport report,
        CancellationToken cancellationToken) => VerificationPolicy.Validate(version, entry, report,
        await db.VersionTranslations.AsNoTracking().Where(value => value.VersionId == version.Id).ToListAsync(cancellationToken),
        await db.VersionAcceptedAnswers.AsNoTracking().Where(value => value.VersionId == version.Id).ToListAsync(cancellationToken),
        await db.ValidationEvidence.AsNoTracking().Where(value => value.VersionId == version.Id).ToListAsync(cancellationToken));

    private async Task<ContentReport> LockedReportAsync(Guid id, CancellationToken cancellationToken) =>
        (await db.ContentReports.FromSqlInterpolated($"SELECT *, xmin FROM content_reports WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken)).Single();

    private void QueueEditor(VerificationJob job, ContentReport report, string reason, bool dead)
    {
        if (report.Status == "pending_verification") report.Status = "in_editor_queue";
        Finish(job, dead ? "dead" : "done", reason);
    }

    private void Finish(VerificationJob job, string status, string? reason)
    {
        job.Status = status;
        job.LastError = reason;
        job.CompletedAt = clock.GetCurrentInstant();
    }

    private void Log(VerificationJob job)
    {
        Transitions.Add(1, new KeyValuePair<string, object?>("status", job.Status));
        logger.LogInformation("Звірка {JobId}, report {ReportId}: {Status}, спроб {Attempts}, причина {Reason}",
            job.Id, job.ContentReportId, job.Status, job.Attempts, job.LastError);
    }

    private sealed record Claim(Guid JobId, Guid ReportId, short Attempt, Instant? ClaimedAt);
}
