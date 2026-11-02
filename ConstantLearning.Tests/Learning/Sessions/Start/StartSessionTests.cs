using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Sessions.Start;

public sealed class StartSessionTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const long UserId = 930000001;
    private const string Path = "/api/v1/sessions";
    private static readonly LocalDate Day = new(2026, 10, 31);

    [DockerFact]
    public Task Ac03_FourReviewsThenThreeNewWords() => AssertCompositionAsync(4, 3, 4, 3);

    [DockerFact]
    public Task Ac04_ThirtyFiveDueWordsSelectTwentyMostOverdue() => AssertCompositionAsync(35, 5, 20, 0);

    [DockerFact]
    public Task Ac05_SixteenReviewsLeaveFourPlacesForNewWords() => AssertCompositionAsync(16, 10, 16, 4);

    private async Task AssertCompositionAsync(int dueCount, short limit, int reviews, int newWords)
    {
        var learner = await SeedLearnerAsync(limit);
        var course = await SeedCourseAsync(dueCount + 15);
        SeedReviews(learner, course.Take(dueCount));
        await Db.SaveChangesAsync();
        var before = await Db.LearnerWords.AsNoTracking().OrderBy(word => word.EntryId)
            .Select(word => new { word.EntryId, word.Version, word.DueOn, word.Lapses, word.ModelState }).ToArrayAsync();
        await using var app = await StartAsync();
        using var response = await app.Client.PostAsync(Path, null);
        await AssertResponseAsync(response, HttpStatusCode.Created);
        var body = await BodyAsync(response);
        Assert.Equal(reviews + newWords, body["total_items"]!.GetValue<int>());
        Assert.Equal("2026-10-31", body["learner_day"]!.GetValue<string>());
        Assert.Equal("2026-10-31T23:00:00Z", body["ends_at"]!.GetValue<string>());
        var items = await Db.SessionItems.AsNoTracking().OrderBy(item => item.Position).ToArrayAsync();
        Assert.Equal(Enumerable.Range(1, items.Length), items.Select(item => (int)item.Position));
        Assert.Equal(Enumerable.Repeat("review", reviews).Concat(Enumerable.Repeat("new_word_card", newWords)), items.Select(item => item.Kind));
        Assert.Equal(course.Take(dueCount).Reverse().Take(reviews).Select(entry => entry.Id), items.Take(reviews).Select(item => item.EntryId));
        Assert.All(items, item => Assert.Equal("pending", item.State));
        Assert.Equal(before, await Db.LearnerWords.AsNoTracking().OrderBy(word => word.EntryId)
            .Select(word => new { word.EntryId, word.Version, word.DueOn, word.Lapses, word.ModelState }).ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Null(body["current_item"]!["card"]);
        Assert.DoesNotContain("accepted_answers", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [DockerFact]
    public async Task Ac08_FiveMissedDaysDoNotCreateMistakesOrAdditionalSessions()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(25);
        SeedReviews(learner, course);
        await Db.SaveChangesAsync();
        Clock.Advance(Duration.FromDays(5));
        await using var app = await StartAsync();
        using var response = await app.Client.PostAsync(Path, null);
        await AssertResponseAsync(response, HttpStatusCode.Created);
        Assert.Equal(20, await Db.SessionItems.CountAsync());
        Assert.Single(await Db.LearningSessions.ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.All(await Db.LearnerWords.AsNoTracking().ToArrayAsync(), word => Assert.Equal(0, word.Lapses));
    }

    [DockerFact]
    public async Task ParallelStartsCreateOneSessionAndReturnIdenticalOrderedCards()
    {
        await SeedLearnerAsync();
        await SeedCourseAsync(10);
        await using var app = await StartAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => app.Client.PostAsync(Path, null)));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Equal(7, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            foreach (var response in responses) await ContractAssert.ResponseAsync("startSession", response);
            var bodies = await Task.WhenAll(responses.Select(BodyAsync));
            Assert.Single(bodies.Select(body => body.ToJsonString()).Distinct());
            Assert.Single(await Db.LearningSessions.Where(session => session.Status == "active").ToArrayAsync());
            Assert.Equal(5, await Db.SessionItems.CountAsync());
            Assert.Empty(await Db.LearnerWords.ToArrayAsync());
            Assert.Equal("żaba", bodies[0]["current_item"]!["card"]!["lemma_pl"]!.GetValue<string>());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [DockerFact]
    public async Task RepeatedStartResumesNextPendingItemAndPreservesOriginalDayBoundary()
    {
        var learner = await SeedLearnerAsync();
        await SeedCourseAsync(10);
        Guid sessionId;
        await using (var app = await StartAsync())
        {
            using var response = await app.Client.PostAsync(Path, null);
            await AssertResponseAsync(response, HttpStatusCode.Created);
            sessionId = (await BodyAsync(response))["id"]!.GetValue<Guid>();
        }
        var first = await Db.SessionItems.OrderBy(item => item.Position).FirstAsync();
        first.State = "answered";
        await Db.Entry(learner).ReloadAsync();
        learner.TimeZone = "America/New_York";
        await Db.SaveChangesAsync();
        await using var restarted = await StartAsync();
        using var resumed = await restarted.Client.PostAsync(Path, null);
        await AssertResponseAsync(resumed, HttpStatusCode.OK);
        var body = await BodyAsync(resumed);
        Assert.Equal(sessionId, body["id"]!.GetValue<Guid>());
        Assert.Equal(2, body["current_item"]!["position"]!.GetValue<int>());
        Assert.Equal(1, body["completed_items"]!.GetValue<int>());
        Assert.Equal("2026-10-31T23:00:00Z", body["ends_at"]!.GetValue<string>());
        Assert.Equal(5, await Db.SessionItems.CountAsync());
    }

    [DockerFact]
    public async Task ExpiredActiveSessionDoesNotBlockStartAndUnshownWordsReturn()
    {
        await SeedLearnerAsync();
        await SeedCourseAsync(10);
        await using (var app = await StartAsync())
        {
            using var response = await app.Client.PostAsync(Path, null);
            await AssertResponseAsync(response, HttpStatusCode.Created);
        }
        var original = await Db.LearningSessions.AsNoTracking().SingleAsync();
        var oldWords = await Db.SessionItems.OrderBy(item => item.Position).Select(item => item.EntryId).ToArrayAsync();
        Clock.Advance(original.EndsAt - Clock.GetCurrentInstant());
        await using var nextDay = await StartAsync();
        using var next = await nextDay.Client.PostAsync(Path, null);
        await AssertResponseAsync(next, HttpStatusCode.Created);
        Assert.Equal("incomplete", (await Db.LearningSessions.AsNoTracking().SingleAsync(session => session.Id == original.Id)).Status);
        var active = await Db.LearningSessions.AsNoTracking().SingleAsync(session => session.Status == "active");
        Assert.Equal(Day.PlusDays(1), active.LearnerDay);
        Assert.Equal(oldWords, await Db.SessionItems.Where(item => item.SessionId == active.Id)
            .OrderBy(item => item.Position).Select(item => item.EntryId).ToArrayAsync());
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());
    }

    [DockerFact]
    public async Task PublishedApprovalAndCurrentVersionControlDisplayedContent()
    {
        await SeedLearnerAsync();
        var course = await SeedCourseAsync(4);
        course[0].PublishedVersionId = null;
        var hidden = await Db.EntryVersions.SingleAsync(version => version.EntryId == course[1].Id);
        hidden.ValidationStatus = "draft";
        var shown = await Db.EntryVersions.SingleAsync(version => version.EntryId == course[2].Id);
        shown.ShowContextBefore = true;
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        using var response = await app.Client.PostAsync(Path, null);
        await AssertResponseAsync(response, HttpStatusCode.Created);
        Assert.Equal(2, await Db.SessionItems.CountAsync());
        Assert.Equal("Це жаба.", (await BodyAsync(response))["current_item"]!["context_uk"]!.GetValue<string>());
        var replacement = ReportsPublicationTestData.NewVersion(course[2]);
        replacement.LemmaPl = "kot";
        Db.EntryVersions.Add(replacement);
        Db.VersionTranslations.Add(new() { VersionId = replacement.Id, Kind = "canonical", TextUk = "кіт", Position = 0 });
        await Db.SaveChangesAsync();
        course[2].PublishedVersionId = replacement.Id;
        await Db.SaveChangesAsync();
        using var repeated = await app.Client.PostAsync(Path, null);
        await AssertResponseAsync(repeated, HttpStatusCode.OK);
        Assert.Equal("kot", (await BodyAsync(repeated))["current_item"]!["card"]!["lemma_pl"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task PendingLimitAppliesAndFutureNewWordsBoundarySuppressesNewCards()
    {
        var learner = await SeedLearnerAsync();
        learner.PendingDailyNewWordsLimit = 3;
        learner.PendingLimitEffectiveFrom = Clock.GetCurrentInstant();
        learner.NewWordsAvailableFrom = Clock.GetCurrentInstant() + Duration.FromHours(2);
        await SeedCourseAsync(10);
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        using var response = await app.Client.PostAsync(Path, null);
        await AssertResponseAsync(response, HttpStatusCode.Created);
        var body = await BodyAsync(response);
        Assert.Equal(0, body["total_items"]!.GetValue<int>());
        Assert.Equal("completed", body["status"]!.GetValue<string>());
        Assert.Null(body["current_item"]);
        Assert.Contains("new_words_limit_reached", body["notices"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Equal(3, (await Db.Learners.AsNoTracking().SingleAsync()).DailyNewWordsLimit);
    }

    [DockerFact]
    public async Task DailyUsageCountsShownWordsOnceAndNeutralAttemptDoesNotRepeatToday()
    {
        var learner = await SeedLearnerAsync(3);
        var course = await SeedCourseAsync(8);
        SeedReviews(learner, course.Take(5));
        var words = Db.ChangeTracker.Entries<LearnerWord>().Select(entry => entry.Entity).ToArray();
        foreach (var word in words.Take(3))
        {
            word.FirstShownDay = Day;
            word.DueOn = Day.PlusDays(1);
        }
        words[3].ControlPending = true;
        foreach (var (entry, kind, outcome) in new[]
        {
            (course[0], "already_known", "already_known"), (course[4], "review", "synonym")
        })
            Db.AnswerEvents.Add(new AnswerEvent
            {
                LearnerId = learner.Id,
                EntryId = entry.Id,
                VersionId = entry.PublishedVersionId!.Value,
                ExerciseKind = kind,
                Outcome = outcome,
                LearnerDay = Day,
                AnsweredAt = Clock.GetCurrentInstant(),
                TimeZone = "Europe/Warsaw",
                SchedulerVersion = "memorize-v1",
                StateBefore = "{}"
            });
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        using var response = await app.Client.PostAsync(Path, null);
        await AssertResponseAsync(response, HttpStatusCode.Created);
        var items = await Db.SessionItems.AsNoTracking().OrderBy(item => item.Position).ToArrayAsync();
        Assert.Equal(new[] { "control_repetition", "new_word_card" }, items.Select(item => item.Kind));
        Assert.Equal(course[3].Id, items[0].EntryId);
        Assert.Equal(course[5].Id, items[1].EntryId);
        Assert.Equal(5, await Db.LearnerWords.CountAsync());
        Assert.Equal(2, await Db.AnswerEvents.CountAsync());
    }

    [DockerFact]
    public async Task AuthenticationAndLearnerIsolationUseContractResponses()
    {
        await SeedLearnerAsync();
        await SeedCourseAsync(5);
        await using var app = await StartAsync();
        using var own = await app.Client.PostAsync(Path, null);
        await AssertResponseAsync(own, HttpStatusCode.Created);
        app.Client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await app.Client.PostAsync(Path, null);
        await AssertResponseAsync(anonymous, HttpStatusCode.Unauthorized);
        Authenticate(app, UserId + 1);
        using var missing = await app.Client.PostAsync(Path, null);
        await AssertResponseAsync(missing, HttpStatusCode.NotFound);
        await SeedLearnerAsync(userId: UserId + 1);
        using var other = await app.Client.PostAsync(Path, null);
        await AssertResponseAsync(other, HttpStatusCode.Created);
        Assert.NotEqual((await BodyAsync(own))["id"]!.GetValue<Guid>(), (await BodyAsync(other))["id"]!.GetValue<Guid>());
    }

    [DockerFact]
    public async Task FailedCardPersistenceRollsBackSessionAndLearnerDay()
    {
        await SeedLearnerAsync();
        await SeedCourseAsync(5);
        await using var app = await StartAsync(new FailingItemsInterceptor());
        await Assert.ThrowsAsync<DbUpdateException>(() => app.Client.PostAsync(Path, null));
        Assert.Empty(await Db.LearningSessions.AsNoTracking().ToArrayAsync());
        Assert.Empty(await Db.SessionItems.AsNoTracking().ToArrayAsync());
        Assert.Null((await Db.Learners.AsNoTracking().SingleAsync()).CurrentDayDate);
    }

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

    private void SeedReviews(Learner learner, IEnumerable<DictionaryEntry> entries)
    {
        foreach (var entry in entries)
            Db.LearnerWords.Add(new LearnerWord
            {
                LearnerId = learner.Id,
                EntryId = entry.Id,
                Status = "review",
                FirstShownDay = Day.PlusDays(-100),
                DueOn = Day.PlusDays(-entry.CoursePosition!.Value),
                IntervalDays = 2,
                ModelState = "{}",
                SchedulerVersion = "memorize-v1"
            });
    }

    private async Task<StartSessionTestApplication> StartAsync(IInterceptor? interceptor = null)
    {
        var app = await StartSessionTestApplication.StartAsync(_postgres.ConnectionString, Clock, interceptor);
        Authenticate(app, UserId);
        return app;
    }

    private void Authenticate(StartSessionTestApplication app, long userId) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(userId, Clock.GetCurrentInstant()));

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private static async Task AssertResponseAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        await ContractAssert.ResponseAsync("startSession", response);
    }

    private sealed class FailingItemsInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO session_items", StringComparison.Ordinal))
                throw new InvalidOperationException("Тестова відмова запису карток.");
            return ValueTask.FromResult(result);
        }
    }
}
