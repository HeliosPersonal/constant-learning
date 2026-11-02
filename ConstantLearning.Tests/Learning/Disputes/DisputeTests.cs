using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Sessions.Answer;
using ConstantLearning.Tests.Infrastructure;
using ConstantLearning.Tests.Learning.Sessions.Answer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Disputes;

public sealed class DisputeTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task ResultSurvivesRestartAndDisputeRestoresStateUntilExplicitContinue()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 2);
        var initialWord = await Db.LearnerWords.SingleAsync(w => w.EntryId == fixture.Entries[0].Id);
        initialWord.Status = "recalled";
        initialWord.ConsecutiveScheduledCorrect = 2;
        initialWord.LastReviewDay = AnswerTestData.Day.PlusDays(-3);
        initialWord.Lapses = 4;
        await Db.SaveChangesAsync();
        var expected = JsonNode.Parse(AnswerWordState.Snapshot(initialWord))!;
        expected["model_state"]!["last_scheduled_attempt_day"] = "2026-10-31";
        var sessionId = fixture.Session!.Id;
        var itemId = fixture.Items[0].Id;
        Guid answerId;
        await using (var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock))
        {
            Authenticate(app);
            using var answer = await app.Client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/items/{itemId}/answer", new { answer = "żapa", dont_know = false });
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            await ContractAssert.ResponseAsync("answerSessionItem", answer);
            answerId = (await Body(answer))["answer_event_id"]!.GetValue<Guid>();
            using var blocked = await app.Client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/items/{fixture.Items[1].Id}/answer", new { answer = "slowo1", dont_know = false });
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        }
        await using var restarted = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(restarted);
        using (var current = await restarted.Client.GetAsync("/api/v1/sessions/current"))
        {
            await ContractAssert.ResponseAsync("getCurrentSession", current);
            Assert.Equal(itemId, (await Body(current))["last_result"]!["item_id"]!.GetValue<Guid>());
        }
        var requests = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => restarted.Client.PostAsync($"/api/v1/answers/{answerId}/dispute", null)));
        var payloads = new List<string>();
        foreach (var response in requests)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                await ContractAssert.ResponseAsync("disputeAnswer", response);
                payloads.Add((await Body(response)).ToJsonString());
            }
        }
        Assert.Single(payloads.Distinct());
        Assert.Single(await Db.AnswerNeutralizations.ToArrayAsync());
        Assert.Single(await Db.ContentReports.ToArrayAsync());
        var word = await Db.LearnerWords.AsNoTracking().SingleAsync(w => w.EntryId == fixture.Entries[0].Id);
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(AnswerWordState.Snapshot(word))));
        var eventAfter = await Db.AnswerEvents.AsNoTracking().SingleAsync();
        Assert.Equal("mistake_typo", eventAfter.Outcome);
        Assert.Equal("żapa", eventAfter.RawAnswer);
        Assert.Equal("recalled", JsonNode.Parse(eventAfter.StateBefore)!["status"]!.GetValue<string>());
        using (var current = await restarted.Client.GetAsync("/api/v1/sessions/current"))
            Assert.Equal("neutralized", (await Body(current))["last_result"]!["dispute"]!["outcome"]!.GetValue<string>());
        using (var next = await restarted.Client.PostAsync($"/api/v1/sessions/{sessionId}/items/{itemId}/continue", null))
        {
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
            await ContractAssert.ResponseAsync("continueSession", next);
            Assert.Null((await Body(next))["last_result"]);
        }
    }

    [DockerFact]
    public async Task ContinueClosesWindowEvenBeforeNextAnswer()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 2);
        await using var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app);
        var path = $"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[0].Id}";
        using var answer = await app.Client.PostAsJsonAsync(path + "/answer", new { answer = "żapa", dont_know = false });
        var id = (await Body(answer))["answer_event_id"]!.GetValue<Guid>();
        using var next = await app.Client.PostAsync(path + "/continue", null);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        using var dispute = await app.Client.PostAsync($"/api/v1/answers/{id}/dispute", null);
        Assert.Equal(HttpStatusCode.Conflict, dispute.StatusCode);
        Assert.Empty(await Db.ContentReports.ToArrayAsync());
    }

    [DockerFact]
    public async Task CompletedResultReopensAndExpiresWithoutChangingCompletedHistory()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        await using var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app);
        var path = $"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[0].Id}";
        using var answer = await app.Client.PostAsJsonAsync(path + "/answer", new { answer = "żapa", dont_know = false });
        var answerId = (await Body(answer))["answer_event_id"]!.GetValue<Guid>();
        using (var current = await app.Client.GetAsync("/api/v1/sessions/current"))
            Assert.Equal("completed", (await Body(current))["status"]!.GetValue<string>());
        using (var start = await app.Client.PostAsync("/api/v1/sessions", null))
            Assert.Equal(fixture.Session.Id, (await Body(start))["id"]!.GetValue<Guid>());
        Clock.Reset(fixture.Session.EndsAt);
        Authenticate(app);
        using var late = await app.Client.PostAsync($"/api/v1/answers/{answerId}/dispute", null);
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal("completed", (await Db.LearningSessions.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await Db.ContentReports.ToArrayAsync());
    }

    [DockerFact]
    public async Task ConcurrentContinueAndDisputeHaveOneConsistentOutcome()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 2);
        await using var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app);
        var path = $"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[0].Id}";
        using var answer = await app.Client.PostAsJsonAsync(path + "/answer", new { answer = "żapa", dont_know = false });
        var answerId = (await Body(answer))["answer_event_id"]!.GetValue<Guid>();
        var actions = await Task.WhenAll(app.Client.PostAsync(path + "/continue", null), app.Client.PostAsync($"/api/v1/answers/{answerId}/dispute", null));
        using var next = actions[0];
        using var dispute = actions[1];
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Contains(dispute.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        Assert.Equal(dispute.StatusCode == HttpStatusCode.OK ? 1 : 0, await Db.AnswerNeutralizations.CountAsync());
        Assert.Equal(await Db.AnswerNeutralizations.CountAsync(), await Db.ContentReports.CountAsync());
    }

    [DockerFact]
    public async Task NewWordCheckDisputeKeepsFirstReviewTomorrow()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        fixture.Items[0].Kind = "new_word_check";
        var word = await Db.LearnerWords.SingleAsync();
        word.FirstShownDay = AnswerTestData.Day;
        word.DueOn = AnswerTestData.Day.PlusDays(1);
        word.IntervalDays = 1;
        await Db.SaveChangesAsync();
        await using var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app);
        var path = $"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[0].Id}";
        using var answer = await app.Client.PostAsJsonAsync(path + "/answer", new { answer = "żapa", dont_know = false });
        var answerId = (await Body(answer))["answer_event_id"]!.GetValue<Guid>();
        using var dispute = await app.Client.PostAsync($"/api/v1/answers/{answerId}/dispute", null);
        Assert.Equal(HttpStatusCode.OK, dispute.StatusCode);
        var after = await Db.LearnerWords.AsNoTracking().SingleAsync();
        Assert.Equal(AnswerTestData.Day.PlusDays(1), after.DueOn);
        Assert.Equal(1, after.IntervalDays);
        Assert.Equal(0, after.Lapses);
        Assert.Equal("in_editor_queue", (await Db.ContentReports.AsNoTracking().SingleAsync()).Status);
    }

    [DockerFact]
    public async Task OldContinueDoesNotCloseNewerResult()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 2);
        await using var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app);
        var first = $"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[0].Id}";
        var second = $"/api/v1/sessions/{fixture.Session.Id}/items/{fixture.Items[1].Id}";
        using var a = await app.Client.PostAsJsonAsync(first + "/answer", new { answer = "żapa", dont_know = false });
        using var c = await app.Client.PostAsync(first + "/continue", null);
        using var b = await app.Client.PostAsJsonAsync(second + "/answer", new { answer = "slowo1", dont_know = false });
        Assert.Equal(HttpStatusCode.OK, b.StatusCode);
        using var stale = await app.Client.PostAsync(first + "/continue", null);
        Assert.Equal(fixture.Items[1].Id, (await Body(stale))["last_result"]!["item_id"]!.GetValue<Guid>());
    }

    [DockerFact]
    public async Task DisputeBypassesDailyReportLimitAndHidesOtherLearnersAnswer()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        Db.ContentReports.AddRange(Enumerable.Range(0, 20).Select(index => new ContentReport
        {
            LearnerId = fixture.Learner.Id, Origin = "missing_entry", SearchQuery = $"тест {index}",
            Status = "in_editor_queue", CreatedAt = Clock.GetCurrentInstant()
        }));
        Db.Learners.Add(new Learner { TelegramUserId = AnswerTestData.UserId + 1, TimeZone = "Europe/Warsaw",
            DailyNewWordsLimit = 1, OnboardedAt = Clock.GetCurrentInstant() });
        await Db.SaveChangesAsync();
        await using var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app);
        using var answer = await app.Client.PostAsJsonAsync($"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[0].Id}/answer", new { answer = "żapa", dont_know = false });
        var id = (await Body(answer))["answer_event_id"]!.GetValue<Guid>();
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(AnswerTestData.UserId + 1, Clock.GetCurrentInstant()));
        using (var foreign = await app.Client.PostAsync($"/api/v1/answers/{id}/dispute", null))
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Authenticate(app);
        using var dispute = await app.Client.PostAsync($"/api/v1/answers/{id}/dispute", null);
        Assert.Equal(HttpStatusCode.OK, dispute.StatusCode);
        Assert.Equal(21, await Db.ContentReports.CountAsync());
    }

    [DockerFact]
    public async Task NeutralizedReviewIsSuppressedUntilNextDayWithOriginalDueDate()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        await using var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app);
        var path = $"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[0].Id}";
        using var answer = await app.Client.PostAsJsonAsync(path + "/answer", new { answer = "żapa", dont_know = false });
        var id = (await Body(answer))["answer_event_id"]!.GetValue<Guid>();
        using var dispute = await app.Client.PostAsync($"/api/v1/answers/{id}/dispute", null);
        Assert.Equal(HttpStatusCode.OK, dispute.StatusCode);
        using var continued = await app.Client.PostAsync(path + "/continue", null);
        using (var today = await app.Client.PostAsync("/api/v1/sessions", null))
            Assert.Equal(0, (await Body(today))["total_items"]!.GetValue<int>());
        Assert.Equal(AnswerTestData.Day, (await Db.LearnerWords.AsNoTracking().SingleAsync()).DueOn);
        Clock.Reset(fixture.Session.EndsAt + Duration.FromMinutes(1));
        Authenticate(app);
        using var tomorrow = await app.Client.PostAsync("/api/v1/sessions", null);
        var body = await Body(tomorrow);
        Assert.Equal(1, body["total_items"]!.GetValue<int>());
        Assert.Equal("review", body["current_item"]!["kind"]!.GetValue<string>());
    }

    [DockerTheory]
    [InlineData("żaba", false, "correct")]
    [InlineData("zaba", false, "mistake_diacritic")]
    [InlineData(null, true, "dont_know")]
    [InlineData("slowo1", false, "synonym")]
    public async Task NonDisputableOutcomesNeverCreateReport(string? input, bool dontKnow, string outcome)
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 2);
        if (outcome == "synonym")
        {
            (await Db.VersionTranslations.SingleAsync(t => t.VersionId == fixture.Versions[1].Id)).TextUk = "підказка 0";
            await Db.SaveChangesAsync();
        }
        await using var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app);
        using var answer = await app.Client.PostAsJsonAsync($"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[0].Id}/answer", new { answer = input, dont_know = dontKnow });
        var body = await Body(answer);
        Assert.Equal(outcome, body["outcome"]!.GetValue<string>());
        using var dispute = await app.Client.PostAsync($"/api/v1/answers/{body["answer_event_id"]!.GetValue<Guid>()}/dispute", null);
        Assert.Equal(HttpStatusCode.Conflict, dispute.StatusCode);
        Assert.Empty(await Db.ContentReports.ToArrayAsync());
        Assert.Empty(await Db.AnswerNeutralizations.ToArrayAsync());
    }

    private sealed class DockerTheoryAttribute : TheoryAttribute
    {
        public DockerTheoryAttribute() => Skip = DockerAvailability.GetSkipReason(DockerAvailability.IsCi, DockerAvailability.UnavailableReason);
    }

    [DockerFact]
    public async Task FailureAfterReportRollsBackReportProjectionAndNeutralization()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        Guid answerId;
        await using (var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock))
        {
            Authenticate(app);
            using var answer = await app.Client.PostAsJsonAsync($"/api/v1/sessions/{fixture.Session!.Id}/items/{fixture.Items[0].Id}/answer", new { answer = "żapa", dont_know = false });
            answerId = (await Body(answer))["answer_event_id"]!.GetValue<Guid>();
        }
        await using (var failed = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock, new FailNeutralization()))
        {
            Authenticate(failed);
            await Assert.ThrowsAsync<DbUpdateException>(() => failed.Client.PostAsync($"/api/v1/answers/{answerId}/dispute", null));
        }
        Assert.Empty(await Db.ContentReports.AsNoTracking().ToArrayAsync());
        Assert.Empty(await Db.AnswerNeutralizations.AsNoTracking().ToArrayAsync());
        Assert.Equal(1, (await Db.LearnerWords.AsNoTracking().SingleAsync()).Lapses);
        await using var retry = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(retry);
        using var response = await retry.Client.PostAsync($"/api/v1/answers/{answerId}/dispute", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class FailNeutralization : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO answer_neutralizations", StringComparison.Ordinal))
                throw new InvalidOperationException("Тестова відмова нейтралізації.");
            return ValueTask.FromResult(result);
        }
    }

    private void Authenticate(AnswerTestApplication app) => app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(AnswerTestData.UserId, Clock.GetCurrentInstant()));
    private static async Task<JsonNode> Body(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
}
