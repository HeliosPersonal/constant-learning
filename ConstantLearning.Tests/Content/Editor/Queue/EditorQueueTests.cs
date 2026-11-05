using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Import;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Tests.Content.Import;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Content.Reports;
using ConstantLearning.Tests.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;

namespace ConstantLearning.Tests.Content.Editor.Queue;

public sealed class EditorQueueTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const string Path = "/api/v1/editor/content-reports";
    private const long EditorTelegramId = 900004321;
    private const long LearnerTelegramId = 900004322;

    [EditorDockerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ac38Ac42_Approve_ClonesImmutableContentAndPublishesSearchWithoutChangingLearnerHistory(bool context)
    {
        var root = await SeedAsync(context);
        var state = new LearnerWord
        {
            LearnerId = root.Learner.Id,
            EntryId = root.Entry.Id,
            Status = "recalled",
            ModelState = "{\"n\":7}",
            SchedulerVersion = "test-v1",
            IntervalDays = 7,
            ConsecutiveScheduledCorrect = 2,
            DueOn = new LocalDate(2026, 11, 7)
        };
        var answer = NewAnswer(root);
        Db.AddRange(state, answer);
        await Db.SaveChangesAsync();
        await Db.Entry(state).ReloadAsync();
        var previousProofs = await Db.ValidationEvidence.AsNoTracking().Where(row => row.VersionId == root.Version.Id).ToArrayAsync();
        await using var app = await StartAsync();
        using var response = await DecisionAsync(app, root.Report.Id, new { decision = "approve", corrected_value = "ропуха" });
        await AssertResponseAsync(response, HttpStatusCode.OK);
        var report = await Db.ContentReports.AsNoTracking().SingleAsync();
        var next = await Db.EntryVersions.AsNoTracking().SingleAsync(row => row.Id == report.ResultingVersionId);
        Assert.Equal("closed", report.Status);
        Assert.Equal("applied", report.Outcome);
        Assert.Equal(root.Version.Id, report.VersionId);
        Assert.Equal(root.Version.Id, next.BasedOnVersionId);
        Assert.Equal("editor", next.CreatedByKind);
        Assert.Equal(root.Report.Id, next.ContentReportId);
        next.Should().BeEquivalentTo(root.Version, options => options.Excluding(row => row.Id)
            .Excluding(row => row.BasedOnVersionId).Excluding(row => row.CreatedByKind).Excluding(row => row.ContentReportId)
            .Excluding(row => row.ContentHash).Excluding(row => row.CreatedAt).Excluding(row => row.Version)
            .Excluding(row => row.Entry).Excluding(row => row.BasedOnVersion));
        (await Db.EntryVersions.AsNoTracking().SingleAsync(row => row.Id == root.Version.Id)).Should()
            .BeEquivalentTo(root.Version, options => options.Excluding(row => row.Entry).Excluding(row => row.BasedOnVersion));
        Assert.Equal(new[] { "ропуха", "жабка" }, await Db.VersionTranslations.Where(row => row.VersionId == next.Id)
            .OrderBy(row => row.Position).Select(row => row.TextUk).ToArrayAsync());
        Assert.Equal(new[] { "żaba", "żabka" }, await Db.VersionAcceptedAnswers.Where(row => row.VersionId == next.Id)
            .OrderBy(row => row.TextPl).Select(row => row.TextPl).ToArrayAsync());
        var nextProofs = await Db.ValidationEvidence.AsNoTracking().Where(row => row.VersionId == next.Id).ToArrayAsync();
        nextProofs.Should().BeEquivalentTo(previousProofs, options => options.Excluding(row => row.Id)
            .Excluding(row => row.VersionId).Excluding(row => row.CreatedAt));
        var log = await Db.PublicationLog.AsNoTracking().SingleAsync(row => row.ActorKind == "editor");
        Assert.Equal(root.Editor.Id, log.ActorLearnerId);
        Assert.Equal(root.Version.Id, log.FromVersionId);
        Assert.Equal(next.Id, log.ToVersionId);
        Assert.Equal(next.Id, (await Db.DictionaryEntries.AsNoTracking().SingleAsync()).PublishedVersionId);
        Assert.True(await Db.SearchForms.AnyAsync(row => row.VersionId == next.Id && row.FormDisplay == "ропуха"));
        Assert.False(await Db.SearchForms.AnyAsync(row => row.VersionId == root.Version.Id));
        (await Db.LearnerWords.AsNoTracking().SingleAsync()).Should().BeEquivalentTo(state);
        (await Db.AnswerEvents.AsNoTracking().SingleAsync()).Should().BeEquivalentTo(answer);
        Assert.Empty(await Db.AnswerNeutralizations.ToListAsync());
        using var card = await SendAsync(app, HttpMethod.Get, $"/api/v1/dictionary/entries/{root.Entry.Id}", telegramId: LearnerTelegramId);
        Assert.Equal(HttpStatusCode.OK, card.StatusCode);
        await ContractAssert.ResponseAsync("getDictionaryEntry", card);
        Assert.Contains("ропуха", await card.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var duplicate = await DecisionAsync(app, root.Report.Id, new { decision = "approve" });
        await AssertResponseAsync(duplicate, HttpStatusCode.Conflict, "report.already_closed");
        Assert.Equal(2, await Db.EntryVersions.CountAsync());
    }

    [DockerFact]
    public async Task AnswerVariantApproval_DoesNotRegradePreviousMistakeOrModifyStatus()
    {
        var root = await SeedAsync();
        var answer = NewAnswer(root);
        Db.AnswerEvents.Add(answer);
        await Db.ContentReports.Where(row => row.Id == root.Report.Id).ExecuteDeleteAsync();
        var dispute = new ContentReport
        {
            LearnerId = root.Learner.Id,
            EntryId = root.Entry.Id,
            VersionId = root.Version.Id,
            AnswerEventId = answer.Id,
            Origin = "answer_dispute",
            ProposedValue = "ropucha",
            Status = "in_editor_queue",
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.ContentReports.Add(dispute);
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        using var response = await DecisionAsync(app, dispute.Id, new { decision = "approve" });
        await AssertResponseAsync(response, HttpStatusCode.OK);
        var versionId = (await Db.DictionaryEntries.AsNoTracking().SingleAsync()).PublishedVersionId;
        Assert.Equal(new[] { "ropucha", "żaba", "żabka" }, await Db.VersionAcceptedAnswers.Where(row => row.VersionId == versionId)
            .OrderBy(row => row.TextPl).Select(row => row.TextPl).ToArrayAsync());
        Assert.Equal("mistake_typo", (await Db.AnswerEvents.AsNoTracking().SingleAsync()).Outcome);
        Assert.Equal(root.Version.Id, (await Db.AnswerEvents.AsNoTracking().SingleAsync()).VersionId);
        Assert.Empty(await Db.AnswerNeutralizations.ToListAsync());
    }

    [EditorDockerTheory]
    [InlineData("source_not_confirming")]
    [InlineData("already_correct")]
    [InlineData("out_of_scope")]
    [InlineData("duplicate")]
    public async Task Ac39_Reject_ClosesWithReasonWithoutPublishing(string reason)
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        using var response = await DecisionAsync(app, root.Report.Id, new { decision = "reject", rejection_reason = reason });
        await AssertResponseAsync(response, HttpStatusCode.OK);
        var report = await Db.ContentReports.AsNoTracking().SingleAsync();
        Assert.Equal("rejected", report.Outcome);
        Assert.Equal(reason, report.RejectionReason);
        Assert.NotNull(report.ClosedAt);
        Assert.Null(report.SummaryDispatchId);
        Assert.Null(report.ResultingVersionId);
        Assert.Equal(root.Version.Id, (await Db.DictionaryEntries.AsNoTracking().SingleAsync()).PublishedVersionId);
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
        Assert.Equal(1, await Db.PublicationLog.CountAsync());
        using var repeat = await DecisionAsync(app, root.Report.Id, new { decision = "reject", rejection_reason = reason });
        await AssertResponseAsync(repeat, HttpStatusCode.Conflict, "report.already_closed");
    }

    [DockerFact]
    public async Task Ac40_MissingWord_CreatesOnlyUnpublishedDraftCandidate()
    {
        var root = await SeedAsync();
        var missing = await AddMissingAsync(root, "  kot  ");
        await using var app = await StartAsync();
        using var response = await DecisionAsync(app, missing.Id, new { decision = "add_to_candidates" });
        await AssertResponseAsync(response, HttpStatusCode.OK);
        var report = await Db.ContentReports.AsNoTracking().SingleAsync(row => row.Id == missing.Id);
        Assert.Equal("added_to_candidates", report.Outcome);
        var candidate = await Db.EntryVersions.AsNoTracking().SingleAsync(row => row.Id == report.ResultingVersionId);
        Assert.Equal("kot", candidate.LemmaPl);
        Assert.Equal("draft", candidate.ValidationStatus);
        var entry = await Db.DictionaryEntries.AsNoTracking().SingleAsync(row => row.Id == candidate.EntryId);
        Assert.Null(entry.PublishedVersionId);
        Assert.Null(entry.CoursePosition);
        Assert.False(await Db.SearchForms.AnyAsync(row => row.EntryId == entry.Id));
        Assert.Equal(1, await Db.PublicationLog.CountAsync());
        using var invisible = await SendAsync(app, HttpMethod.Get, $"/api/v1/dictionary/entries/{entry.Id}", telegramId: LearnerTelegramId);
        Assert.Equal(HttpStatusCode.NotFound, invisible.StatusCode);
        var duplicate = await AddMissingAsync(root, "kot");
        using var rejected = await DecisionAsync(app, duplicate.Id, new { decision = "add_to_candidates" });
        await AssertResponseAsync(rejected, HttpStatusCode.BadRequest, "editor.duplicate_lemma");
        var phrase = await AddMissingAsync(root, "mały kot");
        using var invalid = await DecisionAsync(app, phrase.Id, new { decision = "add_to_candidates" });
        await AssertResponseAsync(invalid, HttpStatusCode.BadRequest, "editor.invalid_value");
    }

    [EditorDockerTheory]
    [InlineData("already_in_dictionary")]
    [InlineData("phrase_not_lemma")]
    [InlineData("out_of_scope")]
    public async Task Ac40_MissingWord_RejectionPreservesDictionary(string reason)
    {
        var root = await SeedAsync();
        var missing = await AddMissingAsync(root, "mały kot");
        await using var app = await StartAsync();
        using var response = await DecisionAsync(app, missing.Id, new { decision = "reject", rejection_reason = reason });
        await AssertResponseAsync(response, HttpStatusCode.OK);
        var report = await Db.ContentReports.AsNoTracking().SingleAsync(row => row.Id == missing.Id);
        Assert.Equal(reason, report.RejectionReason);
        Assert.Equal("rejected", report.Outcome);
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
    }

    [EditorDockerTheory]
    [InlineData("lemma", "ropucha")]
    [InlineData("pronunciation", "жааба")]
    [InlineData("example", "Mała żaba siedzi przy stawie.")]
    public async Task CardFields_ApproveChangesOnlyRequestedFieldAndPreservesVariants(string field, string value)
    {
        var root = await SeedAsync();
        await Db.ContentReports.ExecuteUpdateAsync(update => update.SetProperty(row => row.Field, field).SetProperty(row => row.ProposedValue, value));
        await using var app = await StartAsync();
        using var response = await DecisionAsync(app, root.Report.Id, new { decision = "approve" });
        await AssertResponseAsync(response, HttpStatusCode.OK);
        var next = await Db.EntryVersions.AsNoTracking().SingleAsync(row => row.Id != root.Version.Id);
        Assert.Equal(field == "lemma" ? value : root.Version.LemmaPl, next.LemmaPl);
        Assert.Equal(field == "pronunciation" ? value : root.Version.PronunciationUk, next.PronunciationUk);
        Assert.Equal(field == "example" ? value : root.Version.ExamplePl, next.ExamplePl);
        Assert.Equal(root.Version.ExampleUk, next.ExampleUk);
        Assert.Equal(new[] { "жаба", "жабка" }, await Db.VersionTranslations.Where(row => row.VersionId == next.Id)
            .OrderBy(row => row.Position).Select(row => row.TextUk).ToArrayAsync());
        Assert.True(await Db.VersionAcceptedAnswers.AnyAsync(row => row.VersionId == next.Id && row.TextPl == "żabka" && !row.IsCanonical));
        Assert.Equal(field == "lemma" ? value : root.Version.LemmaPl,
            await Db.VersionAcceptedAnswers.Where(row => row.VersionId == next.Id && row.IsCanonical).Select(row => row.TextPl).SingleAsync());
    }

    [DockerFact]
    public async Task Ac41_RealHmacAndEditorPolicy_ProtectBothEndpoints()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        using var anonymous = await app.Client.GetAsync(Path);
        await AssertResponseAsync(anonymous, HttpStatusCode.Unauthorized);
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post })
        {
            using var response = await SendAsync(app, method, method == HttpMethod.Get ? Path : $"{Path}/{root.Report.Id}/decision",
                method == HttpMethod.Get ? null : new { decision = "approve" }, LearnerTelegramId);
            await AssertResponseAsync(response, HttpStatusCode.Forbidden, "editor.role_required", method == HttpMethod.Get);
        }
        using var forged = new HttpRequestMessage(HttpMethod.Get, Path);
        forged.Headers.Authorization = new("tma", new TelegramInitDataSigner("wrong-token").Sign(EditorTelegramId, Clock.GetCurrentInstant()));
        using var denied = await app.Client.SendAsync(forged);
        await AssertResponseAsync(denied, HttpStatusCode.Unauthorized);
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
    }

    [DockerFact]
    public async Task Ac51_Queue_UsesPseudonymsStableBidirectionalCursorsAndOneDeduplicatedRow()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var payload = new { origin = "missing_word", search_query = "kot" };
        using var one = await SendAsync(app, HttpMethod.Post, "/api/v1/content-reports", payload, LearnerTelegramId);
        using var two = await SendAsync(app, HttpMethod.Post, "/api/v1/content-reports", payload, LearnerTelegramId);
        Assert.Equal(HttpStatusCode.Accepted, one.StatusCode);
        Assert.Equal(await one.Content.ReadAsStringAsync(), await two.Content.ReadAsStringAsync());
        await AddMissingAsync(root, "pies", Duration.FromMinutes(1));
        using var first = await SendAsync(app, HttpMethod.Get, Path + "?limit=1");
        await AssertResponseAsync(first, HttpStatusCode.OK, queue: true);
        var page = await JsonAsync(first);
        Assert.Equal(root.Report.Id, page["items"]![0]!["id"]!.GetValue<Guid>());
        var json = await first.Content.ReadAsStringAsync();
        Assert.DoesNotContain("first_name", json, StringComparison.Ordinal);
        Assert.DoesNotContain("telegram", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(root.Learner.Id.ToString(), json, StringComparison.Ordinal);
        Assert.DoesNotContain("Private Name", json, StringComparison.Ordinal);
        Assert.Equal("R-ABCDEF123456", page["items"]![0]!["reporter_alias"]!.GetValue<string>());
        Assert.True(page["has_next"]!.GetValue<bool>());
        using var close = await DecisionAsync(app, root.Report.Id, new { decision = "reject", rejection_reason = "already_correct" });
        await AssertResponseAsync(close, HttpStatusCode.OK);
        using var next = await SendAsync(app, HttpMethod.Get, Path + "?limit=1&after=" + root.Report.Id);
        await AssertResponseAsync(next, HttpStatusCode.OK, queue: true);
        var middle = await JsonAsync(next);
        var middleId = middle["items"]![0]!["id"]!.GetValue<Guid>();
        using var last = await SendAsync(app, HttpMethod.Get, Path + "?limit=1&after=" + middleId);
        await AssertResponseAsync(last, HttpStatusCode.OK, queue: true);
        var lastId = (await JsonAsync(last))["items"]![0]!["id"]!.GetValue<Guid>();
        using var previous = await SendAsync(app, HttpMethod.Get, Path + "?limit=1&before=" + lastId);
        await AssertResponseAsync(previous, HttpStatusCode.OK, queue: true);
        Assert.Equal(middleId, (await JsonAsync(previous))["items"]![0]!["id"]!.GetValue<Guid>());
        using var all = await SendAsync(app, HttpMethod.Get, Path);
        Assert.Equal(2, (await JsonAsync(all))["items"]!.AsArray().Count);
    }

    [DockerFact]
    public async Task ParallelDecisions_ApplyExactlyOnce_AndCloseReturnsConflict()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => DecisionAsync(app, root.Report.Id, new { decision = "approve" })));
        try
        {
            Assert.Single(results, row => row.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, row => row.StatusCode == HttpStatusCode.Conflict);
            foreach (var response in results) await ContractAssert.ResponseAsync("decideContentReport", response);
        }
        finally { foreach (var response in results) response.Dispose(); }
        Assert.Equal(2, await Db.EntryVersions.CountAsync());
        Assert.Equal(2, await Db.PublicationLog.CountAsync());
    }

    [DockerFact]
    public async Task ProjectionFailure_RollsBackVersionReportHistoryAndSearch()
    {
        var root = await SeedAsync();
        var before = await Db.SearchForms.AsNoTracking().ToArrayAsync();
        await using var app = await StartAsync(services => services.AddScoped<IPublicationProjection, FailingProjection>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => DecisionAsync(app, root.Report.Id, new { decision = "approve" }));
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
        Assert.Equal(1, await Db.PublicationLog.CountAsync());
        Assert.Equal("in_editor_queue", (await Db.ContentReports.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(root.Version.Id, (await Db.DictionaryEntries.AsNoTracking().SingleAsync()).PublishedVersionId);
        (await Db.SearchForms.AsNoTracking().ToArrayAsync()).Should().BeEquivalentTo(before);
    }

    [EditorDockerTheory]
    [InlineData("gender")]
    [InlineData("evidence")]
    [InlineData("example")]
    [InlineData("unpublished")]
    [InlineData("changed")]
    public async Task InvalidOrChangedEntry_CannotBeApproved(string condition)
    {
        var root = await SeedAsync();
        if (condition == "gender") await Db.EntryVersions.ExecuteUpdateAsync(update => update.SetProperty(row => row.Gender, (string?)null));
        if (condition == "evidence") await Db.ValidationEvidence.ExecuteDeleteAsync();
        if (condition == "example") await Db.EntryVersions.ExecuteUpdateAsync(update => update.SetProperty(row => row.ExampleUk, ""));
        if (condition == "unpublished") await Db.DictionaryEntries.ExecuteUpdateAsync(update => update.SetProperty(row => row.PublishedVersionId, (Guid?)null));
        if (condition == "changed") await Db.ContentReports.ExecuteUpdateAsync(update => update.SetProperty(row => row.VersionId, (Guid?)null));
        await using var app = await StartAsync();
        using var response = await DecisionAsync(app, root.Report.Id, new { decision = "approve" });
        await AssertResponseAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("in_editor_queue", (await Db.ContentReports.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(1, await Db.EntryVersions.CountAsync());
    }

    [DockerFact]
    public async Task InvalidRequests_AndMissingReport_ReturnContractErrors()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        using var missingReason = await DecisionAsync(app, root.Report.Id, new { decision = "reject" });
        await AssertResponseAsync(missingReason, HttpStatusCode.BadRequest, "editor.rejection_reason_required");
        using var missing = await DecisionAsync(app, Guid.NewGuid(), new { decision = "approve" });
        await AssertResponseAsync(missing, HttpStatusCode.NotFound, "report.not_found");
        foreach (var payload in new object[] { new { decision = "bad" }, new { decision = "approve", corrected_value = "" },
                     new { decision = "reject", rejection_reason = "phrase_not_lemma" }, new { decision = "add_to_candidates" },
                     new { decision = "approve", source = "fake-proof" } })
        {
            using var response = await DecisionAsync(app, root.Report.Id, payload);
            await AssertResponseAsync(response, HttpStatusCode.BadRequest);
        }
    }

    [DockerFact]
    public async Task Ac11_Queue_FiltersByTargetLanguageWithoutMixing()
    {
        var root = await SeedAsync();
        await AddLanguageReportAsync(root, "de", "Haus", Duration.FromMinutes(1));
        await AddLanguageReportAsync(root, "en", "house", Duration.FromMinutes(2));
        await using var app = await StartAsync();

        // OQ-9: один редактор читає будь-яку мову без per-language 403 — фільтр лише розмежовує, не авторизує.
        using var german = await SendAsync(app, HttpMethod.Get, Path + "?target_language=de");
        await AssertResponseAsync(german, HttpStatusCode.OK, queue: true);
        var germanItem = (await JsonAsync(german))["items"]!.AsArray().Single()!;
        Assert.Equal("de", germanItem["target_language"]!.GetValue<string>());

        using var polish = await SendAsync(app, HttpMethod.Get, Path + "?target_language=pl");
        await AssertResponseAsync(polish, HttpStatusCode.OK, queue: true);
        var polishItems = (await JsonAsync(polish))["items"]!.AsArray();
        Assert.Equal(root.Report.Id, polishItems.Single()!["id"]!.GetValue<Guid>());
        Assert.Equal("pl", polishItems.Single()!["target_language"]!.GetValue<string>());

        using var all = await SendAsync(app, HttpMethod.Get, Path);
        await AssertResponseAsync(all, HttpStatusCode.OK, queue: true);
        var languages = (await JsonAsync(all))["items"]!.AsArray()
            .Select(item => item!["target_language"]!.GetValue<string>()).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "de", "en", "pl" }, languages);

        using var unknown = await SendAsync(app, HttpMethod.Get, Path + "?target_language=zz");
        await AssertResponseAsync(unknown, HttpStatusCode.BadRequest, "editor.invalid_request", queue: true);
        using var malformed = await SendAsync(app, HttpMethod.Get, Path + "?target_language=pol");
        await AssertResponseAsync(malformed, HttpStatusCode.BadRequest, "editor.invalid_request", queue: true);
    }

    [DockerFact]
    public async Task Ac09_CardReport_InheritsEntryTargetLanguage()
    {
        var root = await SeedAsync();
        var german = await AddApprovedEntryAsync("de", "Haus", 2);
        await using var app = await StartAsync();
        using var response = await SendAsync(app, HttpMethod.Post, "/api/v1/content-reports",
            new { origin = "card", entry_id = german.Id, field = "lemma", proposed_value = "Gebäude" }, LearnerTelegramId);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var report = await Db.ContentReports.AsNoTracking().SingleAsync(row => row.EntryId == german.Id);
        Assert.Equal("de", report.TargetLanguage);
    }

    [DockerFact]
    public async Task Ac09_MissingWordReport_InheritsActiveTargetLanguage()
    {
        var root = await SeedAsync();
        await Db.Learners.Where(row => row.Id == root.Learner.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.ActiveTargetLanguage, "en"));
        await using var app = await StartAsync();
        using var response = await SendAsync(app, HttpMethod.Post, "/api/v1/content-reports",
            new { origin = "missing_word", search_query = "cat" }, LearnerTelegramId);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var report = await Db.ContentReports.AsNoTracking()
            .SingleAsync(row => row.Origin == "missing_word" && row.LearnerId == root.Learner.Id);
        Assert.Equal("en", report.TargetLanguage);
    }

    private async Task<ContentReport> AddLanguageReportAsync(Root root, string targetLanguage, string query, Duration offset = default)
    {
        var report = new ContentReport
        {
            LearnerId = root.Learner.Id,
            TargetLanguage = targetLanguage,
            Origin = "missing_word",
            SearchQuery = query,
            Status = "in_editor_queue",
            CreatedAt = Clock.GetCurrentInstant() + offset
        };
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return report;
    }

    private async Task<DictionaryEntry> AddApprovedEntryAsync(string targetLanguage, string lemmaPl, int coursePosition)
    {
        var graph = ContentFixtures.NewPublishedEntry(targetLanguage, lemmaPl, coursePosition, clock: Clock);
        Db.AddRange(graph.Entry, graph.Version, graph.Translation);
        await Db.SaveChangesAsync();
        graph.Entry.PublishedVersionId = graph.Version.Id;
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return graph.Entry;
    }

    private async Task<Root> SeedAsync(bool context = false)
    {
        using var bundle = new DictionaryImportTestBundle();
        bundle.Row["show_context_before"] = context ? "true" : "false";
        bundle.Row["accepted_answers_pl"] = "żaba;żabka";
        bundle.Evidence["accepted_answers_pl"] = new JsonArray("żaba", "żabka");
        await bundle.WriteAsync();
        await new DictionaryImportService(Db, new DictionaryImportReader(),
            new ContentPublicationService(Db, new SearchFormsProjection(Db, Clock), Clock), Clock,
            NullLogger<DictionaryImportService>.Instance).ImportAsync(bundle.DirectoryPath);
        var learner = new Learner
        {
            TelegramUserId = LearnerTelegramId,
            FirstName = "Private Name",
            TimeZone = "Europe/Warsaw",
            DailyNewWordsLimit = 5,
            ReporterAlias = "R-ABCDEF123456",
            TimeZoneConfirmed = true,
            OnboardedAt = Clock.GetCurrentInstant()
        };
        var editor = new Learner { TelegramUserId = EditorTelegramId, TimeZone = "Europe/Warsaw", DailyNewWordsLimit = 5 };
        Db.AddRange(learner, editor);
        var entry = await Db.DictionaryEntries.AsNoTracking().SingleAsync();
        var version = await Db.EntryVersions.AsNoTracking().SingleAsync();
        var report = new ContentReport
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            VersionId = version.Id,
            Origin = "card",
            Field = "translation",
            ProposedValue = "ропуха",
            Status = "in_editor_queue",
            CreatedAt = Clock.GetCurrentInstant() - Duration.FromMinutes(1)
        };
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return new(learner, editor, entry, version, report);
    }

    private async Task<ContentReport> AddMissingAsync(Root root, string query, Duration offset = default)
    {
        var report = new ContentReport
        {
            LearnerId = root.Learner.Id,
            Origin = "missing_word",
            SearchQuery = query,
            Status = "in_editor_queue",
            CreatedAt = Clock.GetCurrentInstant() + offset
        };
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return report;
    }

    private AnswerEvent NewAnswer(Root root) => new()
    {
        LearnerId = root.Learner.Id,
        EntryId = root.Entry.Id,
        VersionId = root.Version.Id,
        ExerciseKind = "review",
        Outcome = "mistake_typo",
        RawAnswer = "ropucha",
        NormalizedAnswer = "ropucha",
        AnsweredAt = Clock.GetCurrentInstant(),
        LearnerDay = new LocalDate(2026, 10, 31),
        TimeZone = "Europe/Warsaw",
        SchedulerVersion = "test-v1",
        StateBefore = "{}"
    };

    private Task<ReportsTestApplication> StartAsync(Action<IServiceCollection>? configure = null) =>
        ReportsTestApplication.StartAsync(_postgres.ConnectionString, Clock, services =>
        {
            services.Configure<IdentityOptions>(options => options.ContentEditorTelegramUserIds = [EditorTelegramId]);
            configure?.Invoke(services);
        });
    private Task<HttpResponseMessage> DecisionAsync(ReportsTestApplication app, Guid reportId, object payload) =>
        SendAsync(app, HttpMethod.Post, $"{Path}/{reportId}/decision", payload);
    private async Task<HttpResponseMessage> SendAsync(ReportsTestApplication app, HttpMethod method, string path,
        object? payload = null, long telegramId = EditorTelegramId)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(telegramId, Clock.GetCurrentInstant()));
        if (payload is not null) request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return await app.Client.SendAsync(request);
    }
    private static async Task AssertResponseAsync(HttpResponseMessage response, HttpStatusCode status, string? code = null, bool queue = false)
    {
        Assert.Equal(status, response.StatusCode);
        await ContractAssert.ResponseAsync(queue ? "listEditorQueueByLanguage" : "decideContentReport", response);
        if (code is not null) Assert.Equal(code, (await JsonAsync(response))["code"]!.GetValue<string>());
    }
    private static async Task<JsonObject> JsonAsync(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    private sealed record Root(Learner Learner, Learner Editor, DictionaryEntry Entry, EntryVersion Version, ContentReport Report);
    private sealed class FailingProjection : IPublicationProjection
    {
        public Task UpdateAsync(IReadOnlyList<PublicationChange> changes, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Тестова помилка проєкції.");
    }
}

public sealed class EditorDockerTheoryAttribute : TheoryAttribute
{
    public EditorDockerTheoryAttribute() =>
        Skip = DockerAvailability.GetSkipReason(DockerAvailability.IsCi, DockerAvailability.UnavailableReason);
}
