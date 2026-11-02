using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Sessions.Answer;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Sessions.Answer;

public sealed class AnswerSessionTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Ac12_ConcurrentNormalizedCorrectAnswerIsCountedOnceAndReturnsIdenticalPersistedResult()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock);
        await using var app = await StartAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => PostAsync(app, fixture, """{"answer":"  ŻABA!  ","dont_know":false}""")));
        Assert.Single(responses.Select(response => response.ToJsonString()).Distinct());
        var body = responses[0];
        Assert.Equal("correct", body["outcome"]!.GetValue<string>());
        Assert.Equal("żaba", body["correct_form"]!.GetValue<string>());
        Assert.False(body["dispute_allowed"]!.GetValue<bool>());
        Assert.Equal(fixture.Items[1].Id, body["next_item"]!["id"]!.GetValue<Guid>());
        Assert.Null(body["next_item"]!["card"]);
        Assert.False(body["session_completed"]!.GetValue<bool>());
        var answer = await Db.AnswerEvents.AsNoTracking().SingleAsync();
        Assert.Equal("żaba", answer.NormalizedAnswer);
        Assert.Equal("  ŻABA!  ", answer.RawAnswer);
        Assert.Equal(fixture.Versions[0].Id, answer.VersionId);
        Assert.Equal(fixture.Items[0].Id, answer.SessionItemId);
        var word = await Db.LearnerWords.AsNoTracking().SingleAsync(word => word.EntryId == fixture.Entries[0].Id);
        Assert.Equal(1, word.ConsecutiveScheduledCorrect);
        Assert.Equal(AnswerTestData.Day, word.LastReviewDay);
        Assert.True(word.IntervalDays > 10);
        Assert.Equal(AnswerTestData.Day.PlusDays(-10), word.FirstShownDay);
        Assert.Equal(0, word.Lapses);
        using var snapshot = JsonDocument.Parse(answer.StateBefore);
        Assert.Equal("review", snapshot.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, snapshot.RootElement.GetProperty("interval_days").GetInt32());
        var item = await Db.SessionItems.AsNoTracking().SingleAsync(item => item.Id == fixture.Items[0].Id);
        Assert.Equal("answered", item.State);
        Assert.NotNull(item.ResponsePayload);
        Assert.Equal(64, item.AnswerPayloadHash!.Length);
    }

    [DockerTheory]
    [InlineData("{\"answer\":\"zaba\",\"dont_know\":false}", "mistake_diacritic", false)]
    [InlineData("{\"answer\":\"źaba\",\"dont_know\":false}", "mistake_diacritic", false)]
    [InlineData("{\"answer\":\"żapa\",\"dont_know\":false}", "mistake_typo", true)]
    [InlineData("{\"answer\":\"будь-що\",\"dont_know\":true}", "dont_know", false)]
    public async Task Ac14To16_MistakesScheduleTomorrowAndExposeOnlyApplicableDispute(string json, string outcome, bool dispute)
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        await using var app = await StartAsync();
        var result = await PostAsync(app, fixture, json);
        Assert.Equal(outcome, result["outcome"]!.GetValue<string>());
        Assert.Equal(dispute, result["dispute_allowed"]!.GetValue<bool>());
        Assert.Equal("2026-11-01", result["next_review_on"]!.GetValue<string>());
        Assert.True(result["session_completed"]!.GetValue<bool>());
        Assert.Null(result["next_item"]);
        if (outcome != "dont_know") Assert.NotEmpty(result["diff"]!.AsArray());
        var word = await Db.LearnerWords.AsNoTracking().SingleAsync();
        Assert.Equal(1, word.Lapses);
        Assert.Equal(1, word.IntervalDays);
        Assert.Equal(0, word.ConsecutiveScheduledCorrect);
        Assert.Equal("review", word.Status);
        Assert.Empty(await Db.ContentReports.ToArrayAsync());
    }

    [DockerFact]
    public async Task ConcurrentDifferentPayloadsHaveOneWinnerAndOneAtomicAnswer()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        await using var app = await StartAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            using var content = JsonContent(index % 2 == 0
                ? """{"answer":"żaba","dont_know":false}""" : """{"answer":"żapa","dont_know":false}""");
            return await app.Client.PostAsync(Path(fixture, 0), content);
        }));
        var successes = new List<string>();
        foreach (var response in responses)
        {
            using (response)
            {
                await ContractAssert.ResponseAsync("answerSessionItem", response);
                if (response.StatusCode == HttpStatusCode.OK) successes.Add((await BodyAsync(response)).ToJsonString());
                else
                {
                    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                    Assert.Equal("answer.already_answered_differently", (await BodyAsync(response))["code"]!.GetValue<string>());
                }
            }
        }
        Assert.Equal(4, successes.Count);
        Assert.Single(successes.Distinct());
        var answer = await Db.AnswerEvents.AsNoTracking().SingleAsync();
        var word = await Db.LearnerWords.AsNoTracking().SingleAsync();
        Assert.Equal(answer.Outcome == "correct" ? 0 : 1, word.Lapses);
        Assert.Equal(answer.Outcome == "correct" ? 1 : 0, word.ConsecutiveScheduledCorrect);
    }

    [DockerFact]
    public async Task ForeignExpiredSessionAndMismatchedItemRemainNotFoundBeforeExpiryCheck()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        Db.Learners.Add(new Learner { TelegramUserId = AnswerTestData.UserId + 1, TimeZone = "Europe/Warsaw", DailyNewWordsLimit = 5, OnboardedAt = Clock.GetCurrentInstant() });
        await Db.SaveChangesAsync();
        Clock.Reset(fixture.Session!.EndsAt);
        await using var app = await StartAsync();
        Authenticate(app, AnswerTestData.UserId + 1);
        await PostAsync(app, fixture, """{"answer":"żaba","dont_know":false}""", HttpStatusCode.NotFound);
        Authenticate(app, AnswerTestData.UserId);
        using var content = JsonContent("""{"answer":"żaba","dont_know":false}""");
        using var missing = await app.Client.PostAsync($"/api/v1/sessions/{fixture.Session.Id}/items/{Guid.NewGuid()}/answer", content);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await ContractAssert.ResponseAsync("answerSessionItem", missing);
        Assert.Equal("active", (await Db.LearningSessions.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
    }

    [DockerTheory]
    [InlineData("{\"answer\":\"   \",\"dont_know\":false}", "answer.empty")]
    [InlineData("{\"dont_know\":false}", "answer.empty")]
    [InlineData("{\"answer\":\"жаба\",\"dont_know\":false}", "answer.wrong_layout")]
    [InlineData("{\"answer\":42,\"dont_know\":false}", "answer.invalid_request")]
    [InlineData("{\"answer\":\"żaba\"}", "answer.invalid_request")]
    [InlineData("{\"answer\":\"żaba\",\"dont_know\":false,\"extra\":1}", "answer.invalid_request")]
    [InlineData("{\"answer\":\"żaba\",\"dont_know\":false,\"dont_know\":true}", "answer.invalid_request")]
    [InlineData("{", "answer.invalid_request")]
    [InlineData("{\"answer\":\"ża\\u0000ba\",\"dont_know\":false}", "answer.invalid_request")]
    [InlineData("{\"answer\":\"ża\\u0000ba\",\"dont_know\":true}", "answer.invalid_request")]
    public async Task Ac13Ac57_InvalidInputDoesNotClaimCardOrWriteHistory(string json, string code)
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        var before = AnswerWordState.Snapshot(await Db.LearnerWords.SingleAsync());
        await using var app = await StartAsync();
        var result = await PostAsync(app, fixture, json, HttpStatusCode.BadRequest);
        Assert.Equal(code, result["code"]!.GetValue<string>());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Equal(before, AnswerWordState.Snapshot(await Db.LearnerWords.AsNoTracking().SingleAsync()));
        var item = await Db.SessionItems.AsNoTracking().SingleAsync();
        Assert.Equal("pending", item.State);
        Assert.Null(item.AnswerPayloadHash);
        Assert.Null(item.ResponsePayload);
    }

    [DockerFact]
    public async Task ApprovedVariantAndCyrillicLookalikesUseDomainNormalization()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        Db.VersionAcceptedAnswers.Add(new VersionAcceptedAnswer { VersionId = fixture.Versions[0].Id, TextPl = "ropucha" });
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var result = await PostAsync(app, fixture, """{"answer":"  rорuсhа ","dont_know":false}""");
        Assert.Equal("correct", result["outcome"]!.GetValue<string>());
        Assert.Equal("żaba", result["correct_form"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task ReplaySurvivesRestartAndContentChangesWithoutReevaluatingOrAdvancingNextCard()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock);
        const string json = """{"answer":"żaba","dont_know":false}""";
        JsonNode original;
        await using (var app = await StartAsync())
        {
            original = await PostAsync(app, fixture, json);
            using var continued = await app.Client.PostAsync($"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[0].Id}/continue", null);
            Assert.Equal(HttpStatusCode.OK, continued.StatusCode);
            await PostAsync(app, fixture, """{"answer":"slowo1","dont_know":false}""", itemIndex: 1);
        }
        fixture.Entries[0].PublishedVersionId = null;
        fixture.Versions[1].LemmaPl = "zmiana";
        await Db.SaveChangesAsync();
        await using var restarted = await StartAsync(new RejectContentReadInterceptor());
        var replay = await PostAsync(restarted, fixture, """{"dont_know":false,"answer":"żaba"}""");
        Assert.Equal(original.ToJsonString(), replay.ToJsonString());
        var conflict = await PostAsync(restarted, fixture, """{"answer":"ŻABA","dont_know":false}""", HttpStatusCode.Conflict);
        Assert.Equal("answer.already_answered_differently", conflict["code"]!.GetValue<string>());
        Assert.Equal(2, await Db.AnswerEvents.CountAsync());
    }

    [DockerFact]
    public async Task Ac42_CurrentPublishedVersionChangesPromptAndEvaluationWithoutResettingHistory()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        var word = await Db.LearnerWords.SingleAsync();
        word.Status = "recalled";
        word.ConsecutiveScheduledCorrect = 2;
        var replacement = ReportsPublicationTestData.NewVersion(fixture.Entries[0]);
        replacement.LemmaPl = "kot";
        Db.EntryVersions.Add(replacement);
        Db.VersionTranslations.Add(new() { VersionId = replacement.Id, Kind = "canonical", TextUk = "кіт", Position = 0 });
        await Db.SaveChangesAsync();
        fixture.Entries[0].PublishedVersionId = replacement.Id;
        await Db.SaveChangesAsync();
        var before = AnswerWordState.Snapshot(word);
        await using var app = await StartAsync();
        using var current = await app.Client.GetAsync("/api/v1/sessions/current");
        await ContractAssert.ResponseAsync("getCurrentSession", current);
        Assert.Equal("кіт", (await BodyAsync(current))["current_item"]!["prompt_uk"]!.GetValue<string>());
        Assert.Equal(before, AnswerWordState.Snapshot(await Db.LearnerWords.AsNoTracking().SingleAsync()));
        var result = await PostAsync(app, fixture, """{"answer":"kot","dont_know":false}""");
        Assert.Equal("correct", result["outcome"]!.GetValue<string>());
        Assert.Equal("recalled", result["word_status"]!.GetValue<string>());
        Assert.Equal(replacement.Id, (await Db.AnswerEvents.AsNoTracking().SingleAsync()).VersionId);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(before),
            JsonNode.Parse((await Db.AnswerEvents.AsNoTracking().SingleAsync()).StateBefore)));
    }

    [DockerFact]
    public async Task Ac33_WithdrawnPendingWordIsSkippedWithoutAnswerOrScheduleChange()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock);
        var before = AnswerWordState.Snapshot(await Db.LearnerWords.SingleAsync(word => word.EntryId == fixture.Entries[0].Id));
        fixture.Entries[0].PublishedVersionId = null;
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var result = await PostAsync(app, fixture, """{"answer":"żaba","dont_know":false}""", HttpStatusCode.Conflict);
        Assert.Equal("answer.content_unavailable", result["code"]!.GetValue<string>());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Equal(before, AnswerWordState.Snapshot(await Db.LearnerWords.AsNoTracking().SingleAsync(word => word.EntryId == fixture.Entries[0].Id)));
        var skipped = await Db.SessionItems.AsNoTracking().SingleAsync(item => item.Id == fixture.Items[0].Id);
        Assert.Equal("skipped", skipped.State);
        Assert.Null(skipped.ResponsePayload);
        using var current = await app.Client.GetAsync("/api/v1/sessions/current");
        Assert.Equal(fixture.Items[1].Id, (await BodyAsync(current))["current_item"]!["id"]!.GetValue<Guid>());
    }

    [DockerFact]
    public async Task Ac56_OnlyApprovedCanonicalSynonymIsNeutralAndNotRepeatedToday()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 2);
        var translation = await Db.VersionTranslations.SingleAsync(value => value.VersionId == fixture.Versions[1].Id);
        translation.TextUk = "підказка 0";
        await Db.SaveChangesAsync();
        var before = await Db.LearnerWords.AsNoTracking().SingleAsync(word => word.EntryId == fixture.Entries[0].Id);
        await using var app = await StartAsync();
        var result = await PostAsync(app, fixture, """{"answer":"slowo1","dont_know":false}""");
        Assert.Equal("synonym", result["outcome"]!.GetValue<string>());
        Assert.False(result["dispute_allowed"]!.GetValue<bool>());
        var after = await Db.LearnerWords.AsNoTracking().SingleAsync(word => word.EntryId == fixture.Entries[0].Id);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.DueOn, after.DueOn);
        Assert.Equal(before.IntervalDays, after.IntervalDays);
        Assert.Equal(before.LastReviewDay, after.LastReviewDay);
        Assert.Equal(before.Lapses, after.Lapses);
        Assert.Equal(AnswerTestData.Day, AnswerWordState.Read(after).LastScheduledAttemptDay);
        Assert.Empty(await Db.ContentReports.ToArrayAsync());
        using var firstContinue = await app.Client.PostAsync($"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[0].Id}/continue", null);
        Assert.Equal(HttpStatusCode.OK, firstContinue.StatusCode);
        await PostAsync(app, fixture, """{"answer":"slowo1","dont_know":false}""", itemIndex: 1);
        using var secondContinue = await app.Client.PostAsync($"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[1].Id}/continue", null);
        Assert.Equal(HttpStatusCode.OK, secondContinue.StatusCode);
        using var next = await app.Client.PostAsync("/api/v1/sessions", null);
        Assert.Equal(0, (await BodyAsync(next))["total_items"]!.GetValue<int>());
    }

    [DockerFact]
    public async Task UnapprovedSynonymDoesNotMakeUnknownAnswerNeutral()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 2);
        (await Db.VersionTranslations.SingleAsync(value => value.VersionId == fixture.Versions[1].Id)).TextUk = "підказка 0";
        fixture.Entries[1].PublishedVersionId = null;
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        Assert.Equal("mistake_typo", (await PostAsync(app, fixture, """{"answer":"slowo1","dont_know":false}"""))["outcome"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task Ac52_ExpiredActionCommitsClosureBefore409AndDoesNotCountAnyAnswer()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        var before = AnswerWordState.Snapshot(await Db.LearnerWords.SingleAsync());
        Clock.Reset(fixture.Session!.EndsAt);
        await using var app = await StartAsync();
        var result = await PostAsync(app, fixture, """{"answer":"żaba","dont_know":false}""", HttpStatusCode.Conflict);
        Assert.Equal("session.expired", result["code"]!.GetValue<string>());
        var session = await Db.LearningSessions.AsNoTracking().SingleAsync();
        Assert.Equal("incomplete", session.Status);
        Assert.Null(session.CompletedAt);
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Equal(before, AnswerWordState.Snapshot(await Db.LearnerWords.AsNoTracking().SingleAsync()));
        Assert.Equal("pending", (await Db.SessionItems.AsNoTracking().SingleAsync()).State);
    }

    [DockerFact]
    public async Task ExpiredCompletedReplayReturns409WithoutRewritingCompletedHistory()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        await using (var initial = await StartAsync()) await PostAsync(initial, fixture, """{"answer":"żaba","dont_know":false}""");
        var completed = await Db.LearningSessions.AsNoTracking().SingleAsync();
        Clock.Reset(completed.EndsAt);
        await using var restarted = await StartAsync();
        var expired = await PostAsync(restarted, fixture, """{"answer":"żaba","dont_know":false}""", HttpStatusCode.Conflict);
        Assert.Equal("session.expired", expired["code"]!.GetValue<string>());
        Assert.Equal("completed", (await Db.LearningSessions.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(completed.CompletedAt, (await Db.LearningSessions.AsNoTracking().SingleAsync()).CompletedAt);
        Assert.Single(await Db.AnswerEvents.ToArrayAsync());
    }

    [DockerFact]
    public async Task AuthenticationOwnershipOrderAndCardKindAreCheckedBeforeClaiming()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock);
        Db.Learners.Add(new Learner { TelegramUserId = AnswerTestData.UserId + 1, TimeZone = "Europe/Warsaw", DailyNewWordsLimit = 5, OnboardedAt = Clock.GetCurrentInstant() });
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        Authenticate(app, AnswerTestData.UserId + 1);
        Assert.Equal("session.item_not_found", (await PostAsync(app, fixture, """{"answer":"żaba","dont_know":false}""", HttpStatusCode.NotFound))["code"]!.GetValue<string>());
        Authenticate(app, AnswerTestData.UserId);
        var later = await PostAsync(app, fixture, """{"answer":"slowo1","dont_know":false}""", HttpStatusCode.Conflict, 1);
        Assert.Equal("answer.item_not_current", later["code"]!.GetValue<string>());
        fixture.Items[0].Kind = "new_word_card";
        await Db.SaveChangesAsync();
        Assert.Equal("answer.invalid_item_kind", (await PostAsync(app, fixture, """{"answer":"żaba","dont_know":false}""", HttpStatusCode.Conflict))["code"]!.GetValue<string>());
        app.Client.DefaultRequestHeaders.Authorization = null;
        await PostAsync(app, fixture, """{"answer":"żaba","dont_know":false}""", HttpStatusCode.Unauthorized);
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
    }

    [DockerFact]
    public async Task FailureAfterConditionalClaimRollsBackClaimProjectionEventAndResponse()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        var before = AnswerWordState.Snapshot(await Db.LearnerWords.SingleAsync());
        await using (var failed = await StartAsync(new FailAnswerInsertInterceptor()))
        {
            using var body = JsonContent("""{"answer":"żaba","dont_know":false}""");
            await Assert.ThrowsAsync<DbUpdateException>(() => failed.Client.PostAsync(Path(fixture, 0), body));
        }
        var item = await Db.SessionItems.AsNoTracking().SingleAsync();
        Assert.Equal("pending", item.State);
        Assert.Null(item.AnswerPayloadHash);
        Assert.Null(item.ResponsePayload);
        Assert.Equal(before, AnswerWordState.Snapshot(await Db.LearnerWords.AsNoTracking().SingleAsync()));
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Equal("active", (await Db.LearningSessions.AsNoTracking().SingleAsync()).Status);
        await using var retry = await StartAsync();
        await PostAsync(retry, fixture, """{"answer":"żaba","dont_know":false}""");
        Assert.Single(await Db.AnswerEvents.ToArrayAsync());
    }

    [DockerFact]
    public async Task InitialRecallRecordsOutcomeWithoutChangingScheduledState()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        fixture.Items[0].Kind = "new_word_check";
        var word = await Db.LearnerWords.SingleAsync();
        word.FirstShownDay = AnswerTestData.Day;
        word.DueOn = AnswerTestData.Day.PlusDays(1);
        word.IntervalDays = 1;
        await Db.SaveChangesAsync();
        var before = AnswerWordState.Snapshot(word);
        await using var app = await StartAsync();
        await PostAsync(app, fixture, """{"answer":"żapa","dont_know":false}""");
        Assert.Equal(before, AnswerWordState.Snapshot(await Db.LearnerWords.AsNoTracking().SingleAsync()));
        Assert.Equal("new_word_check", (await Db.AnswerEvents.AsNoTracking().SingleAsync()).ExerciseKind);
    }

    private async Task<AnswerTestApplication> StartAsync(IInterceptor? interceptor = null)
    {
        var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock, interceptor);
        Authenticate(app, AnswerTestData.UserId);
        return app;
    }

    private void Authenticate(AnswerTestApplication app, long userId) => app.Client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(userId, Clock.GetCurrentInstant()));

    private static string Path(AnswerFixture fixture, int index) => $"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[index].Id}/answer";
    private static StringContent JsonContent(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonNode> PostAsync(AnswerTestApplication app, AnswerFixture fixture, string json,
        HttpStatusCode status = HttpStatusCode.OK, int itemIndex = 0)
    {
        using var content = JsonContent(json);
        using var response = await app.Client.PostAsync(Path(fixture, itemIndex), content);
        Assert.Equal(status, response.StatusCode);
        await ContractAssert.ResponseAsync("answerSessionItem", response);
        return await BodyAsync(response);
    }

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private sealed class DockerTheoryAttribute : TheoryAttribute
    {
        public DockerTheoryAttribute() => Skip = DockerAvailability.GetSkipReason(DockerAvailability.IsCi, DockerAvailability.UnavailableReason);
    }

    private sealed class FailAnswerInsertInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO answer_events", StringComparison.Ordinal))
                throw new InvalidOperationException("Тестова відмова запису відповіді.");
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
