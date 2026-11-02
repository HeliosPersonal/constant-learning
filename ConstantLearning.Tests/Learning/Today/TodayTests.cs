using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using static ConstantLearning.Tests.Learning.Today.TodayTestData;

namespace ConstantLearning.Tests.Learning.Today;

public sealed class TodayTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const string Path = "/api/v1/today";

    [DockerFact]
    public async Task Ac28_CountsOwnProgressAndCompletedSessionsAcrossSevenCalendarDates()
    {
        var learner = Learner(Clock);
        var other = Learner(Clock, UserId + 1);
        Db.AddRange(learner, other);
        var course = await CourseAsync(Db, 25);
        Db.LearnerWords.AddRange(course.Take(12).Select(entry => Word(learner, entry, "recalled", 7, 1)));
        Db.LearnerWords.AddRange(course.Skip(12).Take(7).Select(entry => Word(learner, entry)));
        Db.LearnerWords.Add(Word(learner, course[19], "recalled", 6, 1));
        Db.LearnerWords.Add(Word(learner, course[20], dueDaysFromToday: 1));
        Db.LearnerWords.Add(Word(other, course[21], "recalled"));
        var zone = DateTimeZoneProviders.Tzdb[learner.TimeZone];
        var start = Day.PlusDays(-6).AtStartOfDayInZone(zone).ToInstant();
        Db.LearningSessions.AddRange(Completed(learner, start), Completed(learner, start + Duration.FromHours(1)),
            Completed(learner, Clock.GetCurrentInstant()), Completed(learner, start - Duration.FromNanoseconds(1)),
            Completed(other, Clock.GetCurrentInstant()), Completed(learner, Day.PlusDays(1).AtStartOfDayInZone(zone).ToInstant()));
        Db.LearningSessions.Add(new()
        {
            LearnerId = learner.Id,
            LearnerDay = Day,
            Status = "incomplete",
            EndsAt = Clock.GetCurrentInstant(),
            CreatedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var result = await GetAsync(app);
        Assert.Equal("2026-10-31", result["learner_day"]!.GetValue<string>());
        AssertCounts(result, 7, 13, 3);
        Assert.False(result["has_active_session"]!.GetValue<bool>());
        Assert.Equal("in_progress", result["course_state"]!.GetValue<string>());
        Assert.Equal(7, await Db.LearningSessions.CountAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
    }

    [DockerFact]
    public async Task DueQueueExcludesAllTodaysReviewAttemptsIncludingNeutralOutcomes()
    {
        var learner = Learner(Clock);
        var other = Learner(Clock, UserId + 1);
        Db.AddRange(learner, other);
        var course = await CourseAsync(Db, 7);
        Db.LearnerWords.AddRange(course.Select(entry => Word(learner, entry)));
        Db.AnswerEvents.AddRange(Attempt(learner, course[0], Clock),
            Attempt(learner, course[1], Clock, "control_repetition", "hint_used"),
            Attempt(learner, course[2], Clock, outcome: "incorrect"),
            Attempt(learner, course[3], Clock, daysAgo: 1),
            Attempt(other, course[4], Clock), Attempt(learner, course[5], Clock, "new_word_check", "correct"));
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        AssertCounts(await GetAsync(app), 4, 0, 0);
        Assert.Equal(6, await Db.AnswerEvents.CountAsync());
    }

    [DockerFact]
    public async Task Ac33_WithdrawnAndDraftWordsDisappearAndReapprovalRestoresTheirHistory()
    {
        var learner = Learner(Clock);
        Db.Add(learner);
        var course = await CourseAsync(Db, 4);
        Db.LearnerWords.AddRange(course.Select(entry => Word(learner, entry, "recalled")));
        await Db.SaveChangesAsync();
        var original = await ReadHistoryAsync();
        var pointer = course[0].PublishedVersionId;
        course[0].PublishedVersionId = null;
        var version = await Db.EntryVersions.SingleAsync(value => value.EntryId == course[1].Id);
        version.ValidationStatus = "draft";
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        AssertCounts(await GetAsync(app), 2, 2, 0);
        Assert.Equal(original, await ReadHistoryAsync());
        course[0].PublishedVersionId = pointer;
        version.ValidationStatus = "approved";
        await Db.SaveChangesAsync();
        AssertCounts(await GetAsync(app), 4, 4, 0);
        Assert.Equal(original, await ReadHistoryAsync());
    }

    [DockerFact]
    public async Task Ac10_CourseCompletionAndConcurrentReplenishmentNoticeUsePersistentMarker()
    {
        var learner = Learner(Clock);
        Db.Add(learner);
        var course = await CourseAsync(Db, 1);
        Db.LearnerWords.Add(Word(learner, course[0]));
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var completed = await GetAsync(app);
        Assert.Equal("completed", completed["course_state"]!.GetValue<string>());
        Assert.False(completed["course_replenished_notice"]!.GetValue<bool>());
        Assert.NotNull((await Db.Learners.AsNoTracking().SingleAsync()).CourseCompletedAt);
        await CourseAsync(Db, 1);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => GetAsync(app)));
        Assert.Single(responses, result => result["course_replenished_notice"]!.GetValue<bool>());
        Assert.All(responses, result => Assert.Equal("in_progress", result["course_state"]!.GetValue<string>()));
        Assert.Null((await Db.Learners.AsNoTracking().SingleAsync()).CourseCompletedAt);
        Assert.False((await GetAsync(app))["course_replenished_notice"]!.GetValue<bool>());
        Assert.Empty(await Db.LearningSessions.ToArrayAsync());
    }

    [DockerFact]
    public async Task ActiveSessionSkipsWithdrawnCardsAndExpiresAtItsImmutableBoundary()
    {
        var learner = Learner(Clock);
        Db.Add(learner);
        var course = await CourseAsync(Db, 2);
        var now = Clock.GetCurrentInstant();
        var session = new LearningSession
        {
            LearnerId = learner.Id,
            LearnerDay = Day,
            Status = "active",
            EndsAt = now + Duration.FromHours(1),
            CreatedAt = now,
            LastActivityAt = now
        };
        Db.Add(session);
        Db.SessionItems.AddRange(course.Select((entry, index) => new SessionItem
        {
            SessionId = session.Id,
            EntryId = entry.Id,
            Position = (short)(index + 1),
            Kind = "new_word_card",
            State = "pending",
            CreatedAt = now
        }));
        await Db.SaveChangesAsync();
        course[0].PublishedVersionId = null;
        await Db.SaveChangesAsync();
        await using (var app = await StartAsync())
            Assert.True((await GetAsync(app))["has_active_session"]!.GetValue<bool>());
        Assert.Equal(new[] { "skipped", "pending" }, await Db.SessionItems.AsNoTracking()
            .OrderBy(item => item.Position).Select(item => item.State).ToArrayAsync());
        Clock.Reset(session.EndsAt);
        await using var expired = await StartAsync();
        Assert.False((await GetAsync(expired))["has_active_session"]!.GetValue<bool>());
        var stored = await Db.LearningSessions.AsNoTracking().SingleAsync();
        Assert.Equal("incomplete", stored.Status);
        Assert.Null(stored.CompletedAt);
        Assert.Equal(session.EndsAt, stored.EndsAt);
        Assert.Equal(now, stored.LastActivityAt);
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
    }

    [DockerFact]
    public async Task RemovingAllActiveCardsCompletesExistingSessionWithoutCreatingAnother()
    {
        var learner = Learner(Clock);
        Db.Add(learner);
        var course = await CourseAsync(Db, 1);
        var session = new LearningSession
        {
            LearnerId = learner.Id,
            LearnerDay = Day,
            Status = "active",
            EndsAt = Clock.GetCurrentInstant() + Duration.FromHours(1),
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.Add(session);
        Db.SessionItems.Add(new()
        {
            SessionId = session.Id,
            EntryId = course[0].Id,
            Position = 1,
            Kind = "new_word_card",
            State = "pending",
            CreatedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();
        course[0].PublishedVersionId = null;
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var response = await GetAsync(app);
        Assert.False(response["has_active_session"]!.GetValue<bool>());
        AssertCounts(response, 0, 0, 1);
        Assert.Single(await Db.LearningSessions.ToArrayAsync());
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
    }

    [DockerFact]
    public async Task ExhaustedDailyNewWordLimitMarksNewWordsUnavailableToday()
    {
        var learner = Learner(Clock);
        learner.DailyNewWordsLimit = 1;
        Db.Add(learner);
        var course = await CourseAsync(Db, 2);
        Db.LearnerWords.Add(Word(learner, course[0], shownDaysAgo: 0, dueDaysFromToday: 1));
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var result = await GetAsync(app);
        Assert.False(result["new_words_available_today"]!.GetValue<bool>());
        AssertCounts(result, due: 0, recalled: 0, completed: 0);
    }

    [DockerFact]
    public async Task Ac29_OnlyVerifiedClaimsSelectProgressAndUnonboardedLearnersReceive404()
    {
        var learner = Learner(Clock);
        var other = Learner(Clock, UserId + 1);
        Db.AddRange(learner, other);
        var course = await CourseAsync(Db, 2);
        Db.LearnerWords.Add(Word(learner, course[0], "recalled"));
        Db.LearningSessions.Add(Completed(learner, Clock.GetCurrentInstant()));
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        AssertCounts(await GetAsync(app), 1, 1, 1);
        Authenticate(app, UserId + 1);
        using var spoofed = await app.Client.GetAsync(Path + "?learner_id=" + learner.Id);
        Assert.Equal(HttpStatusCode.OK, spoofed.StatusCode);
        await ContractAssert.ResponseAsync("getToday", spoofed);
        AssertCounts(JsonNode.Parse(await spoofed.Content.ReadAsStringAsync())!, 0, 0, 0);
        Authenticate(app, UserId + 2);
        using var missing = await app.Client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await ContractAssert.ResponseAsync("getToday", missing);
        Assert.Equal("learner.not_onboarded", JsonNode.Parse(await missing.Content.ReadAsStringAsync())!["code"]!.GetValue<string>());
        app.Client.DefaultRequestHeaders.Authorization = null;
        using var unauthorized = await app.Client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        await ContractAssert.ResponseAsync("getToday", unauthorized);
        Assert.Single(await Db.LearningSessions.ToArrayAsync());
    }

    private Task<string[]> ReadHistoryAsync() => Db.LearnerWords.AsNoTracking().OrderBy(word => word.EntryId)
        .Select(word => word.EntryId + ":" + word.Status + ":" + word.FirstShownDay + ":" + word.DueOn + ":" + word.Version)
        .ToArrayAsync();

    private async Task<TodayTestApplication> StartAsync()
    {
        var app = await TodayTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app, UserId);
        return app;
    }

    internal static void Authenticate(TodayTestApplication app, long userId, IClock? clock = null) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(userId, clock?.GetCurrentInstant() ?? Instant.FromUtc(2026, 10, 31, 9, 0)));

    private async Task<JsonNode> GetAsync(TodayTestApplication app)
    {
        Authenticate(app, UserId, Clock);
        using var response = await app.Client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("getToday", response);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    private static void AssertCounts(JsonNode result, int due, int recalled, int completed)
    {
        Assert.Equal(due, result["due_review_count"]!.GetValue<int>());
        Assert.Equal(recalled, result["recalled_words_count"]!.GetValue<int>());
        Assert.Equal(completed, result["completed_sessions_last_7_days"]!.GetValue<int>());
    }
}
