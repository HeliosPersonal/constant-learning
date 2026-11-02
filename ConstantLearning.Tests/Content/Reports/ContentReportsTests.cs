using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Reports;
using ConstantLearning.Features.Identity.Aliases;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using NodaTime.Text;

namespace ConstantLearning.Tests.Content.Reports;

public sealed class ContentReportsTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const string Path = "/api/v1/content-reports";
    private const long TelegramUserId = 900000001;

    [DockerFact]
    public async Task Ac34_TranslationReportAndVerificationJob_AreAtomicAndContractValid_WithoutChangingCardOrProgress()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        using var response = await PostAsync(app, Card(root, proposed: "державна установа", comment: "Тут краще стандартний відповідник."));
        await AssertAsync(response, HttpStatusCode.Accepted);
        var body = await BodyAsync(response);
        var report = await Db.ContentReports.AsNoTracking().SingleAsync();
        Assert.Equal(report.Id, body["id"]!.GetValue<Guid>());
        Assert.Equal("pending_verification", body["status"]!.GetValue<string>());
        Assert.Equal("Дякуємо, передали на перевірку", body["message"]!.GetValue<string>());
        Assert.Equal(root.Learner.Id, report.LearnerId);
        Assert.Equal(root.Entry.Id, report.EntryId);
        Assert.Equal(root.Version.Id, report.VersionId);
        Assert.Equal("translation", report.Field);
        Assert.Equal("державна установа", report.ProposedValue);
        Assert.Equal("Тут краще стандартний відповідник.", report.Comment);
        Assert.Equal(Clock.GetCurrentInstant(), report.CreatedAt);
        Assert.Matches("^[0-9a-f]{64}$", report.DedupKey!);
        var job = await Db.VerificationJobs.AsNoTracking().SingleAsync();
        Assert.Equal(report.Id, job.ContentReportId);
        Assert.Equal("pending", job.Status);
        Assert.Equal(0, job.Attempts);
        Assert.Equal(Clock.GetCurrentInstant(), job.NextAttemptAt);
        ContractAssert.EventRow("verification_jobs", JsonSerializer.Serialize(new
        {
            id = job.Id, content_report_id = job.ContentReportId, status = job.Status,
            attempts = job.Attempts, next_attempt_at = InstantPattern.ExtendedIso.Format(job.NextAttemptAt)
        }));
        Assert.Equal(root.Version.Id, (await Db.DictionaryEntries.AsNoTracking().SingleAsync()).PublishedVersionId);
        Assert.Equal(root.Version.Version, (await Db.EntryVersions.AsNoTracking().SingleAsync()).Version);
        Assert.Empty(await Db.PublicationLog.ToListAsync());
        Assert.Empty(await Db.LearnerWords.ToListAsync());
        Assert.Empty(await Db.AnswerEvents.ToListAsync());
        Assert.Empty(await Db.LearningSessions.ToListAsync());
        Assert.Matches("^R-[0-9A-F]{12}$", (await LearnerAsync()).ReporterAlias!);
    }

    [DockerFact]
    public async Task Ac36_RoutesOnlyTranslationAndPronunciationWithProposedValue_ToVerification()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        (string Field, string? Proposed, string Status)[] cases =
        [
            ("translation", "переклад", "pending_verification"),
            ("pronunciation", "транскрипція", "pending_verification"),
            ("lemma", "żabka", "in_editor_queue"),
            ("example", "To jest żabka.", "in_editor_queue"),
            ("translation", null, "in_editor_queue"),
            ("pronunciation", " \t ", "in_editor_queue")
        ];
        foreach (var (field, proposed, status) in cases)
        {
            using var response = await PostAsync(app, Card(root, field, proposed));
            await AssertAsync(response, HttpStatusCode.Accepted);
            Assert.Equal(status, (await BodyAsync(response))["status"]!.GetValue<string>());
        }

        using var missing = await PostAsync(app, Missing("нове слово", "Немає в словнику."));
        await AssertAsync(missing, HttpStatusCode.Accepted);
        Assert.Equal("in_editor_queue", (await BodyAsync(missing))["status"]!.GetValue<string>());
        var missingReport = await Db.ContentReports.AsNoTracking().SingleAsync(report => report.Origin == "missing_word");
        Assert.Null(missingReport.EntryId);
        Assert.Null(missingReport.VersionId);
        Assert.Equal("нове слово", missingReport.SearchQuery);
        Assert.Equal(7, await Db.ContentReports.CountAsync());
        Assert.Equal(2, await Db.VerificationJobs.CountAsync());
    }

    [DockerFact]
    public async Task Ac51_DeduplicatesNfcCaseAndWhitespace_PreservesDiacriticsAndFieldBoundaries()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        using var first = await PostAsync(app, Card(root, proposed: "  CAFE\u0301\t  РІЧКА ", comment: "  КОМЕНТАР\n тут "));
        using var repeat = await PostAsync(app, Card(root, proposed: "café річка", comment: "коментар\u00a0тут"));
        await AssertAsync(first, HttpStatusCode.Accepted);
        await AssertAsync(repeat, HttpStatusCode.Accepted);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await repeat.Content.ReadAsStringAsync());
        Assert.Single(await Db.ContentReports.ToListAsync());
        Assert.Single(await Db.VerificationJobs.ToListAsync());
        using var noAccent = await PostAsync(app, Card(root, proposed: "cafe річка", comment: "коментар тут"));
        using var differentFields = await PostAsync(app, Card(root, proposed: "café", comment: "річка коментар тут"));
        await AssertAsync(noAccent, HttpStatusCode.Accepted);
        await AssertAsync(differentFields, HttpStatusCode.Accepted);
        Assert.Equal(3, await Db.ContentReports.CountAsync());
        Assert.Equal(3, await Db.VerificationJobs.CountAsync());
    }

    [DockerFact]
    public async Task MissingWord_DeduplicatesOnlyNormalizedQuery_AndClosedHistoryDoesNotBlockNewReport()
    {
        await SeedAsync();
        await using var app = await StartAsync();
        using var original = await PostAsync(app, Missing("  NOWE\t słowo  ", "Перший коментар"));
        using var repeated = await PostAsync(app, Missing("nowe słowo", "Інший коментар"));
        await AssertAsync(original, HttpStatusCode.Accepted);
        await AssertAsync(repeated, HttpStatusCode.Accepted);
        Assert.Equal(await original.Content.ReadAsStringAsync(), await repeated.Content.ReadAsStringAsync());
        var id = (await BodyAsync(original))["id"]!.GetValue<Guid>();
        await Db.ContentReports.Where(report => report.Id == id).ExecuteUpdateAsync(update => update
            .SetProperty(report => report.Status, "closed").SetProperty(report => report.ClosedAt, Clock.GetCurrentInstant()));
        using var afterClosed = await PostAsync(app, Missing("nowe słowo"));
        await AssertAsync(afterClosed, HttpStatusCode.Accepted);
        Assert.NotEqual(id, (await BodyAsync(afterClosed))["id"]!.GetValue<Guid>());
        Assert.Equal(2, await Db.ContentReports.CountAsync());
    }

    [DockerFact]
    public async Task Ac46_TwentyAccepted_TwentyFirstRejected_DuplicateStillAcceptedBeforeQuota()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        using var first = await PostAsync(app, Card(root));
        await AssertAsync(first, HttpStatusCode.Accepted);
        for (var index = 1; index < 20; index++)
        {
            using var accepted = await PostAsync(app, Missing("слово " + index));
            await AssertAsync(accepted, HttpStatusCode.Accepted);
        }

        using var repeated = await PostAsync(app, Card(root, proposed: "  ПРАВИЛЬНИЙ\t переклад "));
        await AssertAsync(repeated, HttpStatusCode.Accepted);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await repeated.Content.ReadAsStringAsync());
        using var rejected = await PostAsync(app, Missing("двадцять перше слово", "Збережіть цей коментар у формі."));
        await AssertAsync(rejected, HttpStatusCode.TooManyRequests, "report.daily_limit_reached");
        Assert.Equal("Сьогоднішній ліміт 20 повідомлень вичерпано — нові можна надіслати після 00:00 за вашим часом.",
            (await BodyAsync(rejected))["message"]!.GetValue<string>());
        Assert.Equal(20, await Db.ContentReports.CountAsync());
        Assert.Single(await Db.VerificationJobs.ToListAsync());
        Assert.Empty(await Db.AnswerEvents.ToListAsync());
        Assert.Empty(await Db.LearningSessions.ToListAsync());
    }

    [DockerFact]
    public async Task ConcurrentIdenticalRequests_CreateOneReportAndOneJob_WithStableAlias()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => PostAsync(app, Card(root))));
        try
        {
            var ids = new List<Guid>();
            foreach (var response in responses)
            {
                await AssertAsync(response, HttpStatusCode.Accepted);
                ids.Add((await BodyAsync(response))["id"]!.GetValue<Guid>());
            }

            Assert.Single(ids.Distinct());
            Assert.Single(await Db.ContentReports.ToListAsync());
            Assert.Single(await Db.VerificationJobs.ToListAsync());
            var alias = (await LearnerAsync()).ReporterAlias;
            using var next = await PostAsync(app, Missing("інше слово"));
            await AssertAsync(next, HttpStatusCode.Accepted);
            Assert.Equal(alias, (await LearnerAsync()).ReporterAlias);
            Assert.Matches("^R-[0-9A-F]{12}$", alias!);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [DockerFact]
    public async Task ConcurrentDistinctRequests_AdmitExactlyTwenty_WithoutQuotaOverrun()
    {
        await SeedAsync();
        await using var app = await StartAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 24).Select(index => PostAsync(app, Missing("слово " + index))));
        try
        {
            Assert.Equal(20, responses.Count(response => response.StatusCode == HttpStatusCode.Accepted));
            Assert.Equal(4, responses.Count(response => response.StatusCode == HttpStatusCode.TooManyRequests));
            foreach (var response in responses) await ContractAssert.ResponseAsync("createContentReport", response);
            Assert.Equal(20, await Db.ContentReports.CountAsync());
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [DockerFact]
    public async Task FailedReportTransaction_RollsBackAliasSnapshotAndJob_AndRefundsQuota()
    {
        var root = await SeedAsync();
        var failure = new FailFirstVerificationWrite();
        await using var app = await StartAsync(services => services.AddDbContext<AppDbContext>(options => options.AddInterceptors(failure)));
        var failureResult = await Assert.ThrowsAsync<DbUpdateException>(() => PostAsync(app, Card(root)));
        Assert.IsType<ReportWriteFailure>(failureResult.InnerException);
        Assert.Empty(await Db.ContentReports.ToListAsync());
        Assert.Empty(await Db.VerificationJobs.ToListAsync());
        Assert.Null((await LearnerAsync()).ReporterAlias);
        Assert.Null((await LearnerAsync()).CurrentDayDate);
        using var first = await PostAsync(app, Card(root));
        await AssertAsync(first, HttpStatusCode.Accepted);
        for (var index = 1; index < 20; index++)
        {
            using var accepted = await PostAsync(app, Missing("слово " + index));
            await AssertAsync(accepted, HttpStatusCode.Accepted);
        }

        using var rejected = await PostAsync(app, Missing("нове понад ліміт"));
        await AssertAsync(rejected, HttpStatusCode.TooManyRequests, "report.daily_limit_reached");
        Assert.Equal(20, await Db.ContentReports.CountAsync());
        Assert.Single(await Db.VerificationJobs.ToListAsync());
    }

    [DockerFact]
    public async Task ExtendedLearnerDay_KeepsQuotaAcrossZoneChanges_AndResetsOnlyAtPersistedBoundary()
    {
        await SeedAsync();
        await using var app = await StartAsync();
        for (var index = 0; index < 20; index++)
        {
            using var accepted = await PostAsync(app, Missing("слово " + index));
            await AssertAsync(accepted, HttpStatusCode.Accepted);
        }

        using var changed = await SendAsync(app, HttpMethod.Patch, "/api/v1/learners/me/settings", """{"time_zone":"America/New_York"}""");
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Clock.Reset(Instant.FromUtc(2026, 1, 16, 0, 0));
        using var changedAgain = await SendAsync(app, HttpMethod.Patch, "/api/v1/learners/me/settings", """{"time_zone":"Asia/Tokyo"}""");
        Assert.Equal(HttpStatusCode.OK, changedAgain.StatusCode);
        using var duringExtension = await PostAsync(app, Missing("ще один запит"));
        await AssertAsync(duringExtension, HttpStatusCode.TooManyRequests, "report.daily_limit_reached");
        Assert.Equal(new LocalDate(2026, 1, 15), (await LearnerAsync()).CurrentDayDate);
        Assert.Equal(Instant.FromUtc(2026, 1, 16, 5, 0), (await LearnerAsync()).CurrentDayEndsAt);
        Clock.Reset(Instant.FromUtc(2026, 1, 16, 5, 0));
        using var newDay = await PostAsync(app, Missing("ще один запит"));
        await AssertAsync(newDay, HttpStatusCode.Accepted);
        Assert.Equal(21, await Db.ContentReports.CountAsync());
        Assert.Equal(new LocalDate(2026, 1, 16), (await LearnerAsync()).CurrentDayDate);
    }

    [DockerFact]
    public async Task AliasCollision_RollsBackSavepoint_RetriesAndPreservesOtherLearnersAlias()
    {
        await SeedAsync();
        var other = new Learner
        {
            TelegramUserId = TelegramUserId + 1, TimeZone = "Europe/Warsaw", DailyNewWordsLimit = 5,
            OnboardedAt = Clock.GetCurrentInstant(), ReporterAlias = "R-AAAAAAAAAAAA"
        };
        Db.Learners.Add(other);
        await Db.SaveChangesAsync();
        var generator = new CollisionGenerator();
        await using var app = await StartAsync(services => services.AddSingleton<IReporterAliasGenerator>(generator));
        using var response = await PostAsync(app, Missing("нове слово"));
        await AssertAsync(response, HttpStatusCode.Accepted);
        Assert.Equal(2, generator.Calls);
        Assert.Equal("R-BBBBBBBBBBBB", (await LearnerAsync()).ReporterAlias);
        Assert.Equal("R-AAAAAAAAAAAA", (await Db.Learners.AsNoTracking().SingleAsync(value => value.Id == other.Id)).ReporterAlias);
        using var repeat = await PostAsync(app, Missing("інше слово"));
        await AssertAsync(repeat, HttpStatusCode.Accepted);
        Assert.Equal(2, generator.Calls);
    }

    [DockerFact]
    public async Task InvalidBodies_RejectSpoofedOriginIdentityAndForeignAnswerFields_WithoutConsumingQuota()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        string[] invalid =
        [
            "{", "null", "[]", "{}", """{"origin":"answer_dispute"}""",
            """{"origin":"card"}""", """{"origin":"missing_word","search_query":" "}""",
            $$"""{"origin":"missing_word","entry_id":"{{root.Entry.Id}}","search_query":"слово"}""",
            $$"""{"origin":"card","entry_id":"{{root.Entry.Id}}","field":"other"}""",
            $$"""{"origin":"card","entry_id":"{{root.Entry.Id}}","field":"lemma","reporter_alias":"R-ATTACK"}""",
            $$"""{"origin":"card","entry_id":"{{root.Entry.Id}}","field":"lemma","learner_id":"{{Guid.NewGuid()}}"}""",
            $$"""{"origin":"card","entry_id":"{{root.Entry.Id}}","field":"lemma","answer_event_id":"{{Guid.NewGuid()}}"}""",
            """{"origin":"missing_word","origin":"card","search_query":"слово"}""",
            """{"origin":"missing_word","search_query":"\u0000"}""",
            JsonSerializer.Serialize(new { origin = "missing_word", search_query = new string('x', 201) }),
            JsonSerializer.Serialize(new { origin = "card", entry_id = root.Entry.Id, field = "lemma", proposed_value = new string('x', 501) }),
            JsonSerializer.Serialize(new { origin = "card", entry_id = root.Entry.Id, field = "lemma", comment = new string('x', 1001) })
        ];
        foreach (var json in invalid)
        {
            using var response = await PostAsync(app, json);
            await AssertAsync(response, HttpStatusCode.BadRequest);
        }

        Assert.Empty(await Db.ContentReports.ToListAsync());
        Assert.Empty(await Db.VerificationJobs.ToListAsync());
        Assert.Null((await LearnerAsync()).ReporterAlias);
        Assert.Null((await LearnerAsync()).CurrentDayDate);
    }

    [DockerFact]
    public async Task UnknownUnpublishedAndNonApprovedCards_ReturnSameContract404_WithoutCreatingAlias()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        using var unknown = await PostAsync(app, Card(root).Replace(root.Entry.Id.ToString(), Guid.NewGuid().ToString(), StringComparison.Ordinal));
        await AssertAsync(unknown, HttpStatusCode.NotFound, "dictionary.entry_not_found");
        await Db.DictionaryEntries.Where(value => value.Id == root.Entry.Id).ExecuteUpdateAsync(update => update.SetProperty(value => value.PublishedVersionId, (Guid?)null));
        using var unpublished = await PostAsync(app, Card(root));
        await AssertAsync(unpublished, HttpStatusCode.NotFound, "dictionary.entry_not_found");
        await Db.EntryVersions.Where(value => value.Id == root.Version.Id).ExecuteUpdateAsync(update => update.SetProperty(value => value.ValidationStatus, "needs_review"));
        await Db.DictionaryEntries.Where(value => value.Id == root.Entry.Id).ExecuteUpdateAsync(update => update.SetProperty(value => value.PublishedVersionId, root.Version.Id));
        using var notApproved = await PostAsync(app, Card(root));
        await AssertAsync(notApproved, HttpStatusCode.NotFound, "dictionary.entry_not_found");
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await unpublished.Content.ReadAsStringAsync());
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await notApproved.Content.ReadAsStringAsync());
        Assert.Empty(await Db.ContentReports.ToListAsync());
        Assert.Null((await LearnerAsync()).ReporterAlias);
    }

    [DockerFact]
    public async Task Endpoint_UsesRealHmacAndOwnClaim_AndDoesNotAcceptClientAlias()
    {
        var root = await SeedAsync();
        var other = new Learner
        {
            TelegramUserId = TelegramUserId + 1, TimeZone = "Europe/Warsaw", DailyNewWordsLimit = 5,
            OnboardedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(other);
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        using var anonymous = await app.Client.PostAsync(Path, new StringContent(Card(root), Encoding.UTF8, "application/json"));
        await AssertAsync(anonymous, HttpStatusCode.Unauthorized, "auth.invalid_init_data");
        using var forgedRequest = new HttpRequestMessage(HttpMethod.Post, Path) { Content = new StringContent(Card(root), Encoding.UTF8, "application/json") };
        forgedRequest.Headers.Authorization = new AuthenticationHeaderValue("tma", new TelegramInitDataSigner("wrong-bot").Sign(TelegramUserId, Clock.GetCurrentInstant()));
        using var forged = await app.Client.SendAsync(forgedRequest);
        await AssertAsync(forged, HttpStatusCode.Unauthorized, "auth.invalid_init_data");
        using var own = await SendAsync(app, HttpMethod.Post, $"{Path}?learner_id={other.Id}&reporter_alias=R-ATTACK", Card(root));
        await AssertAsync(own, HttpStatusCode.Accepted);
        Assert.Equal(root.Learner.Id, (await Db.ContentReports.AsNoTracking().SingleAsync()).LearnerId);
        Assert.Null((await Db.Learners.AsNoTracking().SingleAsync(value => value.Id == other.Id)).ReporterAlias);
        Assert.NotEqual("R-ATTACK", (await LearnerAsync()).ReporterAlias);
        using var newIdentity = await SendAsync(app, HttpMethod.Post, Path, Card(root), TelegramUserId + 2);
        await AssertAsync(newIdentity, HttpStatusCode.NotFound, "learner.not_onboarded");
    }

    [DockerFact]
    public async Task ExactMaximumTextLengths_AreAcceptedAndPreserved_AsUnicodeCodePoints()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var proposed = string.Concat(Enumerable.Repeat("🦉", 500));
        var comment = new string('к', 1000);
        using var response = await PostAsync(app, Card(root, "lemma", proposed, comment));
        await AssertAsync(response, HttpStatusCode.Accepted);
        var report = await Db.ContentReports.AsNoTracking().SingleAsync();
        Assert.Equal(proposed, report.ProposedValue);
        Assert.Equal(comment, report.Comment);
        using var query = await PostAsync(app, Missing(new string('с', 200)));
        await AssertAsync(query, HttpStatusCode.Accepted);
    }

    [DockerFact]
    public async Task DisputePort_BypassesNormalQuota_IsIdempotent_AndDoesNotLeakForeignAnswers()
    {
        var root = await SeedAsync();
        var other = new Learner
        {
            TelegramUserId = TelegramUserId + 1, TimeZone = "Europe/Warsaw", DailyNewWordsLimit = 5,
            OnboardedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(other);
        var first = NewAnswer(root, root.Learner.Id, "варіант один");
        var second = NewAnswer(root, root.Learner.Id, "варіант два");
        var foreign = NewAnswer(root, other.Id, "чужий варіант");
        Db.AnswerEvents.AddRange(first, second, foreign);
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var firstId = await CreateDisputeAsync(app, root.Learner.Id, first.Id);
        Assert.NotNull(firstId);
        Assert.Equal(firstId, await CreateDisputeAsync(app, root.Learner.Id, first.Id));
        Assert.Null(await CreateDisputeAsync(app, root.Learner.Id, foreign.Id));
        Assert.Null(await CreateDisputeAsync(app, root.Learner.Id, Guid.NewGuid()));
        Assert.Null((await Db.Learners.AsNoTracking().SingleAsync(value => value.Id == other.Id)).ReporterAlias);
        var dispute = await Db.ContentReports.AsNoTracking().SingleAsync();
        Assert.Equal("answer_dispute", dispute.Origin);
        Assert.Equal("in_editor_queue", dispute.Status);
        Assert.Null(dispute.DedupKey);
        Assert.Null(dispute.Field);
        Assert.Equal(first.RawAnswer, dispute.ProposedValue);
        Assert.Equal(first.VersionId, dispute.VersionId);
        for (var index = 0; index < 20; index++)
        {
            using var normal = await PostAsync(app, Missing("слово " + index));
            await AssertAsync(normal, HttpStatusCode.Accepted);
        }

        Assert.NotNull(await CreateDisputeAsync(app, root.Learner.Id, second.Id));
        using var exhausted = await PostAsync(app, Missing("ще одне слово"));
        await AssertAsync(exhausted, HttpStatusCode.TooManyRequests, "report.daily_limit_reached");
        Assert.Equal(22, await Db.ContentReports.CountAsync());
        Assert.Empty(await Db.VerificationJobs.ToListAsync());
    }

    [DockerFact]
    public async Task DisputePort_RequiresCallerTransaction_AndCallerRollbackRemovesReportAliasAndSnapshot()
    {
        var root = await SeedAsync();
        var answer = NewAnswer(root, root.Learner.Id, "мій варіант");
        Db.AnswerEvents.Add(answer);
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var reports = scope.ServiceProvider.GetRequiredService<IContentReportService>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => reports.CreateAnswerDisputeAsync(root.Learner.Id, answer.Id));
        }

        Assert.NotNull(await CreateDisputeAsync(app, root.Learner.Id, answer.Id, commit: false));
        Assert.Empty(await Db.ContentReports.ToListAsync());
        Assert.Empty(await Db.VerificationJobs.ToListAsync());
        Assert.Null((await LearnerAsync()).ReporterAlias);
        Assert.Null((await LearnerAsync()).CurrentDayDate);
        Assert.Equal("mistake_typo", (await Db.AnswerEvents.AsNoTracking().SingleAsync()).Outcome);
    }

    [DockerFact]
    public async Task AliasPort_RequiresCallerTransaction_AndNeverSavesOtherTrackedEntities()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var aliases = scope.ServiceProvider.GetRequiredService<IReporterAliasService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => aliases.GetOrCreateAsync(root.Learner.Id));
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.DictionaryEntries.Add(new DictionaryEntry { ExternalId = "not-saved", Domain = "everyday" });
        var alias = await aliases.GetOrCreateAsync(root.Learner.Id);
        Assert.Matches("^R-[0-9A-F]{12}$", alias);
        Assert.Equal(alias, await aliases.GetOrCreateAsync(root.Learner.Id));
        Assert.Equal(1, await db.DictionaryEntries.CountAsync());
        await transaction.RollbackAsync();
        Assert.Null((await LearnerAsync()).ReporterAlias);
    }

    private async Task<ReportRoot> SeedAsync()
    {
        Clock.Reset(Instant.FromUtc(2026, 1, 15, 11, 0));
        var learner = new Learner
        {
            TelegramUserId = TelegramUserId, FirstName = "Test User", TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = true, DailyNewWordsLimit = 5, CanMessage = true,
            OnboardedAt = Clock.GetCurrentInstant(), CreatedAt = Clock.GetCurrentInstant()
        };
        var entry = new DictionaryEntry { ExternalId = Guid.NewGuid().ToString("N"), Domain = "everyday" };
        var version = new EntryVersion
        {
            Entry = entry, LemmaPl = "żaba", PartOfSpeech = "noun", PronunciationUk = "жаба",
            ExamplePl = "To jest żaba.", ExampleUk = "Це жаба.", ValidationStatus = "approved",
            CreatedByKind = "import", ContentHash = new string('a', 64)
        };
        Db.AddRange(learner, version);
        await Db.SaveChangesAsync();
        entry.PublishedVersionId = version.Id;
        await Db.SaveChangesAsync();
        return new ReportRoot(learner, entry, version);
    }

    private Task<ReportsTestApplication> StartAsync(Action<IServiceCollection>? configure = null) =>
        ReportsTestApplication.StartAsync(_postgres.ConnectionString, Clock, configure);
    private Task<Learner> LearnerAsync() => Db.Learners.AsNoTracking().SingleAsync(value => value.TelegramUserId == TelegramUserId);
    private Task<HttpResponseMessage> PostAsync(ReportsTestApplication app, string json) => SendAsync(app, HttpMethod.Post, Path, json);

    private async Task<HttpResponseMessage> SendAsync(ReportsTestApplication app, HttpMethod method, string path, string json, long telegramUserId = TelegramUserId)
    {
        using var request = new HttpRequestMessage(method, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(telegramUserId, Clock.GetCurrentInstant()));
        return await app.Client.SendAsync(request);
    }

    private static string Card(ReportRoot root, string field = "translation", string? proposed = "правильний переклад", string? comment = null) =>
        JsonSerializer.Serialize(new { origin = "card", entry_id = root.Entry.Id, field, proposed_value = proposed, comment });
    private static string Missing(string query, string? comment = null) => JsonSerializer.Serialize(new { origin = "missing_word", search_query = query, comment });

    private AnswerEvent NewAnswer(ReportRoot root, Guid learnerId, string rawAnswer) => new()
    {
        LearnerId = learnerId, EntryId = root.Entry.Id, VersionId = root.Version.Id,
        ExerciseKind = "review", Outcome = "mistake_typo", RawAnswer = rawAnswer,
        AnsweredAt = Clock.GetCurrentInstant(), LearnerDay = new LocalDate(2026, 1, 15),
        TimeZone = "Europe/Warsaw", SchedulerVersion = "memorize-v1", StateBefore = "{}"
    };

    private static async Task<Guid?> CreateDisputeAsync(ReportsTestApplication app, Guid learnerId, Guid answerId, bool commit = true)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var id = await scope.ServiceProvider.GetRequiredService<IContentReportService>().CreateAnswerDisputeAsync(learnerId, answerId);
        if (commit) await transaction.CommitAsync();
        else await transaction.RollbackAsync();
        return id;
    }
    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    private static async Task AssertAsync(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        await ContractAssert.ResponseAsync("createContentReport", response);
        if (code is not null) Assert.Equal(code, (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    private sealed record ReportRoot(Learner Learner, DictionaryEntry Entry, EntryVersion Version);
    private sealed class ReportWriteFailure : Exception { }

    private sealed class FailFirstVerificationWrite : DbCommandInterceptor
    {
        private int _fail = 1;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO verification_jobs", StringComparison.Ordinal)
                && Interlocked.Exchange(ref _fail, 0) == 1) throw new ReportWriteFailure();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CollisionGenerator : IReporterAliasGenerator
    {
        public int Calls { get; private set; }
        public string Create() => ++Calls == 1 ? "R-AAAAAAAAAAAA" : "R-BBBBBBBBBBBB";
    }
}
