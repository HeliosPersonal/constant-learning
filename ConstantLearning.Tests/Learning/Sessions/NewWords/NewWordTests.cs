using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Identity.LearnerDays;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Sessions.Answer;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Identity.Settings;
using ConstantLearning.Tests.Infrastructure;
using ConstantLearning.Tests.Learning.Sessions.Answer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Sessions.NewWords;

public sealed class NewWordTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Ac11_AllNewCardsPrecedeExactlyOneCheckPerAcknowledgedWord()
    {
        var fixture = await SeedAsync(3, 3);
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        var sessionId = session["id"]!.GetValue<Guid>();
        var next = session["current_item"]!;
        using (var current = await app.Client.GetAsync("/api/v1/sessions/current"))
            await ContractAssert.ResponseAsync("getCurrentSession", current);
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());

        for (var index = 0; index < 3; index++)
        {
            Assert.Equal("new_word_card", next["kind"]!.GetValue<string>());
            Assert.NotNull(next["card"]);
            Clock.Advance(Duration.FromMinutes(1));
            var result = await ActAsync(app, sessionId, next["id"]!.GetValue<Guid>(), false);
            next = result["next_item"]!;
            Assert.False(result["session_completed"]!.GetValue<bool>());
            Assert.Equal(index + 1, await Db.LearnerWords.CountAsync());
            Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        }

        Assert.Equal("new_word_check", next["kind"]!.GetValue<string>());
        var before = await Db.LearnerWords.AsNoTracking().OrderBy(word => word.EntryId).ToArrayAsync();
        Assert.All(before, word =>
        {
            Assert.Equal(AnswerTestData.Day, word.FirstShownDay);
            Assert.Equal(AnswerTestData.Day.PlusDays(1), word.DueOn);
            Assert.Equal(1, word.IntervalDays);
            Assert.Equal("review", word.Status);
            Assert.Equal(0, word.ConsecutiveScheduledCorrect);
            Assert.False(word.ControlPending);
        });
        for (var index = 0; index < 3; index++)
        {
            Assert.Null(next["card"]);
            Assert.False(next.AsObject().ContainsKey("pronunciation_uk"));
            var entryId = (await Db.SessionItems.AsNoTracking().SingleAsync(item => item.Id == next["id"]!.GetValue<Guid>())).EntryId;
            var answer = fixture.Versions.Single(version => version.EntryId == entryId).LemmaPl;
            var result = await AnswerAsync(app, sessionId, next["id"]!.GetValue<Guid>(), answer);
            Assert.Equal(index == 2, result["session_completed"]!.GetValue<bool>());
            next = result["next_item"]!;
        }
        Assert.Null(next);
        Assert.Equal(before.Select(AnswerWordState.Snapshot), (await Db.LearnerWords.AsNoTracking()
            .OrderBy(word => word.EntryId).ToArrayAsync()).Select(AnswerWordState.Snapshot));
        Assert.Equal(3, await Db.AnswerEvents.CountAsync());
        Assert.Equal(6, await Db.SessionItems.CountAsync());
        Assert.All(await Db.SessionItems.AsNoTracking().ToArrayAsync(), item => Assert.NotNull(item.AnsweredAt));
        Assert.Equal(Clock.GetCurrentInstant(), (await Db.LearningSessions.AsNoTracking().SingleAsync()).LastActivityAt);
    }

    [DockerFact]
    public async Task Ac20Ac21_FirstTenKnownAreFreeAndEleventhConsumesQuotaWithoutReplacementInLaterSession()
    {
        await SeedAsync(14, 1);
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        var sessionId = session["id"]!.GetValue<Guid>();
        var next = session["current_item"]!;
        for (var count = 1; count <= 11; count++)
        {
            if (count == 11)
            {
                Clock.Advance(Duration.FromHours(2));
                await ChangeTimeZoneAsync("Pacific/Kiritimati");
            }
            var result = await ActAsync(app, sessionId, next["id"]!.GetValue<Guid>(), true);
            Assert.Equal(count <= 10, result["replacement_added"]!.GetValue<bool>());
            Assert.Equal("2026-11-07", result["control_repetition_on"]!.GetValue<string>());
            Assert.Equal(count == 11, result["session_completed"]!.GetValue<bool>());
            next = result["next_item"]!;
        }
        Assert.Null(next);
        Assert.Equal(11, await Db.AnswerEvents.CountAsync());
        Assert.Equal(11, await Db.LearnerWords.CountAsync());
        Assert.DoesNotContain(await Db.SessionItems.ToArrayAsync(), item => item.Kind == "new_word_check");
        Assert.All(await Db.LearnerWords.AsNoTracking().ToArrayAsync(), word =>
        {
            Assert.Equal("review", word.Status);
            Assert.True(word.ControlPending);
            Assert.Equal(7, word.IntervalDays);
            Assert.Equal(0, word.ConsecutiveScheduledCorrect);
            Assert.Equal(AnswerTestData.Day, word.FirstShownDay);
        });
        var later = await StartSessionAsync(app);
        Assert.Equal(0, later["total_items"]!.GetValue<int>());
        Assert.Contains("new_words_limit_reached", later["notices"]!.AsArray().Select(value => value!.GetValue<string>()));
        Clock.Advance(Duration.FromDays(1));
        var tomorrow = await StartSessionAsync(app);
        Assert.Equal(1, tomorrow["total_items"]!.GetValue<int>());
        Assert.Equal("new_word_card", tomorrow["current_item"]!["kind"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task TwentyWordSlotsAllowKnownReplacementsAndChecksBeyondTwentyPhysicalItems()
    {
        await SeedAsync(30, 20);
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        var sessionId = session["id"]!.GetValue<Guid>();
        var next = session["current_item"]!;
        for (var index = 0; index < 30; index++)
        {
            Assert.Equal("new_word_card", next["kind"]!.GetValue<string>());
            var result = await ActAsync(app, sessionId, next["id"]!.GetValue<Guid>(), index < 10);
            next = result["next_item"]!;
        }
        Assert.Equal("new_word_check", next["kind"]!.GetValue<string>());
        var items = await Db.SessionItems.AsNoTracking().ToArrayAsync();
        Assert.Equal(50, items.Length);
        Assert.Equal(20, items.Count(item => item.Kind == "new_word_check"));
        Assert.Equal(30, items.Where(item => item.Kind == "new_word_card").Select(item => item.EntryId).Distinct().Count());
        Assert.True(items.Where(item => item.Kind == "new_word_card").Max(item => item.Position)
            < items.Where(item => item.Kind == "new_word_check").Min(item => item.Position));
        using var current = await app.Client.GetAsync("/api/v1/sessions/current");
        await ContractAssert.ResponseAsync("getCurrentSession", current);
        Assert.Equal(50, (await BodyAsync(current))["total_items"]!.GetValue<int>());
        Assert.Equal(10, await Db.AnswerEvents.CountAsync());
    }

    [DockerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameActionConcurrencyAndRestartReplayHaveOneAtomicEffect(bool known)
    {
        var fixture = await SeedAsync(3, 1);
        JsonNode original;
        Guid sessionId;
        Guid itemId;
        await using (var app = await StartAsync())
        {
            var session = await StartSessionAsync(app);
            sessionId = session["id"]!.GetValue<Guid>();
            itemId = session["current_item"]!["id"]!.GetValue<Guid>();
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => ActAsync(app, sessionId, itemId, known)));
            Assert.Single(results.Select(value => value.ToJsonString()).Distinct());
            original = results[0];
        }
        Assert.Single(await Db.LearnerWords.ToArrayAsync());
        Assert.Equal(known ? 1 : 0, await Db.AnswerEvents.CountAsync());
        Assert.Equal(2, await Db.SessionItems.CountAsync());
        fixture.Entries[0].PublishedVersionId = null;
        await Db.SaveChangesAsync();
        await using var restarted = await StartAsync(new RejectContentReadInterceptor());
        var replay = await ActAsync(restarted, sessionId, itemId, known);
        Assert.True(JsonNode.DeepEquals(original, replay));
        var conflict = await ActAsync(restarted, sessionId, itemId, !known, HttpStatusCode.Conflict);
        Assert.Equal("new_word.already_processed_differently", conflict["code"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task DifferentConcurrentActionsHaveOnlyOneWinner()
    {
        await SeedAsync(3, 1);
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        var sessionId = session["id"]!.GetValue<Guid>();
        var itemId = session["current_item"]!["id"]!.GetValue<Guid>();
        var requests = Enumerable.Range(0, 8).Select(index => app.Client.PostAsync(Path(sessionId, itemId, index % 2 == 0), null));
        var responses = await Task.WhenAll(requests);
        try
        {
            Assert.Equal(4, responses.Count(value => value.StatusCode == HttpStatusCode.OK));
            Assert.Equal(4, responses.Count(value => value.StatusCode == HttpStatusCode.Conflict));
            for (var index = 0; index < responses.Length; index++)
                await ContractAssert.ResponseAsync(index % 2 == 0 ? "markAlreadyKnown" : "acknowledgeNewWordCard", responses[index]);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        var word = await Db.LearnerWords.AsNoTracking().SingleAsync();
        Assert.Equal(word.ControlPending ? 1 : 0, await Db.AnswerEvents.CountAsync());
        Assert.Equal(2, await Db.SessionItems.CountAsync());
        Assert.Equal(word.ControlPending ? "new_word_card" : "new_word_check", (await Db.SessionItems.AsNoTracking()
            .SingleAsync(value => value.State == "pending")).Kind);
    }

    [DockerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ac52_OwnershipPrecedesExpiryAndLateActionCommitsClosureWithoutShowingWord(bool known)
    {
        await SeedAsync(2, 1);
        Db.Learners.Add(new Learner
        {
            TelegramUserId = AnswerTestData.UserId + 1,
            TimeZone = "Europe/Warsaw",
            DailyNewWordsLimit = 1,
            OnboardedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        var sessionId = session["id"]!.GetValue<Guid>();
        var itemId = session["current_item"]!["id"]!.GetValue<Guid>();
        var stored = await Db.LearningSessions.AsNoTracking().SingleAsync();
        Clock.Reset(stored.EndsAt);
        Authenticate(app, AnswerTestData.UserId + 1);
        await ActAsync(app, sessionId, itemId, known, HttpStatusCode.NotFound, authenticate: false);
        Assert.Equal("active", (await Db.LearningSessions.AsNoTracking().SingleAsync()).Status);
        var expired = await ActAsync(app, sessionId, itemId, known, HttpStatusCode.Conflict);
        Assert.Equal("session.expired", expired["code"]!.GetValue<string>());
        Assert.Equal("incomplete", (await Db.LearningSessions.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Equal("pending", (await Db.SessionItems.AsNoTracking().SingleAsync()).State);
    }

    [DockerFact]
    public async Task WithdrawalOfLastUnshownCardKeepsEarlierAcknowledgedCheckAvailableToCurrentReader()
    {
        var fixture = await SeedAsync(2, 2);
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        var sessionId = session["id"]!.GetValue<Guid>();
        await ActAsync(app, sessionId, session["current_item"]!["id"]!.GetValue<Guid>(), false);
        fixture.Entries[1].PublishedVersionId = null;
        await Db.SaveChangesAsync();
        using var current = await app.Client.GetAsync("/api/v1/sessions/current");
        await ContractAssert.ResponseAsync("getCurrentSession", current);
        Assert.Equal("new_word_check", (await BodyAsync(current))["current_item"]!["kind"]!.GetValue<string>());
        Assert.Single(await Db.LearnerWords.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Equal("skipped", (await Db.SessionItems.AsNoTracking().SingleAsync(item => item.EntryId == fixture.Entries[1].Id)).State);
    }

    [DockerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureAfterClaimRollsBackWordEventFollowupAndClaim(bool known)
    {
        await SeedAsync(2, 1);
        Guid sessionId;
        Guid itemId;
        await using (var initial = await StartAsync())
        {
            var session = await StartSessionAsync(initial);
            sessionId = session["id"]!.GetValue<Guid>();
            itemId = session["current_item"]!["id"]!.GetValue<Guid>();
        }
        await using (var failed = await StartAsync(new FailFollowupInsertInterceptor()))
            await Assert.ThrowsAsync<DbUpdateException>(() => failed.Client.PostAsync(Path(sessionId, itemId, known), null));
        var item = await Db.SessionItems.AsNoTracking().SingleAsync();
        Assert.Equal("pending", item.State);
        Assert.Null(item.AnsweredAt);
        Assert.Null(item.AnswerPayloadHash);
        Assert.Null(item.ResponsePayload);
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        await using var retry = await StartAsync();
        await ActAsync(retry, sessionId, itemId, known);
        Assert.Single(await Db.LearnerWords.ToArrayAsync());
    }

    [DockerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedTimeZonePreservesOriginalDayAndSessionBoundary(bool known)
    {
        await SeedAsync(3, 1);
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        var sessionId = session["id"]!.GetValue<Guid>();
        var itemId = session["current_item"]!["id"]!.GetValue<Guid>();
        var original = await Db.LearningSessions.AsNoTracking().SingleAsync();
        Clock.Advance(Duration.FromHours(2));
        await ChangeTimeZoneAsync("Pacific/Kiritimati");
        await ActAsync(app, sessionId, itemId, known);
        var word = await Db.LearnerWords.AsNoTracking().SingleAsync();
        Assert.Equal(original.LearnerDay, word.FirstShownDay);
        Assert.Equal(original.LearnerDay.PlusDays(known ? 7 : 1), word.DueOn);
        Assert.Equal(original.EndsAt, (await Db.LearningSessions.AsNoTracking().SingleAsync()).EndsAt);
        Clock.Reset(original.EndsAt);
        var expired = await ActAsync(app, sessionId, itemId, known, HttpStatusCode.Conflict);
        Assert.Equal("session.expired", expired["code"]!.GetValue<string>());
        Assert.Single(await Db.LearnerWords.ToArrayAsync());
    }

    [DockerFact]
    public async Task ReplacementUsesCurrentApprovedQuotaAndDoesNotDuplicateReservedCards()
    {
        var fixture = await SeedAsync(10, 2);
        fixture.Entries[8].Domain = "legalization";
        fixture.Entries[9].Domain = "legalization";
        fixture.Versions[8].UtilityRank = 1;
        fixture.Versions[9].UtilityRank = 2;
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        var sessionId = session["id"]!.GetValue<Guid>();
        var initial = await Db.SessionItems.AsNoTracking().OrderBy(item => item.Position).ToArrayAsync();
        Assert.Equal(new[] { fixture.Entries[0].Id, fixture.Entries[8].Id }, initial.Select(item => item.EntryId));
        await ActAsync(app, sessionId, initial[0].Id, true);
        var replaced = await Db.SessionItems.AsNoTracking().OrderBy(item => item.Position).ToArrayAsync();
        Assert.Equal(fixture.Entries[1].Id, replaced[2].EntryId);
        Assert.Equal(3, replaced.Select(item => item.EntryId).Distinct().Count());
        var known = await Db.AnswerEvents.AsNoTracking().SingleAsync();
        Assert.Equal(fixture.Versions[0].Id, known.VersionId);
        Assert.Equal("already_known", known.Outcome);
        Assert.Equal("not_started", JsonNode.Parse(known.StateBefore)!["status"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task ExhaustedCourseAllowsKnownWithoutReplacementAndNextStartReportsCompletion()
    {
        await SeedAsync(1, 1);
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        var known = await ActAsync(app, session["id"]!.GetValue<Guid>(), session["current_item"]!["id"]!.GetValue<Guid>(), true);
        Assert.False(known["replacement_added"]!.GetValue<bool>());
        Assert.True(known["session_completed"]!.GetValue<bool>());
        Assert.Null(known["next_item"]);
        var empty = await StartSessionAsync(app);
        Assert.Equal(0, empty["total_items"]!.GetValue<int>());
        Assert.Contains("course_completed", empty["notices"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Single(await Db.AnswerEvents.ToArrayAsync());
    }

    [DockerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableNewPortionCannotBeShownByAnOldPlannedCard(bool known)
    {
        await SeedAsync(2, 1);
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        Db.ChangeTracker.Clear();
        var learner = await Db.Learners.SingleAsync();
        learner.NewWordsAvailableFrom = Clock.GetCurrentInstant() + Duration.FromDays(1);
        await Db.SaveChangesAsync();
        var result = await ActAsync(app, session["id"]!.GetValue<Guid>(), session["current_item"]!["id"]!.GetValue<Guid>(),
            known, HttpStatusCode.Conflict);
        Assert.Equal("new_word.limit_reached", result["code"]!.GetValue<string>());
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Equal("pending", (await Db.SessionItems.AsNoTracking().SingleAsync()).State);
    }

    [DockerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithdrawnCardIsSkippedWithoutShowingOrCountingAnAnswer(bool known)
    {
        var fixture = await SeedAsync(2, 2);
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        fixture.Entries[0].PublishedVersionId = null;
        await Db.SaveChangesAsync();
        var result = await ActAsync(app, session["id"]!.GetValue<Guid>(), session["current_item"]!["id"]!.GetValue<Guid>(),
            known, HttpStatusCode.Conflict);
        Assert.Equal("new_word.content_unavailable", result["code"]!.GetValue<string>());
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Equal("skipped", (await Db.SessionItems.AsNoTracking().SingleAsync(item => item.EntryId == fixture.Entries[0].Id)).State);
    }

    [DockerFact]
    public async Task AuthenticationOrderAndWrongCardKindCannotClaimNewWord()
    {
        await SeedAsync(2, 2);
        await using var app = await StartAsync();
        var session = await StartSessionAsync(app);
        var sessionId = session["id"]!.GetValue<Guid>();
        var items = await Db.SessionItems.OrderBy(item => item.Position).ToArrayAsync();
        var order = await ActAsync(app, sessionId, items[1].Id, false, HttpStatusCode.Conflict);
        Assert.Equal("new_word.item_not_current", order["code"]!.GetValue<string>());
        items[0].Kind = "review";
        await Db.SaveChangesAsync();
        var kind = await ActAsync(app, sessionId, items[0].Id, true, HttpStatusCode.Conflict);
        Assert.Equal("new_word.invalid_item_kind", kind["code"]!.GetValue<string>());
        app.Client.DefaultRequestHeaders.Authorization = null;
        await ActAsync(app, sessionId, items[0].Id, false, HttpStatusCode.Unauthorized, authenticate: false);
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
    }

    private async Task ChangeTimeZoneAsync(string timeZone)
    {
        await using var settings = await SettingsTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        await using var scope = settings.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var days = scope.ServiceProvider.GetRequiredService<ILearnerDayStateService>();
        var learnerId = await Db.Learners.Select(value => value.Id).SingleAsync();
        var current = (await days.GetForUpdateAsync(learnerId))!;
        days.ChangeTimeZone(current, timeZone);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task<AnswerFixture> SeedAsync(int count, int limit)
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, count, session: false);
        Db.LearnerWords.RemoveRange(await Db.LearnerWords.ToArrayAsync());
        fixture.Learner.DailyNewWordsLimit = (short)limit;
        await Db.SaveChangesAsync();
        return fixture;
    }

    private async Task<AnswerTestApplication> StartAsync(IInterceptor? interceptor = null)
    {
        var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock, interceptor);
        Authenticate(app);
        return app;
    }

    private void Authenticate(AnswerTestApplication app, long userId = AnswerTestData.UserId) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(userId, Clock.GetCurrentInstant()));

    private async Task<JsonNode> StartSessionAsync(AnswerTestApplication app)
    {
        Authenticate(app);
        using var response = await app.Client.PostAsync("/api/v1/sessions", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await ContractAssert.ResponseAsync("startSession", response);
        return await BodyAsync(response);
    }

    private async Task<JsonNode> ActAsync(AnswerTestApplication app, Guid sessionId, Guid itemId, bool known,
        HttpStatusCode status = HttpStatusCode.OK, bool authenticate = true)
    {
        if (authenticate) Authenticate(app);
        using var response = await app.Client.PostAsync(Path(sessionId, itemId, known), null);
        Assert.Equal(status, response.StatusCode);
        await ContractAssert.ResponseAsync(known ? "markAlreadyKnown" : "acknowledgeNewWordCard", response);
        return await BodyAsync(response);
    }

    private static async Task<JsonNode> AnswerAsync(AnswerTestApplication app, Guid sessionId, Guid itemId, string answer)
    {
        using var response = await app.Client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/items/{itemId}/answer", new { answer, dont_know = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("answerSessionItem", response);
        return await BodyAsync(response);
    }

    private static string Path(Guid sessionId, Guid itemId, bool known) =>
        $"/api/v1/sessions/{sessionId}/items/{itemId}/{(known ? "already-known" : "acknowledge")}";

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private sealed class DockerTheoryAttribute : TheoryAttribute
    {
        public DockerTheoryAttribute() => Skip = DockerAvailability.GetSkipReason(DockerAvailability.IsCi, DockerAvailability.UnavailableReason);
    }

    private sealed class FailFollowupInsertInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO session_items", StringComparison.Ordinal))
                throw new InvalidOperationException("Тестова відмова запису наступної картки.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RejectContentReadInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM dictionary_entries", StringComparison.Ordinal))
                throw new InvalidOperationException("Повтор не повинен перечитувати Content.");
            return ValueTask.FromResult(result);
        }
    }
}
