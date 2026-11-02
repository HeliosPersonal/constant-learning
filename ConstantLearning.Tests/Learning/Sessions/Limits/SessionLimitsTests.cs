using System.Data.Common;
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
using Microsoft.EntityFrameworkCore.Diagnostics;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Sessions.Limits;

public sealed class SessionLimitsTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const long UserId = 940000001;
    private static readonly LocalDate Day = new(2026, 1, 15);

    [DockerFact]
    public async Task Ac06_ConfirmedShowsExhaustDailyLimit_ButPlansDoNotConsumeIt()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(12);
        foreach (var entry in course.Take(5)) AddWord(learner, entry, Day);
        AddWord(learner, course[5], Day.PlusDays(-10), Day);
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var response = await StartSessionAsync(app);
        Assert.Contains("new_words_limit_reached", Notices(response));
        Assert.Equal(1, response["total_items"]!.GetValue<int>());
        Assert.Equal("review", response["current_item"]!["kind"]!.GetValue<string>());
        var repeated = await StartSessionAsync(app);
        Assert.Equal(response.ToJsonString(), repeated.ToJsonString());
        Assert.Equal(6, await Db.LearnerWords.CountAsync());
        Assert.Empty(await Db.AnswerEvents.ToArrayAsync());
        Assert.Null((await Db.Learners.AsNoTracking().SingleAsync()).NewWordsAvailableFrom);
    }

    [DockerFact]
    public async Task Ac07_TokyoKeepsWarsawUsageUntilOriginalMidnight_ThenStartsShortDay()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(12);
        foreach (var entry in course.Take(5)) AddWord(learner, entry, Day);
        learner.NewWordsAvailableFrom = Instant.FromUtc(2026, 1, 15, 23, 0);
        await Db.SaveChangesAsync();
        await using (var initial = await StartAsync()) await StartSessionAsync(initial);
        SetTime(Instant.FromUtc(2026, 1, 15, 19, 0));
        await using (var changed = await StartAsync())
        {
            await ChangeSettingsAsync(changed, """{"time_zone":"Asia/Tokyo","daily_new_words_limit":3}""");
            var before = await StartSessionAsync(changed);
            Assert.Contains("new_words_limit_reached", Notices(before));
            Assert.Equal("2026-01-15", before["learner_day"]!.GetValue<string>());
            Assert.Equal(0, before["total_items"]!.GetValue<int>());
        }

        SetTime(Instant.FromUtc(2026, 1, 15, 23, 0));
        await using var restarted = await StartAsync();
        var after = await StartSessionAsync(restarted);
        Assert.Equal("2026-01-16", after["learner_day"]!.GetValue<string>());
        Assert.Equal("2026-01-16T15:00:00Z", after["ends_at"]!.GetValue<string>());
        Assert.Equal(3, (await ItemsAsync(after)).Count(item => item.Kind == "new_word_card"));
        Assert.DoesNotContain("new_words_limit_reached", Notices(after));
        var stored = await Db.Learners.AsNoTracking().SingleAsync();
        Assert.Equal(learner.NewWordsAvailableFrom, stored.CurrentDayStartsAt);
        Assert.Equal(3, stored.DailyNewWordsLimit);
        Assert.Null(stored.PendingDailyNewWordsLimit);
        Assert.Null(stored.PendingLimitEffectiveFrom);
    }

    [DockerFact]
    public async Task Ac54_NewYorkExtendsUsageAndPendingLimitUntilItsMidnight()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(20);
        foreach (var entry in course.Take(5)) AddWord(learner, entry, Day);
        learner.NewWordsAvailableFrom = Instant.FromUtc(2026, 1, 15, 23, 0);
        await Db.SaveChangesAsync();
        await using (var initial = await StartAsync()) await StartSessionAsync(initial);
        SetTime(Instant.FromUtc(2026, 1, 15, 11, 0));
        await using (var changed = await StartAsync())
            await ChangeSettingsAsync(changed, """{"time_zone":"America/New_York","daily_new_words_limit":10}""");

        SetTime(Instant.FromUtc(2026, 1, 16, 0, 0));
        await using (var restarted = await StartAsync())
        {
            var before = await StartSessionAsync(restarted);
            Assert.Equal("2026-01-15", before["learner_day"]!.GetValue<string>());
            Assert.Equal("2026-01-16T05:00:00Z", before["ends_at"]!.GetValue<string>());
            Assert.Equal(0, before["total_items"]!.GetValue<int>());
            Assert.Contains("new_words_limit_reached", Notices(before));
            var stored = await Db.Learners.AsNoTracking().SingleAsync();
            Assert.Equal(5, stored.DailyNewWordsLimit);
            Assert.Equal(Instant.FromUtc(2026, 1, 16, 5, 0), stored.PendingLimitEffectiveFrom);
        }

        SetTime(Instant.FromUtc(2026, 1, 16, 5, 0));
        await using var nextDay = await StartAsync();
        var after = await StartSessionAsync(nextDay);
        Assert.Equal("2026-01-16", after["learner_day"]!.GetValue<string>());
        Assert.Equal(10, (await ItemsAsync(after)).Count(item => item.Kind == "new_word_card"));
        Assert.DoesNotContain("new_words_limit_reached", Notices(after));
    }

    [DockerFact]
    public async Task FutureIssuanceBoundarySuppressesNewWordsEvenWhenCurrentUsageIsZero()
    {
        var learner = await SeedLearnerAsync();
        await SeedCourseAsync(10);
        var boundary = Clock.GetCurrentInstant() + Duration.FromHours(1);
        learner.NewWordsAvailableFrom = boundary;
        await Db.SaveChangesAsync();
        await using (var app = await StartAsync())
        {
            var blocked = await StartSessionAsync(app);
            Assert.Equal(0, blocked["total_items"]!.GetValue<int>());
            Assert.Contains("new_words_limit_reached", Notices(blocked));
            Assert.DoesNotContain("course_completed", Notices(blocked));
        }
        SetTime(boundary);
        await using var restarted = await StartAsync();
        var allowed = await StartSessionAsync(restarted);
        Assert.Equal(5, allowed["total_items"]!.GetValue<int>());
        Assert.DoesNotContain("new_words_limit_reached", Notices(allowed));
        Assert.Empty(await Db.LearnerWords.ToArrayAsync());
    }

    [DockerTheory]
    [InlineData(10, 4)]
    [InlineData(11, 3)]
    [InlineData(12, 2)]
    public async Task OnlyFirstTenKnownWordsAreFree_AndDuplicateEvidenceCountsOnce(int knownCount, int expected)
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(30);
        foreach (var entry in course.Take(knownCount))
        {
            AddWord(learner, entry, Day);
            AddKnown(learner, entry, Day);
        }
        AddKnown(learner, course[0], Day);
        AddWord(learner, course[knownCount], Day);
        // Втрата approved змінює квоти, але не повертає витрачений денний ліміт.
        course[knownCount].PublishedVersionId = null;
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var response = await StartSessionAsync(app);
        Assert.Equal(expected, response["total_items"]!.GetValue<int>());
        Assert.All(await ItemsAsync(response), item => Assert.Equal("new_word_card", item.Kind));
        Assert.Equal(knownCount + 1, await Db.AnswerEvents.CountAsync());
    }

    [DockerFact]
    public async Task Ac09_UsesFullApprovedQuotaDenominatorsHistoricalKnownAndRanks()
    {
        var learner = await SeedLearnerAsync(3);
        var course = await SeedCourseAsync(6);
        course[4].Domain = "legalization";
        course[5].Domain = "legalization";
        AddWord(learner, course[0], Day.PlusDays(-1));
        AddWord(learner, course[1], Day.PlusDays(-1));
        AddKnown(learner, course[4], Day.PlusDays(-1));
        course[0].PublishedVersionId = null;
        var third = await Db.EntryVersions.SingleAsync(version => version.EntryId == course[2].Id);
        var fourth = await Db.EntryVersions.SingleAsync(version => version.EntryId == course[3].Id);
        third.FrequencyRank = 50;
        fourth.FrequencyRank = 1;
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var response = await StartSessionAsync(app);
        Assert.Equal(new[] { course[3].Id, course[5].Id, course[2].Id },
            (await ItemsAsync(response)).Select(item => item.EntryId));
        Assert.DoesNotContain("course_completed", Notices(response));
        Assert.Equal(2, await Db.LearnerWords.CountAsync());
        Assert.Single(await Db.AnswerEvents.ToArrayAsync());
    }

    [DockerFact]
    public async Task Ac09_EqualSharesChooseEverydayBeforeLegalization()
    {
        await SeedLearnerAsync(3);
        var course = await SeedCourseAsync(4);
        course[0].Domain = "legalization";
        course[1].Domain = "legalization";
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        var response = await StartSessionAsync(app);
        Assert.Equal(new[] { course[2].Id, course[0].Id, course[3].Id },
            (await ItemsAsync(response)).Select(item => item.EntryId));
    }

    [DockerFact]
    public async Task Ac10_CompletedCourseContinuesReviews_AndReplenishmentIsDurableAndConsumedOnce()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(2);
        AddWord(learner, course[0], Day.PlusDays(-10), Day);
        AddWord(learner, course[1], Day.PlusDays(-10));
        await Db.SaveChangesAsync();
        await using (var first = await StartAsync())
        {
            var completed = await StartSessionAsync(first);
            Assert.Contains("course_completed", Notices(completed));
            Assert.Equal("review", completed["current_item"]!["kind"]!.GetValue<string>());
        }
        var completedAt = (await Db.Learners.AsNoTracking().SingleAsync()).CourseCompletedAt;
        Assert.Equal(Clock.GetCurrentInstant(), completedAt);
        await Db.LearningSessions.ExecuteUpdateAsync(update => update.SetProperty(session => session.Status, "completed"));
        var added = await SeedCourseAsync(3);
        Clock.Advance(Duration.FromMinutes(1));
        await using (var restarted = await StartAsync())
        {
            var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => StartSessionAsync(restarted)));
            Assert.Single(responses, response => Notices(response).Contains("course_replenished"));
            Assert.All(responses, response => Assert.DoesNotContain("course_completed", Notices(response)));
            Assert.Single(responses.Select(response => response["id"]!.GetValue<Guid>()).Distinct());
            Assert.Equal(added.Select(entry => entry.Id), (await ItemsAsync(responses[0]))
                .Where(item => item.Kind == "new_word_card").Select(item => item.EntryId));
        }
        Assert.Null((await Db.Learners.AsNoTracking().SingleAsync()).CourseCompletedAt);
        await using var again = await StartAsync();
        Assert.DoesNotContain("course_replenished", Notices(await StartSessionAsync(again)));
    }

    [DockerFact]
    public async Task EmptySessionFromExhaustedLimitIsNotCourseCompletion()
    {
        var learner = await SeedLearnerAsync();
        var course = await SeedCourseAsync(6);
        foreach (var entry in course.Take(5)) AddWord(learner, entry, Day);
        await Db.SaveChangesAsync();
        await using (var first = await StartAsync())
        {
            var response = await StartSessionAsync(first);
            Assert.Equal("completed", response["status"]!.GetValue<string>());
            Assert.DoesNotContain("course_completed", Notices(response));
        }
        Assert.Null((await Db.Learners.AsNoTracking().SingleAsync()).CourseCompletedAt);
        await SeedCourseAsync(1);
        await using var restarted = await StartAsync();
        Assert.DoesNotContain("course_replenished", Notices(await StartSessionAsync(restarted)));
    }

    [DockerFact]
    public async Task Ac33_WithdrawalSkipsPendingReview_AndReapprovalRestoresUnchangedHistory()
    {
        var learner = await SeedLearnerAsync();
        var entry = (await SeedCourseAsync(1))[0];
        AddWord(learner, entry, Day.PlusDays(-10), Day);
        AddKnown(learner, entry, Day.PlusDays(-10));
        await Db.SaveChangesAsync();
        var before = await Db.LearnerWords.AsNoTracking().Select(word => new
        {
            word.Status,
            word.FirstShownDay,
            word.DueOn,
            word.IntervalDays,
            word.Lapses,
            word.Version,
            word.ModelState
        }).SingleAsync();
        var answerBefore = await Db.AnswerEvents.AsNoTracking().Select(answer => answer.Id).SingleAsync();
        await using var app = await StartAsync();
        await StartSessionAsync(app);
        var approved = entry.PublishedVersionId;
        entry.PublishedVersionId = null;
        await Db.SaveChangesAsync();
        var removed = await StartSessionAsync(app);
        Assert.Null(removed["current_item"]);
        Assert.Equal("skipped", (await Db.SessionItems.AsNoTracking().SingleAsync()).State);
        entry.PublishedVersionId = approved;
        await Db.SaveChangesAsync();
        var restored = await StartSessionAsync(app);
        Assert.Equal("review", restored["current_item"]!["kind"]!.GetValue<string>());
        Assert.DoesNotContain("course_replenished", Notices(restored));
        Assert.Equal(entry.Id, (await ItemsAsync(restored)).Single().EntryId);
        Assert.Equal(before, await Db.LearnerWords.AsNoTracking().Select(word => new
        {
            word.Status,
            word.FirstShownDay,
            word.DueOn,
            word.IntervalDays,
            word.Lapses,
            word.Version,
            word.ModelState
        }).SingleAsync());
        Assert.Equal(answerBefore, await Db.AnswerEvents.AsNoTracking().Select(answer => answer.Id).SingleAsync());
    }

    [DockerFact]
    public async Task FailedReplenishmentTransactionPreservesNoticeForNextStart()
    {
        await SeedLearnerAsync();
        await using (var first = await StartAsync()) await StartSessionAsync(first);
        var completedAt = (await Db.Learners.AsNoTracking().SingleAsync()).CourseCompletedAt;
        await SeedCourseAsync(2);
        await using (var failed = await StartAsync(new FailingItemsInterceptor()))
            await Assert.ThrowsAsync<DbUpdateException>(() => failed.Client.PostAsync("/api/v1/sessions", null));
        Assert.Equal(completedAt, (await Db.Learners.AsNoTracking().SingleAsync()).CourseCompletedAt);
        Assert.Single(await Db.LearningSessions.ToArrayAsync());
        await using var retry = await StartAsync();
        Assert.Contains("course_replenished", Notices(await StartSessionAsync(retry)));
    }

    private async Task<Learner> SeedLearnerAsync(short limit = 5)
    {
        Clock.Reset(Instant.FromUtc(2026, 1, 15, 9, 0));
        var learner = new Learner
        {
            TelegramUserId = UserId,
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
        var start = await Db.DictionaryEntries.MaxAsync(entry => (int?)entry.CoursePosition) ?? 0;
        for (var index = 1; index <= count; index++)
        {
            var entry = new DictionaryEntry
            {
                ExternalId = Guid.NewGuid().ToString("N"),
                Domain = "everyday",
                CoursePosition = start + index
            };
            var version = ReportsPublicationTestData.NewVersion(entry);
            version.FrequencyRank = start + index;
            Db.EntryVersions.Add(version);
            Db.VersionTranslations.Add(new() { VersionId = version.Id, Kind = "canonical", TextUk = "жаба", Position = 0 });
            await Db.SaveChangesAsync();
            entry.PublishedVersionId = version.Id;
            entries.Add(entry);
        }
        await Db.SaveChangesAsync();
        return entries.ToArray();
    }

    private void AddWord(Learner learner, DictionaryEntry entry, LocalDate shown, LocalDate? due = null) =>
        Db.LearnerWords.Add(new LearnerWord
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            Status = "review",
            FirstShownDay = shown,
            DueOn = due ?? Day.PlusDays(1),
            IntervalDays = 2,
            ModelState = "{}",
            SchedulerVersion = "memorize-v1"
        });

    private void AddKnown(Learner learner, DictionaryEntry entry, LocalDate day) =>
        Db.AnswerEvents.Add(new AnswerEvent
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            VersionId = entry.PublishedVersionId!.Value,
            ExerciseKind = "already_known",
            Outcome = "already_known",
            LearnerDay = day,
            AnsweredAt = Clock.GetCurrentInstant(),
            TimeZone = "Europe/Warsaw",
            SchedulerVersion = "memorize-v1",
            StateBefore = "{}"
        });

    private void SetTime(Instant now) => Clock.Advance(now - Clock.GetCurrentInstant());

    private async Task<StartSessionTestApplication> StartAsync(IInterceptor? interceptor = null)
    {
        var app = await StartSessionTestApplication.StartAsync(_postgres.ConnectionString, Clock, interceptor);
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(UserId, Clock.GetCurrentInstant()));
        return app;
    }

    private static async Task<JsonNode> StartSessionAsync(StartSessionTestApplication app)
    {
        using var response = await app.Client.PostAsync("/api/v1/sessions", null);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);
        await ContractAssert.ResponseAsync("startSession", response);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    private static async Task ChangeSettingsAsync(StartSessionTestApplication app, string json)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await app.Client.PatchAsync("/api/v1/learners/me/settings", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("updateLearnerSettings", response);
    }

    private Task<SessionItem[]> ItemsAsync(JsonNode response)
    {
        var sessionId = response["id"]!.GetValue<Guid>();
        return Db.SessionItems.AsNoTracking().Where(item => item.SessionId == sessionId)
            .OrderBy(item => item.Position).ToArrayAsync();
    }

    private static string[] Notices(JsonNode response) => response["notices"]!.AsArray()
        .Select(node => node!.GetValue<string>()).ToArray();

    private sealed class DockerTheoryAttribute : TheoryAttribute
    {
        public DockerTheoryAttribute() => Skip = DockerAvailability.GetSkipReason(
            DockerAvailability.IsCi, DockerAvailability.UnavailableReason);
    }

    private sealed class FailingItemsInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO session_items", StringComparison.Ordinal))
                throw new InvalidOperationException("Тестова відмова запису карток після поповнення курсу.");
            return ValueTask.FromResult(result);
        }
    }
}
