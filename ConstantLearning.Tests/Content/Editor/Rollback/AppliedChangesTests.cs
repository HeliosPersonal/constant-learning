using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Import;
using ConstantLearning.Features.Content.Publishing;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Features.Identity.Auth;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Notifications.Data;
using ConstantLearning.Tests.Content.Import;
using ConstantLearning.Tests.Content.Reports;
using ConstantLearning.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;

namespace ConstantLearning.Tests.Content.Editor.Rollback;

public sealed class AppliedChangesTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const string Path = "/api/v1/editor/applied-changes";
    private const long EditorTelegramId = 900005001;
    private const long LearnerTelegramId = 900005002;

    [RollbackDockerTheory]
    [InlineData("translation", "ропуха", "жаба", "editor")]
    [InlineData("lemma", "ropucha", "żaba", "auto_verification")]
    [InlineData("pronunciation", "жааба", "жаба", "editor")]
    [InlineData("example", "Mała żaba siedzi.", "To jest zielona żaba.", "auto_verification")]
    public async Task Ac48_ListAndRollback_RestorePublishedContentAndSearch_PreserveHistory(
        string field, string value, string previousValue, string actor)
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var change = await PublishAsync(app, root, field, value);
        if (actor == "auto_verification")
            await Db.PublicationLog.Where(row => row.Id == change.Id).ExecuteUpdateAsync(update =>
                update.SetProperty(row => row.ActorKind, actor).SetProperty(row => row.ActorLearnerId, (Guid?)null));
        var sent = new NotificationDispatch
        {
            RecipientTelegramUserId = LearnerTelegramId,
            LearnerId = root.Learner.Id,
            Kind = "report_summary",
            LocalDate = new LocalDate(2026, 10, 31),
            DueAt = Clock.GetCurrentInstant(),
            Status = "sent",
            Attempts = 1,
            SentAt = Clock.GetCurrentInstant(),
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.NotificationDispatches.Add(sent);
        await Db.SaveChangesAsync();
        await Db.ContentReports.Where(row => row.ResultingVersionId == change.ToVersionId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.SummaryDispatchId, sent.Id));
        Db.ChangeTracker.Clear();
        var preserved = await HistoryAsync();
        var versions = await VersionsAsync();
        using var list = await SendAsync(app, HttpMethod.Get, Path);
        await AssertResponseAsync(list, HttpStatusCode.OK, list: true);
        var item = (await JsonAsync(list))["items"]![0]!;
        Assert.Equal(change.Id, item["id"]!.GetValue<Guid>());
        Assert.Equal(field, item["field"]!.GetValue<string>());
        Assert.Equal(previousValue, item["previous_value"]!.GetValue<string>());
        Assert.Equal(value, item["new_value"]!.GetValue<string>());
        Assert.Equal(actor, item["confirmed_by"]!.GetValue<string>());
        Assert.Equal(change.CreatedAt.ToDateTimeOffset(), item["applied_at"]!.GetValue<DateTimeOffset>());
        Assert.True(item["rollback_allowed"]!.GetValue<bool>());
        Clock.Advance(Duration.FromMinutes(7));
        using var response = await RollbackAsync(app, change.Id);
        await AssertResponseAsync(response, HttpStatusCode.OK);
        var result = await JsonAsync(response);
        Assert.Equal(change.Id, result["change_id"]!.GetValue<Guid>());
        Assert.Equal(root.Version.Id, result["published_version_id"]!.GetValue<Guid>());
        Assert.Equal(Clock.GetCurrentInstant().ToDateTimeOffset(), result["rolled_back_at"]!.GetValue<DateTimeOffset>());
        Assert.Equal(root.Version.Id, await PublishedAsync(root.Entry.Id));
        var rollback = await Db.PublicationLog.AsNoTracking().SingleAsync(row => row.Action == "rollback");
        Assert.Equal(change.ToVersionId, rollback.FromVersionId);
        Assert.Equal(root.Version.Id, rollback.ToVersionId);
        Assert.Equal(root.Editor.Id, rollback.ActorLearnerId);
        Assert.Equal("editor", rollback.ActorKind);
        Assert.Equal(Clock.GetCurrentInstant(), rollback.CreatedAt);
        Assert.All(await Db.SearchForms.ToArrayAsync(), row => Assert.Equal(root.Version.Id, row.VersionId));
        Assert.True(await Db.SearchForms.AnyAsync(row => row.FormDisplay == "жаба"));
        Assert.Equal(preserved, await HistoryAsync());
        Assert.Equal(versions, await VersionsAsync());
        using var card = await SendAsync(app, HttpMethod.Get, $"/api/v1/dictionary/entries/{root.Entry.Id}", telegramId: LearnerTelegramId);
        await ContractAssert.ResponseAsync("getDictionaryEntry", card);
        Assert.Contains(previousValue, await card.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var refreshed = await SendAsync(app, HttpMethod.Get, Path);
        var page = await JsonAsync(refreshed);
        Assert.Single(page["items"]!.AsArray());
        Assert.False(page["items"]![0]!["rollback_allowed"]!.GetValue<bool>());
        using var repeat = await RollbackAsync(app, change.Id);
        await AssertResponseAsync(repeat, HttpStatusCode.Conflict, "editor.rollback_not_current");
        Assert.Equal(3, await Db.PublicationLog.CountAsync());
    }

    [DockerFact]
    public async Task Ac49_NewerPublication_BlocksOlderRollback()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var older = await PublishAsync(app, root, "translation", "ропуха");
        Clock.Advance(Duration.FromMinutes(1));
        var newer = await PublishAsync(app, root, "translation", "жабеня");
        using var response = await RollbackAsync(app, older.Id);
        await AssertResponseAsync(response, HttpStatusCode.Conflict, "editor.rollback_not_current");
        Assert.Equal(newer.ToVersionId, await PublishedAsync(root.Entry.Id));
        Assert.False(await Db.PublicationLog.AnyAsync(row => row.Action == "rollback"));
    }

    [RollbackDockerTheory]
    [InlineData(null, "ropucha")]
    [InlineData("żabka", "żabka")]
    public async Task Ac48_ApprovedAnswerDispute_ListsActualAddedVariantAndRollsBackWithoutRewritingHistory(
        string? correctedValue, string expectedValue)
    {
        var root = await SeedAsync();
        var dispute = new ContentReport
        {
            LearnerId = root.Learner.Id,
            EntryId = root.Entry.Id,
            VersionId = root.Version.Id,
            AnswerEventId = await Db.AnswerEvents.Select(row => row.Id).SingleAsync(),
            Origin = "answer_dispute",
            ProposedValue = "ropucha",
            Status = "in_editor_queue",
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.ContentReports.Add(dispute);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        await using var app = await StartAsync();
        using var approval = await SendAsync(app, HttpMethod.Post,
            $"/api/v1/editor/content-reports/{dispute.Id}/decision",
            new { decision = "approve", corrected_value = correctedValue });
        Assert.Equal(HttpStatusCode.OK, approval.StatusCode);
        await ContractAssert.ResponseAsync("decideContentReport", approval);
        var publishedId = await PublishedAsync(root.Entry.Id);
        var change = await Db.PublicationLog.AsNoTracking().SingleAsync(row => row.ToVersionId == publishedId);
        var history = await HistoryAsync();
        var versions = await VersionsAsync();

        using var list = await SendAsync(app, HttpMethod.Get, Path);
        await AssertResponseAsync(list, HttpStatusCode.OK, list: true);
        var items = (await JsonAsync(list))["items"]!.AsArray();
        var item = Assert.Single(items)!;
        Assert.Equal(dispute.Id, item["content_report_id"]!.GetValue<Guid>());
        Assert.Equal("accepted_answer", item["field"]!.GetValue<string>());
        Assert.Null(item["previous_value"]);
        Assert.Equal(expectedValue, item["new_value"]!.GetValue<string>());
        Assert.True(item["rollback_allowed"]!.GetValue<bool>());
        Assert.True(await Db.SearchForms.AnyAsync(row => row.VersionId == publishedId && row.FormDisplay == expectedValue));

        using var rollback = await RollbackAsync(app, change.Id);
        await AssertResponseAsync(rollback, HttpStatusCode.OK);
        Assert.Equal(root.Version.Id, await PublishedAsync(root.Entry.Id));
        Assert.Equal(new[] { "żaba" }, await Db.VersionAcceptedAnswers.Where(row => row.VersionId == root.Version.Id)
            .Select(row => row.TextPl).ToArrayAsync());
        Assert.True(await Db.VersionAcceptedAnswers.AnyAsync(row => row.VersionId == publishedId && row.TextPl == expectedValue));
        Assert.False(await Db.SearchForms.AnyAsync(row => row.FormDisplay == expectedValue));
        Assert.Equal(history, await HistoryAsync());
        Assert.Equal(versions, await VersionsAsync());

        using var refreshed = await SendAsync(app, HttpMethod.Get, Path);
        await AssertResponseAsync(refreshed, HttpStatusCode.OK, list: true);
        var rolledBackItem = Assert.Single((await JsonAsync(refreshed))["items"]!.AsArray())!;
        Assert.Equal(expectedValue, rolledBackItem["new_value"]!.GetValue<string>());
        Assert.False(rolledBackItem["rollback_allowed"]!.GetValue<bool>());
    }

    [DockerFact]
    public async Task Ac50_RealAuthenticationAndEditorPolicy_ProtectBothEndpoints()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var change = await PublishAsync(app, root, "translation", "ропуха");
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Post })
        {
            var path = method == HttpMethod.Get ? Path : $"{Path}/{change.Id}/rollback";
            using var anonymous = await app.Client.SendAsync(new HttpRequestMessage(method, path));
            await AssertResponseAsync(anonymous, HttpStatusCode.Unauthorized, list: method == HttpMethod.Get);
            using var learner = await SendAsync(app, method, path, telegramId: LearnerTelegramId);
            await AssertResponseAsync(learner, HttpStatusCode.Forbidden, "editor.role_required", method == HttpMethod.Get);
            using var forgedRequest = new HttpRequestMessage(method, path);
            forgedRequest.Headers.Authorization = new("tma", new TelegramInitDataSigner("wrong-token").Sign(EditorTelegramId, Clock.GetCurrentInstant()));
            using var forged = await app.Client.SendAsync(forgedRequest);
            await AssertResponseAsync(forged, HttpStatusCode.Unauthorized, list: method == HttpMethod.Get);
        }
        Assert.Equal(change.ToVersionId, await PublishedAsync(root.Entry.Id));
        Assert.False(await Db.PublicationLog.AnyAsync(row => row.Action == "rollback"));
    }

    [DockerFact]
    public async Task List_UsesStableBidirectionalCursorsAndFiltersNonReportPublications()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var changes = new List<PublicationLog>();
        for (var index = 0; index < 4; index++)
            changes.Add(await PublishAsync(app, root, "translation", "жаба" + index));
        var ids = changes.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id).Select(row => row.Id).ToArray();
        using var first = await SendAsync(app, HttpMethod.Get, Path + "?limit=2");
        await AssertResponseAsync(first, HttpStatusCode.OK, list: true);
        var page = await JsonAsync(first);
        Assert.Equal(ids[..2], page["items"]!.AsArray().Select(row => row!["id"]!.GetValue<Guid>()));
        Assert.False(page["has_prev"]!.GetValue<bool>());
        Assert.True(page["has_next"]!.GetValue<bool>());
        Assert.Equal(ids[1], page["next_cursor"]!.GetValue<Guid>());
        using var rollback = await RollbackAsync(app, ids[0]);
        await AssertResponseAsync(rollback, HttpStatusCode.OK);
        using var next = await SendAsync(app, HttpMethod.Get, Path + "?limit=2&after=" + ids[1]);
        await AssertResponseAsync(next, HttpStatusCode.OK, list: true);
        page = await JsonAsync(next);
        Assert.Equal(ids[2..], page["items"]!.AsArray().Select(row => row!["id"]!.GetValue<Guid>()));
        Assert.True(page["has_prev"]!.GetValue<bool>());
        Assert.False(page["has_next"]!.GetValue<bool>());
        Assert.Null(page["next_cursor"]);
        using var previous = await SendAsync(app, HttpMethod.Get, Path + "?limit=2&before=" + ids[2]);
        await AssertResponseAsync(previous, HttpStatusCode.OK, list: true);
        Assert.Equal(ids[..2], (await JsonAsync(previous))["items"]!.AsArray().Select(row => row!["id"]!.GetValue<Guid>()));
        // Навіть editor-публікація без content report не належить до списку.
        await Db.PublicationLog.Where(row => row.ActorKind == "import").ExecuteUpdateAsync(update => update.SetProperty(row => row.ActorKind, "editor"));
        using var all = await SendAsync(app, HttpMethod.Get, Path);
        Assert.Equal(4, (await JsonAsync(all))["items"]!.AsArray().Count);
    }

    [DockerFact]
    public async Task ParallelRollbacks_ApplyExactlyOnce()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var change = await PublishAsync(app, root, "translation", "ропуха");
        var responses = await Task.WhenAll(RollbackAsync(app, change.Id), RollbackAsync(app, change.Id));
        try
        {
            Assert.Single(responses, row => row.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, row => row.StatusCode == HttpStatusCode.Conflict);
            foreach (var response in responses) await ContractAssert.ResponseAsync("rollbackAppliedChange", response);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.Single(await Db.PublicationLog.Where(row => row.Action == "rollback").ToArrayAsync());
        Assert.Equal(root.Version.Id, await PublishedAsync(root.Entry.Id));
    }

    [DockerFact]
    public async Task ConcurrentEditorPublicationAndRollback_RejectOneAndKeepProjectionConsistent()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var change = await PublishAsync(app, root, "translation", "ропуха");
        var report = await AddReportAsync(root, "translation", "жабеня");
        var rollbackTask = RollbackAsync(app, change.Id);
        var publishTask = DecideAsync(app, report.Id);
        await Task.WhenAll(rollbackTask, publishTask);
        using var rollback = await rollbackTask;
        using var publish = await publishTask;
        Assert.True((rollback.StatusCode == HttpStatusCode.OK && publish.StatusCode == HttpStatusCode.BadRequest)
            || (rollback.StatusCode == HttpStatusCode.Conflict && publish.StatusCode == HttpStatusCode.OK));
        await ContractAssert.ResponseAsync("rollbackAppliedChange", rollback);
        await ContractAssert.ResponseAsync("decideContentReport", publish);
        var publishedId = await PublishedAsync(root.Entry.Id);
        Assert.All(await Db.SearchForms.ToArrayAsync(), row => Assert.Equal(publishedId, row.VersionId));
        Assert.Equal(3, await Db.PublicationLog.CountAsync());
    }

    [DockerFact]
    public async Task ProjectionFailure_RollsBackPointerLogAndSearchAtomically()
    {
        var root = await SeedAsync();
        await using var normal = await StartAsync();
        var change = await PublishAsync(normal, root, "translation", "ропуха");
        var forms = await Db.SearchForms.AsNoTracking().ToArrayAsync();
        var history = await HistoryAsync();
        await using var failing = await StartAsync(services => services.AddScoped<IPublicationProjection, FailingProjection>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => RollbackAsync(failing, change.Id));
        Assert.Equal(change.ToVersionId, await PublishedAsync(root.Entry.Id));
        Assert.Equal(2, await Db.PublicationLog.CountAsync());
        (await Db.SearchForms.AsNoTracking().ToArrayAsync()).Should().BeEquivalentTo(forms);
        Assert.Equal(history, await HistoryAsync());
    }

    [RollbackDockerTheory]
    [InlineData("unapproved")]
    [InlineData("no_previous")]
    [InlineData("unpublished")]
    public async Task IneligiblePreviousOrCurrentVersion_DoesNotPublish(string condition)
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var change = await PublishAsync(app, root, "translation", "ропуха");
        if (condition == "unapproved") await Db.EntryVersions.Where(row => row.Id == root.Version.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.ValidationStatus, "needs_review"));
        if (condition == "no_previous") await Db.PublicationLog.Where(row => row.Id == change.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.FromVersionId, (Guid?)null));
        if (condition == "unpublished") await Db.DictionaryEntries.Where(row => row.Id == root.Entry.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.PublishedVersionId, (Guid?)null));
        using var list = await SendAsync(app, HttpMethod.Get, Path);
        Assert.False((await JsonAsync(list))["items"]![0]!["rollback_allowed"]!.GetValue<bool>());
        using var response = await RollbackAsync(app, change.Id);
        await AssertResponseAsync(response, HttpStatusCode.Conflict, "editor.rollback_not_current");
        Assert.False(await Db.PublicationLog.AnyAsync(row => row.Action == "rollback"));
    }

    [DockerFact]
    public async Task MissingIneligibleAndInvalidRequests_ReturnErrorsWithoutMutation()
    {
        var root = await SeedAsync();
        await using var app = await StartAsync();
        var importId = await Db.PublicationLog.Select(row => row.Id).SingleAsync();
        foreach (var id in new[] { Guid.NewGuid().ToString(), importId.ToString(), "bad-id" })
        {
            using var response = await SendAsync(app, HttpMethod.Post, $"{Path}/{id}/rollback");
            await AssertResponseAsync(response, HttpStatusCode.NotFound, "editor.change_not_found");
        }
        foreach (var query in new[] { "limit=0", "limit=101", "limit=bad", "limit=1&limit=2", "unknown=1", "after=bad",
            "after=" + Guid.NewGuid(), $"after={Guid.NewGuid()}&before={Guid.NewGuid()}" })
        {
            using var response = await SendAsync(app, HttpMethod.Get, Path + "?" + query);
            await AssertResponseAsync(response, HttpStatusCode.BadRequest, "editor.invalid_request", list: true);
        }
        using var empty = await SendAsync(app, HttpMethod.Get, Path);
        await AssertResponseAsync(empty, HttpStatusCode.OK, list: true);
        Assert.Empty((await JsonAsync(empty))["items"]!.AsArray());
        Assert.Equal(root.Version.Id, await PublishedAsync(root.Entry.Id));
    }

    private async Task<Root> SeedAsync()
    {
        using var bundle = new DictionaryImportTestBundle();
        await bundle.WriteAsync();
        await new DictionaryImportService(Db, new DictionaryImportReader(),
            new ContentPublicationService(Db, new SearchFormsProjection(Db, Clock), Clock), Clock,
            NullLogger<DictionaryImportService>.Instance).ImportAsync(bundle.DirectoryPath);
        var learner = new Learner { TelegramUserId = LearnerTelegramId, TimeZone = "Europe/Warsaw", DailyNewWordsLimit = 5 };
        var editor = new Learner { TelegramUserId = EditorTelegramId, TimeZone = "Europe/Warsaw", DailyNewWordsLimit = 5 };
        var entry = await Db.DictionaryEntries.AsNoTracking().SingleAsync();
        var version = await Db.EntryVersions.AsNoTracking().SingleAsync();
        Db.AddRange(learner, editor, new LearnerWord
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            Status = "recalled",
            ModelState = "{\"n\":7}",
            SchedulerVersion = "test-v1",
            IntervalDays = 7,
            ConsecutiveScheduledCorrect = 2,
            DueOn = new LocalDate(2026, 11, 7)
        }, new AnswerEvent
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            VersionId = version.Id,
            ExerciseKind = "review",
            Outcome = "mistake_typo",
            RawAnswer = "ropucha",
            NormalizedAnswer = "ropucha",
            AnsweredAt = Clock.GetCurrentInstant(),
            LearnerDay = new LocalDate(2026, 10, 31),
            TimeZone = "Europe/Warsaw",
            SchedulerVersion = "test-v1",
            StateBefore = "{}"
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return new(learner, editor, entry, version);
    }

    private async Task<ContentReport> AddReportAsync(Root root, string field, string value)
    {
        var report = new ContentReport
        {
            LearnerId = root.Learner.Id,
            EntryId = root.Entry.Id,
            VersionId = await PublishedAsync(root.Entry.Id),
            Origin = "card",
            Field = field,
            ProposedValue = value,
            Status = "in_editor_queue",
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.ContentReports.Add(report);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return report;
    }

    private async Task<PublicationLog> PublishAsync(ReportsTestApplication app, Root root, string field, string value)
    {
        var report = await AddReportAsync(root, field, value);
        using var response = await DecideAsync(app, report.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var versionId = await Db.ContentReports.Where(row => row.Id == report.Id).Select(row => row.ResultingVersionId).SingleAsync();
        return await Db.PublicationLog.AsNoTracking().SingleAsync(row => row.ToVersionId == versionId);
    }

    private Task<Guid?> PublishedAsync(Guid entryId) => Db.DictionaryEntries.Where(row => row.Id == entryId)
        .Select(row => row.PublishedVersionId).SingleAsync();

    // PostgreSQL зберігає в знімку всі поля, включно з датами, без втрат серіалізації NodaTime.
    private Task<string> HistoryAsync() => Db.Database.SqlQueryRaw<string>("""
        SELECT jsonb_build_object(
          'reports', (SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM content_reports r),
          'words', (SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM learner_words r),
          'answers', (SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM answer_events r),
          'neutralizations', (SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM answer_neutralizations r),
          'dispatches', (SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM notification_dispatches r)
        )::text AS "Value"
        """).SingleAsync();

    private Task<string> VersionsAsync() => Db.Database.SqlQueryRaw<string>("""
        SELECT jsonb_build_object(
          'versions', (SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM entry_versions r),
          'translations', (SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM version_translations r),
          'answers', (SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM version_accepted_answers r),
          'evidence', (SELECT jsonb_agg(to_jsonb(r) ORDER BY id) FROM validation_evidence r)
        )::text AS "Value"
        """).SingleAsync();

    private Task<ReportsTestApplication> StartAsync(Action<IServiceCollection>? configure = null) =>
        ReportsTestApplication.StartAsync(_postgres.ConnectionString, Clock, services =>
        {
            services.Configure<IdentityOptions>(options => options.ContentEditorTelegramUserIds = [EditorTelegramId]);
            configure?.Invoke(services);
        });
    private Task<HttpResponseMessage> RollbackAsync(ReportsTestApplication app, Guid id) => SendAsync(app, HttpMethod.Post, $"{Path}/{id}/rollback");
    private Task<HttpResponseMessage> DecideAsync(ReportsTestApplication app, Guid id) =>
        SendAsync(app, HttpMethod.Post, $"/api/v1/editor/content-reports/{id}/decision", new { decision = "approve" });
    private async Task<HttpResponseMessage> SendAsync(ReportsTestApplication app, HttpMethod method, string path,
        object? payload = null, long telegramId = EditorTelegramId)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new("tma", new TelegramInitDataSigner().Sign(telegramId, Clock.GetCurrentInstant()));
        if (payload is not null) request.Content = JsonContent.Create(payload);
        return await app.Client.SendAsync(request);
    }
    private static async Task AssertResponseAsync(HttpResponseMessage response, HttpStatusCode status, string? code = null, bool list = false)
    {
        Assert.Equal(status, response.StatusCode);
        await ContractAssert.ResponseAsync(list ? "listAppliedChanges" : "rollbackAppliedChange", response);
        if (code is not null) Assert.Equal(code, (await JsonAsync(response))["code"]!.GetValue<string>());
    }
    private static async Task<JsonObject> JsonAsync(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    private sealed record Root(Learner Learner, Learner Editor, DictionaryEntry Entry, EntryVersion Version);
    private sealed class FailingProjection(SearchFormsProjection real) : IPublicationProjection
    {
        public async Task UpdateAsync(IReadOnlyList<PublicationChange> changes, CancellationToken cancellationToken)
        {
            await real.UpdateAsync(changes, cancellationToken);
            throw new InvalidOperationException("Тестова помилка після перебудови пошуку.");
        }
    }
}

public sealed class RollbackDockerTheoryAttribute : TheoryAttribute
{
    public RollbackDockerTheoryAttribute() =>
        Skip = DockerAvailability.GetSkipReason(DockerAvailability.IsCi, DockerAvailability.UnavailableReason);
}
