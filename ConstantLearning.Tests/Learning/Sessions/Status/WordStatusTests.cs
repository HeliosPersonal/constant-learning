using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Sessions.Answer;
using ConstantLearning.Tests.Infrastructure;
using ConstantLearning.Tests.Learning.Sessions.Answer;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Sessions.Status;

public sealed class WordStatusTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Ac18_TwoDifferentScheduledDaysImmediatelyCountYoungRecalledWordAndReplayDoesNotAddSuccess()
    {
        var fixture = await SeedYoungAsync();
        await using var app = await StartAsync();
        var firstCard = await StartSessionAsync(app);
        var first = await AnswerAsync(app, firstCard);
        await AssertStateAsync(app, fixture, first, "review", 1, 0);
        var replay = await AnswerAsync(app, firstCard);
        Assert.True(JsonNode.DeepEquals(first, replay));
        Assert.Single(await Db.AnswerEvents.ToArrayAsync());
        await AssertStateAsync(app, fixture, replay, "review", 1, 0);
        var sameDay = await StartSessionAsync(app);
        Assert.Equal(0, sameDay["total_items"]!.GetValue<int>());

        await AdvanceToDueAsync(fixture);
        var second = await AnswerAsync(app, await StartSessionAsync(app));
        var recalled = await AssertStateAsync(app, fixture, second, "recalled", 2, 1);
        Assert.InRange(Period.Between(recalled.FirstShownDay!.Value, Today, PeriodUnits.Days).Days, 2, 4);
        var events = await Db.AnswerEvents.AsNoTracking().OrderBy(value => value.LearnerDay).ToArrayAsync();
        Assert.Equal(2, events.Length);
        Assert.True(events[0].LearnerDay < events[1].LearnerDay);
        Assert.All(events, value => Assert.Equal("review", value.ExerciseKind));
    }

    [DockerTheory]
    [InlineData("żapa", false, "mistake_typo")]
    [InlineData("zaba", false, "mistake_diacritic")]
    [InlineData(null, true, "dont_know")]
    public async Task Ac19_MistakeRemovesRecalledWordAndRequiresTwoNewScheduledSuccesses(
        string? answer, bool dontKnow, string outcome)
    {
        var fixture = await SeedYoungAsync();
        await using var app = await StartAsync();
        await AssertStateAsync(app, fixture, await AnswerAsync(app, await StartSessionAsync(app)), "review", 1, 0);
        await AdvanceToDueAsync(fixture);
        await AssertStateAsync(app, fixture, await AnswerAsync(app, await StartSessionAsync(app)), "recalled", 2, 1);
        await AdvanceToDueAsync(fixture);

        var mistakeDay = Today;
        var mistake = await AnswerAsync(app, await StartSessionAsync(app), answer, dontKnow);
        Assert.Equal(outcome, mistake["outcome"]!.GetValue<string>());
        var reset = await AssertStateAsync(app, fixture, mistake, "review", 0, 0);
        Assert.Equal(mistakeDay.PlusDays(1), reset.DueOn);
        Assert.Equal(1, reset.IntervalDays);
        Assert.Equal(1, reset.Lapses);
        Assert.False(reset.ControlPending);

        await AdvanceToDueAsync(fixture);
        await AssertStateAsync(app, fixture, await AnswerAsync(app, await StartSessionAsync(app)), "review", 1, 0);
        await AdvanceToDueAsync(fixture);
        var recovered = await AssertStateAsync(app, fixture,
            await AnswerAsync(app, await StartSessionAsync(app)), "recalled", 2, 1);
        Assert.Equal(1, recovered.Lapses);
        var events = await Db.AnswerEvents.AsNoTracking().OrderBy(value => value.LearnerDay).ToArrayAsync();
        Assert.Equal(new[] { "correct", "correct", outcome, "correct", "correct" }, events.Select(value => value.Outcome));
        Assert.Equal(5, events.Select(value => value.LearnerDay).Distinct().Count());
        Assert.Equal("recalled", JsonNode.Parse(events[2].StateBefore)!["status"]!.GetValue<string>());
        Assert.Equal(0, JsonNode.Parse(events[3].StateBefore)!["consecutive_scheduled_correct"]!.GetValue<int>());
    }

    [DockerTheory]
    [InlineData(1, "review", 0)]
    [InlineData(2, "recalled", 1)]
    public async Task Ac18_NeutralSynonymPreservesStreakStatusAndCounter(int streak, string status, int recalled)
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 2, session: false);
        var word = await Db.LearnerWords.SingleAsync(value => value.EntryId == fixture.Entries[0].Id);
        word.Status = status;
        word.ConsecutiveScheduledCorrect = (short)streak;
        word.LastReviewDay = AnswerTestData.Day.PlusDays(-2);
        (await Db.LearnerWords.SingleAsync(value => value.EntryId == fixture.Entries[1].Id)).DueOn = AnswerTestData.Day.PlusDays(180);
        (await Db.VersionTranslations.SingleAsync(value => value.VersionId == fixture.Versions[1].Id)).TextUk = "підказка 0";
        await Db.SaveChangesAsync();
        var before = AnswerWordState.Read(word);
        await using var app = await StartAsync();
        await AssertTodayAsync(app, recalled);
        var result = await AnswerAsync(app, await StartSessionAsync(app), "slowo1");
        Assert.Equal("synonym", result["outcome"]!.GetValue<string>());
        var after = await AssertStateAsync(app, fixture, result, status, streak, recalled);
        Assert.Equal(before with { LastScheduledAttemptDay = Today }, AnswerWordState.Read(after));
        Assert.Equal(0, (await StartSessionAsync(app))["total_items"]!.GetValue<int>());
        Advance(Today.PlusDays(1));
        var next = await AnswerAsync(app, await StartSessionAsync(app));
        await AssertStateAsync(app, fixture, next, "recalled", 2, 1);
        Assert.Equal(new[] { "synonym", "correct" }, await Db.AnswerEvents.AsNoTracking()
            .OrderBy(value => value.LearnerDay).Select(value => value.Outcome).ToArrayAsync());
    }

    [DockerTheory]
    [InlineData("żaba", false)]
    [InlineData("zaba", false)]
    [InlineData(null, true)]
    public async Task NewWordCheckNeverCountsAsScheduledSuccessOrMistake(string? answer, bool dontKnow)
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1);
        fixture.Items[0].Kind = "new_word_check";
        var word = await Db.LearnerWords.SingleAsync();
        word.FirstShownDay = Today;
        word.DueOn = Today.PlusDays(1);
        word.IntervalDays = 1;
        await Db.SaveChangesAsync();
        var before = AnswerWordState.Snapshot(word);
        await using var app = await StartAsync();
        var initial = new JsonObject
        {
            ["id"] = fixture.Session!.Id,
            ["current_item"] = new JsonObject { ["id"] = fixture.Items[0].Id }
        };
        var result = await AnswerAsync(app, initial, answer, dontKnow);
        var unchanged = await AssertStateAsync(app, fixture, result, "review", 0, 0);
        Assert.Equal(before, AnswerWordState.Snapshot(unchanged));
        Assert.Equal("new_word_check", (await Db.AnswerEvents.AsNoTracking().SingleAsync()).ExerciseKind);
        await AdvanceToDueAsync(fixture);
        await AssertStateAsync(app, fixture, await AnswerAsync(app, await StartSessionAsync(app)), "review", 1, 0);
        await AdvanceToDueAsync(fixture);
        await AssertStateAsync(app, fixture, await AnswerAsync(app, await StartSessionAsync(app)), "recalled", 2, 1);
    }

    [DockerFact]
    public async Task Ac23_CorrectControlIsFirstSuccessAndSchedulesMoreThanSevenDays()
    {
        var fixture = await SeedControlAsync();
        await using var app = await StartAsync();
        await AssertTodayAsync(app, 0);
        var control = await StartSessionAsync(app);
        Assert.Equal("control_repetition", control["current_item"]!["kind"]!.GetValue<string>());
        var first = await AssertStateAsync(app, fixture, await AnswerAsync(app, control), "review", 1, 0);
        Assert.False(first.ControlPending);
        Assert.InRange(first.IntervalDays!.Value, 8, 21);
        Assert.Equal(Today.PlusDays(first.IntervalDays.Value), first.DueOn);
        await AdvanceToDueAsync(fixture);
        var review = await StartSessionAsync(app);
        Assert.Equal("review", review["current_item"]!["kind"]!.GetValue<string>());
        await AssertStateAsync(app, fixture, await AnswerAsync(app, review), "recalled", 2, 1);
        Assert.Equal(new[] { "control_repetition", "review" }, await Db.AnswerEvents.AsNoTracking()
            .OrderBy(value => value.LearnerDay).Select(value => value.ExerciseKind).ToArrayAsync());
    }

    [DockerTheory]
    [InlineData("żapa", false)]
    [InlineData("zaba", false)]
    [InlineData(null, true)]
    public async Task Ac22_ControlMistakeClearsControlAndReturnsToOrdinaryReview(string? answer, bool dontKnow)
    {
        var fixture = await SeedControlAsync();
        await using var app = await StartAsync();
        var control = await StartSessionAsync(app);
        Assert.Equal("control_repetition", control["current_item"]!["kind"]!.GetValue<string>());
        var reset = await AssertStateAsync(app, fixture, await AnswerAsync(app, control, answer, dontKnow), "review", 0, 0);
        Assert.False(reset.ControlPending);
        Assert.Equal(1, reset.Lapses);
        Assert.Equal(1, reset.IntervalDays);
        Assert.Equal(Today.PlusDays(1), reset.DueOn);
        await AdvanceToDueAsync(fixture);
        var review = await StartSessionAsync(app);
        Assert.Equal("review", review["current_item"]!["kind"]!.GetValue<string>());
        await AssertStateAsync(app, fixture, await AnswerAsync(app, review), "review", 1, 0);
        await AdvanceToDueAsync(fixture);
        await AssertStateAsync(app, fixture, await AnswerAsync(app, await StartSessionAsync(app)), "recalled", 2, 1);
        Assert.Equal(new[] { "control_repetition", "review", "review" }, await Db.AnswerEvents.AsNoTracking()
            .OrderBy(value => value.LearnerDay).Select(value => value.ExerciseKind).ToArrayAsync());
    }

    private LocalDate Today => Clock.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb["Europe/Warsaw"]).Date;

    private async Task<AnswerFixture> SeedYoungAsync()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1, session: false);
        var word = await Db.LearnerWords.SingleAsync();
        word.FirstShownDay = Today.PlusDays(-1);
        word.IntervalDays = 1;
        await Db.SaveChangesAsync();
        return fixture;
    }

    private async Task<AnswerFixture> SeedControlAsync()
    {
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, 1, session: false);
        var word = await Db.LearnerWords.SingleAsync();
        // Початковий стан після «Вже знаю»; саму дію реалізує окрема задача.
        word.FirstShownDay = Today.PlusDays(-7);
        word.IntervalDays = 7;
        word.ControlPending = true;
        await Db.SaveChangesAsync();
        return fixture;
    }

    private void Advance(LocalDate day) => Clock.Reset(day.At(new LocalTime(9, 0)).InUtc().ToInstant());

    private async Task AdvanceToDueAsync(AnswerFixture fixture)
    {
        var word = await ReadWordAsync(fixture);
        Assert.True(word.DueOn > Today);
        Advance(word.DueOn!.Value);
    }

    private Task<LearnerWord> ReadWordAsync(AnswerFixture fixture) => Db.LearnerWords.AsNoTracking()
        .SingleAsync(value => value.LearnerId == fixture.Learner.Id && value.EntryId == fixture.Entries[0].Id);

    private async Task<LearnerWord> AssertStateAsync(AnswerTestApplication app, AnswerFixture fixture,
        JsonNode result, string status, int streak, int recalled)
    {
        Assert.Equal(status, result["word_status"]!.GetValue<string>());
        var word = await ReadWordAsync(fixture);
        Assert.Equal(status, word.Status);
        Assert.Equal(streak, word.ConsecutiveScheduledCorrect);
        await AssertTodayAsync(app, recalled);
        return word;
    }

    private async Task AssertTodayAsync(AnswerTestApplication app, int recalled)
    {
        Authenticate(app);
        using var response = await app.Client.GetAsync("/api/v1/today");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("getToday", response);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(recalled, body["recalled_words_count"]!.GetValue<int>());
    }

    private async Task<AnswerTestApplication> StartAsync() =>
        await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);

    private void Authenticate(AnswerTestApplication app) => app.Client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(AnswerTestData.UserId, Clock.GetCurrentInstant()));

    private async Task<JsonNode> StartSessionAsync(AnswerTestApplication app)
    {
        Authenticate(app);
        using var response = await app.Client.PostAsync("/api/v1/sessions", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await ContractAssert.ResponseAsync("startSession", response);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    private async Task<JsonNode> AnswerAsync(AnswerTestApplication app, JsonNode session, string? answer = "żaba", bool dontKnow = false)
    {
        Authenticate(app);
        using var response = await app.Client.PostAsJsonAsync(
            $"/api/v1/sessions/{session["id"]!.GetValue<Guid>()}/items/{session["current_item"]!["id"]!.GetValue<Guid>()}/answer",
            new { answer, dont_know = dontKnow });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("answerSessionItem", response);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    private sealed class DockerTheoryAttribute : TheoryAttribute
    {
        public DockerTheoryAttribute() => Skip = DockerAvailability.GetSkipReason(DockerAvailability.IsCi, DockerAvailability.UnavailableReason);
    }
}
