using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using ConstantLearning.Tests.Learning.Sessions.Start;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Sessions.Current;

public sealed class CurrentSessionTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const long UserId = 950000001;
    private const string Path = "/api/v1/sessions/current";
    private static readonly LocalDate Day = new(2026, 10, 31);

    [DockerFact]
    public async Task Ac24_RestartReturnsSeventhOfTenWithoutAddingCardsOrCountingShows()
    {
        var learner = await SeedLearnerAsync(10);
        var course = await SeedCourseAsync(20);
        JsonNode started;
        await using (var initial = await StartAsync()) started = await StartSessionAsync(initial);
        var sessionId = started["id"]!.GetValue<Guid>();
        var items = await Db.SessionItems.OrderBy(item => item.Position).ToArrayAsync();
        foreach (var item in items.Take(6))
        {
            item.State = "answered";
            item.AnsweredAt = Clock.GetCurrentInstant();
            AddWord(learner, course.Single(entry => entry.Id == item.EntryId), Day, Day.PlusDays(1));
        }
        await Db.SaveChangesAsync();
        var before = await ReadHistoryAsync();
        var sessionBefore = await Db.LearningSessions.AsNoTracking().SingleAsync();
        await using var restarted = await StartAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => GetCurrentAsync(restarted)));
        Assert.Single(responses.Select(response => response.ToJsonString()).Distinct());
        var response = responses[0];
        Assert.Equal(sessionId, response["id"]!.GetValue<Guid>());
        Assert.Equal(10, response["total_items"]!.GetValue<int>());
        Assert.Equal(6, response["completed_items"]!.GetValue<int>());
        Assert.Equal(items[6].Id, response["current_item"]!["id"]!.GetValue<Guid>());
        Assert.Equal(7, response["current_item"]!["position"]!.GetValue<int>());
        Assert.Equal(before, await ReadHistoryAsync());
        Assert.Equal(10, await Db.SessionItems.CountAsync());
        Assert.Single(await Db.LearningSessions.ToArrayAsync());
        var sessionAfter = await Db.LearningSessions.AsNoTracking().SingleAsync();
        Assert.Equal(sessionBefore.Version, sessionAfter.Version);
        Assert.Equal(sessionBefore.LastActivityAt, sessionAfter.LastActivityAt);
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Null((await Db.Learners.AsNoTracking().SingleAsync()).NewWordsAvailableFrom);
    }

    [DockerFact]
    public async Task Ac25_ExpiryPreservesShownUsageAndReviews_WhileUnshownWordsReturnToNextSession()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(10);
        AddWord(learner, course[0], Day.PlusDays(-10), Day.PlusDays(-1));
        AddWord(learner, course[1], Day.PlusDays(-10), Day);
        await Db.SaveChangesAsync();
        await using (var initial = await StartAsync()) await StartSessionAsync(initial);
        var session = await Db.LearningSessions.AsNoTracking().SingleAsync();
        var newCards = await Db.SessionItems.Where(item => item.Kind == "new_word_card")
            .OrderBy(item => item.Position).ToArrayAsync();
        newCards[0].State = "answered";
        newCards[0].AnsweredAt = Clock.GetCurrentInstant();
        AddWord(learner, course.Single(entry => entry.Id == newCards[0].EntryId), Day, Day.PlusDays(1));
        Db.SessionItems.Add(new SessionItem
        {
            SessionId = session.Id,
            EntryId = newCards[0].EntryId,
            Position = 8,
            Kind = "new_word_check",
            State = "pending",
            CreatedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();
        var before = await ReadHistoryAsync();
        var oldItems = await Db.SessionItems.AsNoTracking().OrderBy(item => item.Position)
            .Select(item => new { item.Id, item.State, item.Version }).ToArrayAsync();
        Clock.Reset(session.EndsAt);
        await using var nextDay = await StartAsync();
        var expired = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => GetMissingAsync(nextDay)));
        Assert.All(expired, body => Assert.Equal("session.not_found", body["code"]!.GetValue<string>()));
        var closed = await Db.LearningSessions.AsNoTracking().SingleAsync();
        Assert.Equal("incomplete", closed.Status);
        Assert.Null(closed.CompletedAt);
        Assert.Equal(session.EndsAt, closed.EndsAt);
        Assert.Equal(before, await ReadHistoryAsync());
        Assert.Equal(oldItems, await Db.SessionItems.AsNoTracking().OrderBy(item => item.Position)
            .Select(item => new { item.Id, item.State, item.Version }).ToArrayAsync());
        Assert.Single(await Db.LearningSessions.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());

        var started = await StartSessionAsync(nextDay);
        var activeId = started["id"]!.GetValue<Guid>();
        var nextItems = await Db.SessionItems.AsNoTracking().Where(item => item.SessionId == activeId).ToArrayAsync();
        Assert.All(newCards.Skip(1), card => Assert.Contains(nextItems,
            item => item.EntryId == card.EntryId && item.Kind == "new_word_card"));
        Assert.Contains(nextItems, item => item.EntryId == newCards[0].EntryId && item.Kind == "review");
        Assert.DoesNotContain(nextItems, item => item.EntryId == newCards[0].EntryId && item.Kind == "new_word_card");
        Assert.Contains(nextItems, item => item.EntryId == course[0].Id && item.Kind == "review");
        Assert.Contains(nextItems, item => item.EntryId == course[1].Id && item.Kind == "review");
        Assert.Equal(before, await ReadHistoryAsync());
    }

    [DockerFact]
    public async Task TimeZoneExtensionDoesNotExtendExistingSessionBoundary()
    {
        await SeedLearnerAsync();
        await SeedCourseAsync(10);
        Instant originalEnd;
        await using (var app = await StartAsync())
        {
            await StartSessionAsync(app);
            originalEnd = (await Db.LearningSessions.AsNoTracking().SingleAsync()).EndsAt;
            using var body = new StringContent("""{"time_zone":"America/New_York"}""", Encoding.UTF8, "application/json");
            using var changed = await app.Client.PatchAsync("/api/v1/learners/me/settings", body);
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            await ContractAssert.ResponseAsync("updateLearnerSettings", changed);
        }
        Clock.Reset(originalEnd - Duration.FromNanoseconds(1));
        await using (var before = await StartAsync())
            Assert.Equal("active", (await GetCurrentAsync(before))["status"]!.GetValue<string>());
        Clock.Reset(originalEnd);
        await using var expired = await StartAsync();
        await GetMissingAsync(expired);
        var stored = await Db.Learners.AsNoTracking().SingleAsync();
        Assert.True(stored.CurrentDayEndsAt > originalEnd);
        Assert.Equal(Day, stored.CurrentDayDate);
        Assert.Equal(originalEnd, (await Db.LearningSessions.AsNoTracking().SingleAsync()).EndsAt);
        Assert.Equal("incomplete", (await Db.LearningSessions.AsNoTracking().SingleAsync()).Status);
    }

    [DockerFact]
    public async Task WithdrawnAndDraftPendingCardsAreSkippedWithoutChangingLearnerHistory()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(5);
        AddWord(learner, course[0], Day.PlusDays(-10), Day);
        AddWord(learner, course[1], Day.PlusDays(-10), Day);
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        await StartSessionAsync(app);
        var before = await ReadHistoryAsync();
        course[0].PublishedVersionId = null;
        (await Db.EntryVersions.SingleAsync(version => version.EntryId == course[1].Id)).ValidationStatus = "draft";
        await Db.SaveChangesAsync();
        var current = await GetCurrentAsync(app);
        var items = await Db.SessionItems.AsNoTracking().OrderBy(item => item.Position).ToArrayAsync();
        Assert.Equal(new[] { "skipped", "skipped", "pending", "pending", "pending" }, items.Select(item => item.State));
        Assert.Equal(items[2].Id, current["current_item"]!["id"]!.GetValue<Guid>());
        Assert.Equal(2, current["completed_items"]!.GetValue<int>());
        Assert.Equal(before, await ReadHistoryAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
    }

    [DockerFact]
    public async Task RemovingAllPendingContentClosesSessionAndReturnsNotFoundWithoutCreatingAnother()
    {
        await SeedLearnerAsync();
        await SeedCourseAsync(3);
        await using var app = await StartAsync();
        await StartSessionAsync(app);
        await Db.DictionaryEntries.ExecuteUpdateAsync(update => update.SetProperty(entry => entry.PublishedVersionId, (Guid?)null));
        await GetMissingAsync(app);
        var closed = await Db.LearningSessions.AsNoTracking().SingleAsync();
        Assert.Equal("completed", closed.Status);
        Assert.Equal(Clock.GetCurrentInstant(), closed.CompletedAt);
        Assert.All(await Db.SessionItems.AsNoTracking().ToArrayAsync(), item => Assert.Equal("skipped", item.State));
        await GetMissingAsync(app);
        Assert.Single(await Db.LearningSessions.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());
    }

    [DockerFact]
    public async Task CurrentCardUsesLatestPublishedVersion_AndReviewDoesNotLeakAnswers()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(3);
        AddWord(learner, course[0], Day.PlusDays(-10), Day);
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        await StartSessionAsync(app);
        var review = await GetCurrentAsync(app);
        Assert.Equal("review", review["current_item"]!["kind"]!.GetValue<string>());
        Assert.Null(review["current_item"]!["card"]);
        Assert.DoesNotContain("żaba", review.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("accepted_answers", review.ToJsonString(), StringComparison.Ordinal);
        (await Db.SessionItems.SingleAsync(item => item.Kind == "review")).State = "answered";
        var replacement = ReportsPublicationTestData.NewVersion(course[1]);
        replacement.LemmaPl = "kot";
        Db.EntryVersions.Add(replacement);
        Db.VersionTranslations.Add(new() { VersionId = replacement.Id, Kind = "canonical", TextUk = "кіт", Position = 0 });
        await Db.SaveChangesAsync();
        course[1].PublishedVersionId = replacement.Id;
        await Db.SaveChangesAsync();
        var next = await GetCurrentAsync(app);
        Assert.Equal("kot", next["current_item"]!["card"]!["lemma_pl"]!.GetValue<string>());
        Assert.DoesNotContain("accepted_answers", next.ToJsonString(), StringComparison.Ordinal);
        Assert.Single(await Db.LearnerWords.ToArrayAsync());
    }

    [DockerFact]
    public async Task AuthenticationMissingSessionAndLearnerIsolationUseContractResponses()
    {
        await SeedLearnerAsync();
        await SeedCourseAsync(5);
        await using var app = await StartAsync();
        await GetMissingAsync(app);
        Assert.Empty(await Db.LearningSessions.ToArrayAsync());
        await StartSessionAsync(app);
        Authenticate(app, UserId + 1);
        await GetMissingAsync(app);
        await SeedLearnerAsync(userId: UserId + 1);
        await GetMissingAsync(app);
        Assert.Single(await Db.LearningSessions.ToArrayAsync());
        app.Client.DefaultRequestHeaders.Authorization = null;
        using var unauthorized = await app.Client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        await ContractAssert.ResponseAsync("getCurrentSession", unauthorized);
    }

    [DockerFact]
    public async Task ConcurrentStartAndGetAtBoundaryLeaveOneNewActiveSession()
    {
        await SeedLearnerAsync();
        await SeedCourseAsync(10);
        await using (var initial = await StartAsync()) await StartSessionAsync(initial);
        var previous = await Db.LearningSessions.AsNoTracking().SingleAsync();
        Clock.Reset(previous.EndsAt);
        await using var app = await StartAsync();
        var reads = Enumerable.Range(0, 6).Select(_ => app.Client.GetAsync(Path)).ToArray();
        var started = await StartSessionAsync(app);
        var responses = await Task.WhenAll(reads);
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound);
                await ContractAssert.ResponseAsync("getCurrentSession", response);
                if (response.StatusCode == HttpStatusCode.OK)
                    Assert.Equal(started["id"]!.GetValue<Guid>(), (await BodyAsync(response))["id"]!.GetValue<Guid>());
            }
        }
        Assert.Equal(2, await Db.LearningSessions.CountAsync());
        Assert.Single(await Db.LearningSessions.Where(session => session.Status == "active").ToArrayAsync());
        Assert.Equal("incomplete", (await Db.LearningSessions.AsNoTracking().SingleAsync(session => session.Id == previous.Id)).Status);
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());
    }

    private Task<string[]> ReadHistoryAsync() => Db.LearnerWords.AsNoTracking().OrderBy(word => word.EntryId)
        .Select(word => word.EntryId + ":" + word.Status + ":" + word.FirstShownDay + ":" + word.DueOn + ":" + word.Version)
        .ToArrayAsync();

    private async Task<Learner> SeedLearnerAsync(short limit = 5, long userId = UserId)
    {
        var learner = new Learner
        {
            TelegramUserId = userId,
            TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = true,
            DailyNewWordsLimit = limit,
            OnboardedAt = Clock.GetCurrentInstant(),
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        return learner;
    }

    private async Task<DictionaryEntry[]> SeedCourseAsync(int count)
    {
        var entries = new List<DictionaryEntry>();
        for (var index = 1; index <= count; index++)
        {
            var entry = new DictionaryEntry { ExternalId = Guid.NewGuid().ToString("N"), Domain = "everyday", CoursePosition = index };
            var version = ReportsPublicationTestData.NewVersion(entry);
            version.FrequencyRank = index;
            Db.EntryVersions.Add(version);
            Db.VersionTranslations.Add(new() { VersionId = version.Id, Kind = "canonical", TextUk = "жаба", Position = 0 });
            await Db.SaveChangesAsync();
            entry.PublishedVersionId = version.Id;
            entries.Add(entry);
        }
        await Db.SaveChangesAsync();
        return entries.ToArray();
    }

    private void AddWord(Learner learner, DictionaryEntry entry, LocalDate shown, LocalDate due) =>
        Db.LearnerWords.Add(new LearnerWord
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            Status = "review",
            FirstShownDay = shown,
            DueOn = due,
            IntervalDays = 2,
            ModelState = "{}",
            SchedulerVersion = "memorize-v1"
        });

    private async Task<StartSessionTestApplication> StartAsync()
    {
        var app = await StartSessionTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app, UserId);
        return app;
    }

    private void Authenticate(StartSessionTestApplication app, long userId) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(userId, Clock.GetCurrentInstant()));

    private static async Task<JsonNode> StartSessionAsync(StartSessionTestApplication app)
    {
        using var response = await app.Client.PostAsync("/api/v1/sessions", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await ContractAssert.ResponseAsync("startSession", response);
        return await BodyAsync(response);
    }

    private static async Task<JsonNode> GetCurrentAsync(StartSessionTestApplication app)
    {
        using var response = await app.Client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("getCurrentSession", response);
        return await BodyAsync(response);
    }

    private static async Task<JsonNode> GetMissingAsync(StartSessionTestApplication app)
    {
        using var response = await app.Client.GetAsync(Path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ContractAssert.ResponseAsync("getCurrentSession", response);
        var body = await BodyAsync(response);
        Assert.Equal("session.not_found", body["code"]!.GetValue<string>());
        return body;
    }

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
}
