using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Import;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Features.Content.References;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Features.Content.Verification;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Content.Import;
using ConstantLearning.Tests.Content.References;
using ConstantLearning.Tests.Content.Reports;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Text;
using Quartz;

namespace ConstantLearning.Tests.Content.Verification;

public sealed class ContentVerificationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const long TelegramId = 900001234;

    [DockerFact]
    public async Task Ac35Ac42_RealSnapshotAndAuthenticatedReport_PublishImmutableVersionAndPreserveProgress()
    {
        var root = await SeedAsync(createJob: false);
        await ImportReferenceAsync();
        var state = new LearnerWord
        {
            LearnerId = root.Learner.Id,
            EntryId = root.Entry.Id,
            Status = "recalled",
            ModelState = "{}",
            SchedulerVersion = "test-v1",
            IntervalDays = 7,
            ConsecutiveScheduledCorrect = 2,
            DueOn = new LocalDate(2026, 11, 5)
        };
        var answer = new AnswerEvent
        {
            LearnerId = root.Learner.Id,
            EntryId = root.Entry.Id,
            VersionId = root.Version.Id,
            ExerciseKind = "review",
            Outcome = "correct",
            AnsweredAt = Clock.GetCurrentInstant(),
            LearnerDay = new LocalDate(2026, 10, 31),
            TimeZone = "Europe/Warsaw",
            SchedulerVersion = "test-v1",
            StateBefore = "{}"
        };
        Db.AddRange(state, answer);
        await Db.SaveChangesAsync();
        await using var app = await ReportsTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        using var request = Authorized(HttpMethod.Post, "/api/v1/content-reports");
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            origin = "card",
            entry_id = root.Entry.Id,
            field = "translation",
            proposed_value = "ропуха"
        }), Encoding.UTF8, "application/json");
        using var response = await app.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await ContractAssert.ResponseAsync("createContentReport", response);
        var worker = Worker(Db, [new WikislownikReferenceSource(Db, Clock)]);
        Assert.True(await worker.ProcessNextAsync());
        Assert.False(await worker.ProcessNextAsync());
        var report = await Db.ContentReports.AsNoTracking().SingleAsync();
        var next = await Db.EntryVersions.AsNoTracking().SingleAsync(value => value.Id == report.ResultingVersionId);
        Assert.Equal("closed", report.Status);
        Assert.Equal("applied", report.Outcome);
        Assert.Equal(root.Version.Id, next.BasedOnVersionId);
        Assert.Equal(root.Entry.Id, next.EntryId);
        Assert.Equal(root.Version.Id, report.VersionId);
        Assert.Equal("auto_verification", next.CreatedByKind);
        Assert.Equal(report.Id, next.ContentReportId);
        Assert.NotEqual(root.Version.ContentHash, next.ContentHash);
        next.Should().BeEquivalentTo(root.Version, options => options.Excluding(value => value.Id)
            .Excluding(value => value.BasedOnVersionId).Excluding(value => value.CreatedByKind)
            .Excluding(value => value.ContentReportId).Excluding(value => value.ContentHash)
            .Excluding(value => value.CreatedAt).Excluding(value => value.Version)
            .Excluding(value => value.Entry).Excluding(value => value.BasedOnVersion));
        (await Db.EntryVersions.AsNoTracking().SingleAsync(value => value.Id == root.Version.Id)).Should()
            .BeEquivalentTo(root.Version, options => options.Excluding(value => value.Entry).Excluding(value => value.BasedOnVersion));
        Assert.Equal(new[] { "ропуха", "жабка" }, await Db.VersionTranslations.Where(value => value.VersionId == next.Id)
            .OrderBy(value => value.Position).Select(value => value.TextUk).ToArrayAsync());
        Assert.Equal(new[] { "żaba", "żabka" }, await Db.VersionAcceptedAnswers.Where(value => value.VersionId == next.Id)
            .OrderBy(value => value.TextPl).Select(value => value.TextPl).ToArrayAsync());
        (await Db.LearnerWords.AsNoTracking().SingleAsync()).Should().BeEquivalentTo(state);
        (await Db.AnswerEvents.AsNoTracking().SingleAsync()).Should().BeEquivalentTo(answer);
        var proof = await Db.ValidationEvidence.SingleAsync(value => value.VersionId == next.Id && value.Source == "wikislownik");
        Assert.Equal("12345", proof.RevisionId);
        Assert.Equal(Clock.GetCurrentInstant(), proof.RetrievedAt);
        Assert.Contains("CC BY-SA", proof.Attribution);
        Assert.Equal(await Db.ValidationEvidence.CountAsync(value => value.VersionId == root.Version.Id) + 1,
            await Db.ValidationEvidence.CountAsync(value => value.VersionId == next.Id));
        Assert.Equal(next.Id, (await Db.DictionaryEntries.AsNoTracking().SingleAsync()).PublishedVersionId);
        Assert.Equal(2, await Db.PublicationLog.CountAsync());
        Assert.True(await Db.SearchForms.AnyAsync(value => value.VersionId == next.Id && value.FormDisplay == "ропуха"));
        Assert.False(await Db.SearchForms.AnyAsync(value => value.VersionId == root.Version.Id));
        await AssertJobAsync("done", 1);
        using var cardRequest = Authorized(HttpMethod.Get, $"/api/v1/dictionary/entries/{root.Entry.Id}");
        using var card = await app.Client.SendAsync(cardRequest);
        Assert.Equal(HttpStatusCode.OK, card.StatusCode);
        await ContractAssert.ResponseAsync("getDictionaryEntry", card);
        Assert.Contains("ропуха", await card.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task SubMicrosecondClock_ClaimOwnerCanCompletePublication()
    {
        var root = await SeedAsync();
        Clock.Advance(Duration.FromNanoseconds(789));
        Assert.True(await Worker(Db, [Confirmed("ропуха")]).ProcessNextAsync());
        await AssertJobAsync("done", 1);
        Assert.Equal("closed", (await Db.ContentReports.SingleAsync()).Status);
        Assert.Equal(2, await Db.EntryVersions.CountAsync());
        Assert.True((await Db.EntryVersions.SingleAsync(value => value.Id != root.Version.Id)).CreatedAt > root.Version.CreatedAt);
    }

    [DockerFact]
    public async Task UnavailableSource_RetriesWithBackoffAndDeadLettersAfterThreeAttempts()
    {
        await SeedAsync();
        var source = new StubSource("wikislownik", ReferenceResult.Manual("source_unavailable"));
        var worker = Worker(Db, [source]);
        await worker.TickAsync();
        await AssertJobAsync("pending", 1, "source_unavailable");
        Assert.Equal(Clock.GetCurrentInstant() + Duration.FromSeconds(30), (await Db.VerificationJobs.SingleAsync()).NextAttemptAt);
        Assert.False(await worker.ProcessNextAsync());
        Clock.Advance(Duration.FromSeconds(30));
        Assert.True(await worker.ProcessNextAsync());
        await AssertJobAsync("pending", 2, "source_unavailable");
        Assert.Equal(Clock.GetCurrentInstant() + Duration.FromSeconds(60), (await Db.VerificationJobs.SingleAsync()).NextAttemptAt);
        Clock.Advance(Duration.FromSeconds(59));
        Assert.False(await worker.ProcessNextAsync());
        Clock.Advance(Duration.FromSeconds(1));
        await worker.TickAsync();
        await AssertJobAsync("dead", 3, "source_unavailable");
        Assert.Equal(3, source.Calls);
        Assert.Equal("in_editor_queue", (await Db.ContentReports.SingleAsync()).Status);
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
        Assert.Null((await Db.ContentReports.SingleAsync()).ClosedAt);
    }

    [DockerFact]
    public async Task ProjectionFailure_RollsBackVersionEvidencePublicationAndReport_ThenRecovers()
    {
        var root = await SeedAsync();
        var source = Confirmed("ропуха");
        var forms = await Db.SearchForms.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync();
        var proofCount = await Db.ValidationEvidence.CountAsync();
        var broken = new FailingProjection(new SearchFormsProjection(Db, Clock));
        await Worker(Db, [source], broken).ProcessNextAsync();
        await AssertJobAsync("pending", 1, "worker_failure");
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
        Assert.Equal(1, await Db.PublicationLog.CountAsync());
        Assert.Equal(proofCount, await Db.ValidationEvidence.CountAsync());
        Assert.Equal(root.Version.Id, (await Db.DictionaryEntries.SingleAsync()).PublishedVersionId);
        Assert.Equal("pending_verification", (await Db.ContentReports.SingleAsync()).Status);
        (await Db.SearchForms.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync()).Should().BeEquivalentTo(forms);
        Clock.Advance(Duration.FromSeconds(30));
        await Worker(Db, [source]).ProcessNextAsync();
        await AssertJobAsync("done", 2);
        Assert.Equal(2, await Db.EntryVersions.CountAsync());
    }

    [DockerFact]
    public async Task AbandonedClaim_IsRecovered_AndOldOwnerCannotCommit()
    {
        await SeedAsync();
        await using var provider = _postgres.CreateServices(Clock);
        await using var scope = provider.CreateAsyncScope();
        var firstDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var paused = new PausedSource(ConfirmedResult("ропуха"));
        var first = Worker(firstDb, [paused]).ProcessNextAsync();
        await paused.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(await Worker(Db, [Confirmed("ропуха")]).ProcessNextAsync());
        Clock.Advance(ContentVerificationWorker.ClaimLifetime);
        Assert.True(await Worker(Db, [Confirmed("ропуха")]).ProcessNextAsync());
        paused.Release.SetResult();
        Assert.True(await first);
        await AssertJobAsync("done", 2);
        Assert.Equal(2, await Db.EntryVersions.CountAsync());
        Assert.Equal(2, await Db.PublicationLog.CountAsync());
    }

    [DockerFact]
    public async Task SkipLocked_ProcessesAnotherJobWithoutWaiting()
    {
        var root = await SeedAsync();
        var second = AddJob(root, "pronunciation", "жа́ба");
        await Db.SaveChangesAsync();
        await using var provider = _postgres.CreateServices(Clock);
        await using var scope = provider.CreateAsyncScope();
        var lockedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await lockedDb.Database.BeginTransactionAsync();
        await lockedDb.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM verification_jobs WHERE id = {root.Job!.Id} FOR UPDATE");
        Assert.True(await Worker(Db, [Confirmed("жа́ба")]).ProcessNextAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("pending", (await Db.VerificationJobs.AsNoTracking().SingleAsync(value => value.Id == root.Job.Id)).Status);
        Assert.Equal("done", (await Db.VerificationJobs.AsNoTracking().SingleAsync(value => value.Id == second.Id)).Status);
        await transaction.RollbackAsync();
    }

    [DockerFact]
    public async Task ThirdAbandonedAttempt_DeadLettersWithoutFourthSourceCall()
    {
        await SeedAsync();
        await Db.VerificationJobs.ExecuteUpdateAsync(update => update.SetProperty(value => value.Status, "running")
            .SetProperty(value => value.Attempts, (short)3).SetProperty(value => value.ClaimedAt, Clock.GetCurrentInstant() - ContentVerificationWorker.ClaimLifetime));
        var source = Confirmed("ропуха");
        await Worker(Db, [source]).TickAsync();
        await AssertJobAsync("dead", 3, "attempts_exhausted_after_restart");
        Assert.Equal(0, source.Calls);
    }

    [DockerFact]
    public async Task CompletedJob_RequeuedWithClosedReport_DoesNotPublishAgain()
    {
        await SeedAsync();
        var worker = Worker(Db, [Confirmed("ропуха")]);
        await worker.ProcessNextAsync();
        await Db.VerificationJobs.ExecuteUpdateAsync(update => update.SetProperty(value => value.Status, "pending"));
        Assert.True(await worker.ProcessNextAsync());
        Assert.Equal(2, await Db.EntryVersions.CountAsync());
        Assert.Equal(2, await Db.PublicationLog.CountAsync());
        await AssertJobAsync("done", 2, "report_already_processed");
    }

    [DockerFact]
    public async Task Shutdown_LeavesDurableClaimForRecoveryAfterLease()
    {
        await SeedAsync();
        using var stop = new CancellationTokenSource();
        var source = new PausedSource(ConfirmedResult("ропуха"));
        var running = Worker(Db, [source]).ProcessNextAsync(stop.Token);
        await source.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        await AssertJobAsync("running", 1);
        Assert.Equal("pending_verification", (await Db.ContentReports.SingleAsync()).Status);
        Clock.Advance(ContentVerificationWorker.ClaimLifetime);
        Assert.True(await Worker(Db, [Confirmed("ропуха")]).ProcessNextAsync());
        await AssertJobAsync("done", 2);
    }

    [DockerFact]
    public async Task ConfirmedPronunciation_PublishesWithoutChangingTranslations()
    {
        var root = await SeedAsync(field: "pronunciation", proposed: "жа́ба");
        await Worker(Db, [Confirmed("жа́ба")]).TickAsync();
        await AssertJobAsync("done", 1);
        var report = await Db.ContentReports.SingleAsync();
        var next = await Db.EntryVersions.SingleAsync(value => value.Id == report.ResultingVersionId);
        Assert.Equal("жа́ба", next.PronunciationUk);
        Assert.Equal("жаба", (await Db.EntryVersions.SingleAsync(value => value.Id == root.Version.Id)).PronunciationUk);
        Assert.Equal(new[] { "жаба", "жабка" }, await Db.VersionTranslations.Where(value => value.VersionId == next.Id)
            .OrderBy(value => value.Position).Select(value => value.TextUk).ToArrayAsync());
        Assert.True(await Db.ValidationEvidence.AnyAsync(value => value.VersionId == next.Id
            && value.Field == "pronunciation_uk" && value.Source == "wikislownik"));
    }

    [DockerFact]
    public async Task InvalidProposal_IsQueuedWithoutLookingUpSource()
    {
        await SeedAsync(proposed: "translation");
        var source = Confirmed("translation");
        await Worker(Db, [source]).TickAsync();
        await AssertJobAsync("done", 1, "invalid_proposed_language");
        Assert.Equal(0, source.Calls);
        Assert.Equal("in_editor_queue", (await Db.ContentReports.SingleAsync()).Status);
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
    }

    [DockerFact]
    public async Task AvailableCandidateWithoutRevision_DoesNotCreateEvidenceOrPublish()
    {
        await SeedAsync();
        var evidenceCount = await Db.ValidationEvidence.CountAsync();
        var source = new StubSource("wikislownik", new(ReferenceStatus.Available,
            [Candidate("ропуха") with { RevisionId = null }], null));
        await Worker(Db, [source]).TickAsync();
        await AssertJobAsync("done", 1, "value_not_confirmed");
        Assert.Equal("in_editor_queue", (await Db.ContentReports.SingleAsync()).Status);
        Assert.Equal(evidenceCount, await Db.ValidationEvidence.CountAsync());
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
    }

    [DockerFact]
    public async Task LegalizationWithoutGovernmentConfirmation_CannotUseDictionaryInstead()
    {
        await SeedAsync(legalization: true);
        var source = Confirmed("ропуха");
        await Worker(Db, [source, new GovernmentReferenceSource(new ReferenceSourcesOptions(), Clock)]).TickAsync();
        await AssertJobAsync("done", 1, "permitted_government_reference_missing");
        Assert.Equal(0, source.Calls);
        Assert.Equal("in_editor_queue", (await Db.ContentReports.SingleAsync()).Status);
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
    }

    [DockerFact]
    public async Task PronunciationConflict_WithManualCandidate_GoesToEditor()
    {
        await SeedAsync(field: "pronunciation", proposed: "жа́ба");
        var conflicting = new StubSource("wsjp", ReferenceResult.Manual("wsjp_requires_editor",
            [Candidate("жаба", "wsjp", "https://wsjp.pl/haslo/zaba")]));
        await Worker(Db, [Confirmed("жа́ба"), conflicting]).TickAsync();
        await AssertJobAsync("done", 1, "source_conflict");
        Assert.Equal("in_editor_queue", (await Db.ContentReports.SingleAsync()).Status);
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
    }

    [DockerFact]
    public async Task TranslationWithContext_DoesNotCallSourceOrChangeExamples()
    {
        await SeedAsync(context: true);
        var source = Confirmed("ропуха");
        await Worker(Db, [source]).TickAsync();
        await AssertJobAsync("done", 1, "context_requires_editor");
        Assert.Equal(0, source.Calls);
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
    }

    [DockerFact]
    public async Task Legalization_RequiresGovernmentSource_AndCanUseReviewedOfficialReference()
    {
        var root = await SeedAsync(legalization: true);
        var dictionary = Confirmed("ропуха");
        var options = new ReferenceSourcesOptions();
        options.GovernmentReferences.Add(new("żaba", "ропуха", new Uri("https://www.gov.pl/web/ua/test"),
            Clock.GetCurrentInstant(), "Урядова сторінка українською; редакторське зіставлення."));
        await Worker(Db, [dictionary, new GovernmentReferenceSource(options, Clock)]).TickAsync();
        await AssertJobAsync("done", 1);
        Assert.Equal(0, dictionary.Calls);
        Assert.NotEqual(root.Version.Id, (await Db.DictionaryEntries.SingleAsync()).PublishedVersionId);
        var proof = await Db.ValidationEvidence.SingleAsync(value => value.Source == "government");
        Assert.Null(proof.RevisionId);
        Assert.Equal("https://www.gov.pl/web/ua/test", proof.Url);
    }

    [DockerFact]
    public async Task MissingRevision_RemainsManual()
    {
        await SeedAsync();
        var source = new StubSource("wikislownik", ReferenceResult.Manual("revision_missing", [Candidate("ропуха") with { RevisionId = null }]));
        await Worker(Db, [source]).TickAsync();
        await AssertJobAsync("done", 1, "revision_missing");
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
    }

    [DockerFact]
    public async Task PublishedVersionChangesDuringLookup_LeavesNewPublicationUntouched()
    {
        var root = await SeedAsync();
        await using var provider = _postgres.CreateServices(Clock);
        await using var scope = provider.CreateAsyncScope();
        var workerDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var paused = new PausedSource(ConfirmedResult("ропуха"));
        var task = Worker(workerDb, [paused]).ProcessNextAsync();
        await paused.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await new ContentPublicationService(Db, new SearchFormsProjection(Db, Clock), Clock)
            .ApplyAsync([new(root.Entry.Id, root.Version.Id, null, "editor", Action: "unpublish")]);
        paused.Release.SetResult();
        await task;
        Db.ChangeTracker.Clear();
        await AssertJobAsync("done", 1, "published_version_changed");
        Assert.Null((await Db.DictionaryEntries.SingleAsync()).PublishedVersionId);
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
    }

    [DockerFact]
    public async Task QuartzRegistration_ResolvesScopedWorkerAndThirtySecondTrigger()
    {
        await using var app = await ReportsTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ContentVerificationWorker>());
        var scheduler = await app.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        var detail = await scheduler.GetJobDetail(new JobKey("ContentVerification"));
        Assert.Equal(typeof(ContentVerificationJob), detail!.JobType);
        Assert.True(detail.ConcurrentExecutionDisallowed);
        var trigger = Assert.IsAssignableFrom<ISimpleTrigger>(await scheduler.GetTrigger(new TriggerKey("ContentVerification-trigger")));
        Assert.Equal(TimeSpan.FromSeconds(30), trigger.RepeatInterval);
        await scheduler.Shutdown();
    }

    private ContentVerificationWorker Worker(AppDbContext db, IEnumerable<IReferenceSource> sources, IPublicationProjection? projection = null) =>
        new(db, new ContentPublicationService(db, projection ?? new SearchFormsProjection(db, Clock), Clock), sources,
            Clock, NullLogger<ContentVerificationWorker>.Instance);

    private async Task<Root> SeedAsync(bool createJob = true, string field = "translation", string proposed = "ропуха",
        bool context = false, bool legalization = false)
    {
        using var bundle = new DictionaryImportTestBundle();
        bundle.Row["show_context_before"] = context ? "true" : "false";
        bundle.Row["accepted_answers_pl"] = "żaba;żabka";
        bundle.Evidence["accepted_answers_pl"] = new System.Text.Json.Nodes.JsonArray("żaba", "żabka");
        if (legalization)
        {
            bundle.Row["domain"] = "legalization";
            bundle.Row["utility_rank"] = "1";
            bundle.Row["legalization_stage"] = "application";
        }
        await bundle.WriteAsync();
        var publisher = new ContentPublicationService(Db, new SearchFormsProjection(Db, Clock), Clock);
        await new DictionaryImportService(Db, new DictionaryImportReader(), publisher, Clock,
            NullLogger<DictionaryImportService>.Instance).ImportAsync(bundle.DirectoryPath);
        var learner = new Learner
        {
            TelegramUserId = TelegramId,
            TimeZone = "Europe/Warsaw",
            DailyNewWordsLimit = 5,
            OnboardedAt = Clock.GetCurrentInstant(),
            CreatedAt = Clock.GetCurrentInstant(),
            TimeZoneConfirmed = true
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        var root = new Root(learner, await Db.DictionaryEntries.AsNoTracking().SingleAsync(),
            await Db.EntryVersions.AsNoTracking().SingleAsync(), null);
        if (createJob) root = root with { Job = AddJob(root, field, proposed) };
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return root;
    }

    private VerificationJob AddJob(Root root, string field, string proposed)
    {
        var report = new ContentReport
        {
            LearnerId = root.Learner.Id,
            EntryId = root.Entry.Id,
            VersionId = root.Version.Id,
            Origin = "card",
            Field = field,
            ProposedValue = proposed,
            Status = "pending_verification",
            CreatedAt = Clock.GetCurrentInstant()
        };
        var job = new VerificationJob { ContentReportId = report.Id, Status = "pending", NextAttemptAt = Clock.GetCurrentInstant(), CreatedAt = Clock.GetCurrentInstant() };
        Db.AddRange(report, job);
        return job;
    }

    private async Task ImportReferenceAsync()
    {
        await Db.ReferenceEntries.ExecuteDeleteAsync();
        var text = ReferenceFixtures.Read("supported").Replace("kot", "żaba", StringComparison.Ordinal)
            .Replace("kɔt", "ʐaba", StringComparison.Ordinal).Replace("[[кіт]]", "[[ропуха]]", StringComparison.Ordinal);
        await using var dump = ReferenceFixtures.Dump(("żaba", text, "12345"));
        var converter = new PolishIpaConverter();
        await new ReferenceSnapshotImporter(Db, new WikislownikParser(converter), converter, Clock)
            .ImportWikislownikDumpAsync(dump, Clock.GetCurrentInstant());
    }

    private async Task AssertJobAsync(string status, int attempts, string? reason = null)
    {
        var job = await Db.VerificationJobs.AsNoTracking().SingleAsync();
        Assert.Equal(status, job.Status);
        Assert.Equal(attempts, job.Attempts);
        Assert.Equal(reason, job.LastError);
        Assert.Equal(status is "done" or "dead", job.CompletedAt.HasValue);
        ContractAssert.EventRow("verification_jobs", JsonSerializer.Serialize(new
        {
            id = job.Id,
            content_report_id = job.ContentReportId,
            status = job.Status,
            attempts = job.Attempts,
            next_attempt_at = InstantPattern.ExtendedIso.Format(job.NextAttemptAt)
        }));
    }

    private HttpRequestMessage Authorized(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(TelegramId, Clock.GetCurrentInstant()));
        return request;
    }

    private StubSource Confirmed(string value) => new("wikislownik", ConfirmedResult(value));
    private ReferenceResult ConfirmedResult(string value) => new(ReferenceStatus.Available, [Candidate(value)], null);
    private ReferenceCandidate Candidate(string value, string source = "wikislownik", string url = "https://pl.wiktionary.org/wiki/%C5%BCaba") =>
        new(value, source, url, "12345", Clock.GetCurrentInstant(), "Автори Wikisłownik; CC BY-SA 4.0", "CC-BY-SA-4.0");

    private sealed class StubSource(string name, ReferenceResult result) : IReferenceSource
    {
        public string Name => name;
        public int Calls { get; private set; }
        public Task<ReferenceResult> LookupAsync(ReferenceQuery query, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class PausedSource(ReferenceResult result) : IReferenceSource
    {
        public string Name => "wikislownik";
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<ReferenceResult> LookupAsync(ReferenceQuery query, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class FailingProjection(IPublicationProjection inner) : IPublicationProjection
    {
        public async Task UpdateAsync(IReadOnlyList<PublicationChange> changes, CancellationToken cancellationToken)
        {
            await inner.UpdateAsync(changes, cancellationToken);
            throw new InvalidOperationException("Тестовий збій після оновлення пошукових форм.");
        }
    }

    private sealed record Root(Learner Learner, DictionaryEntry Entry, EntryVersion Version, VerificationJob? Job);
}
