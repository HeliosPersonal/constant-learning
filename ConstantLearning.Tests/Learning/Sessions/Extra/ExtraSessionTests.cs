using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Tests.Content.Data;
using ConstantLearning.Tests.Infrastructure;
using ConstantLearning.Tests.Learning.Sessions.Start;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Sessions.Extra;

/// <summary>
/// Крайові й помилкові випадки extra-дій (unlimited-learning T11): ідемпотентність, добір практики,
/// виключення слів з відповіддю сьогодні чи без approved, авторизація й те, що extra-session не є
/// completed session. Доба learner — Europe/Warsaw, тож UTC-межа доби — 23:00.
/// </summary>
public sealed class ExtraSessionTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const long UserId = 940000001;
    private const string ExtraWordPath = "/api/v1/sessions/extra-word";
    private const string ExtraPracticePath = "/api/v1/sessions/extra-practice";
    private const string TodayPath = "/api/v1/today";
    private static readonly LocalDate Day = new(2026, 10, 31);

    [DockerFact]
    public async Task Ac05_DoubleTapReturnsTheSameCardWithoutObtainingASecondWord()
    {
        var learner = await SeedLearnerAsync(limit: 3);
        var course = await SeedCourseAsync(6);
        ExhaustDailyLimit(learner, course, 3);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var first = await app.Client.PostAsync(ExtraWordPath, null);
        await AssertResponseAsync(first, "startExtraWord", HttpStatusCode.Created);
        var created = await BodyAsync(first);
        Assert.Equal("extra_word", created["kind"]!.GetValue<string>());
        Assert.NotNull(created["current_item"]!["card"]);
        var sessionId = created["id"]!.GetValue<Guid>();
        var cardLemma = created["current_item"]!["card"]!["lemma_pl"]!.GetValue<string>();

        using var repeat = await app.Client.PostAsync(ExtraWordPath, null);
        await AssertResponseAsync(repeat, "startExtraWord", HttpStatusCode.OK);
        var resumed = await BodyAsync(repeat);
        Assert.Equal(sessionId, resumed["id"]!.GetValue<Guid>());
        Assert.Equal(cardLemma, resumed["current_item"]!["card"]!["lemma_pl"]!.GetValue<string>());

        Assert.Single(await Db.LearningSessions.Where(session => session.Kind == "extra_word").ToArrayAsync());
        Assert.Single(await Db.SessionItems.Where(item => item.SessionId == sessionId).ToArrayAsync());
    }

    [DockerFact]
    public async Task Ac05_OpenExtraSessionFromAnotherDeviceResumesInsteadOfStartingNew()
    {
        var learner = await SeedLearnerAsync(limit: 3);
        var course = await SeedCourseAsync(6);
        ExhaustDailyLimit(learner, course, 3);
        var open = new LearningSession
        {
            LearnerId = learner.Id,
            LearnerDay = Day,
            EndsAt = Instant.FromUtc(2026, 10, 31, 23, 0),
            Status = "active",
            Kind = "extra_word",
            LastActivityAt = Clock.GetCurrentInstant(),
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.LearningSessions.Add(open);
        Db.SessionItems.Add(new SessionItem
        {
            SessionId = open.Id,
            EntryId = course[3].Id,
            Position = 1,
            Kind = "new_word_card",
            State = "pending",
            CreatedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var response = await app.Client.PostAsync(ExtraWordPath, null);
        await AssertResponseAsync(response, "startExtraWord", HttpStatusCode.OK);
        Assert.Equal(open.Id, (await BodyAsync(response))["id"]!.GetValue<Guid>());
        Assert.Single(await Db.LearningSessions.Where(session => session.Kind == "extra_word").ToArrayAsync());
    }

    [DockerFact]
    public async Task PlanNotDoneBlocksBothExtraActionsWithoutLeakingState()
    {
        var learner = await SeedLearnerAsync(limit: 3);
        var course = await SeedCourseAsync(6);
        // Слово, яке підлягає повторенню сьогодні, означає, що план дня ще не виконано.
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var word = await app.Client.PostAsync(ExtraWordPath, null);
        await AssertResponseAsync(word, "startExtraWord", HttpStatusCode.Conflict);
        Assert.Equal("extra.plan_not_done", (await BodyAsync(word))["code"]!.GetValue<string>());

        using var practice = await app.Client.PostAsync(ExtraPracticePath, null);
        await AssertResponseAsync(practice, "startExtraPractice", HttpStatusCode.Conflict);
        Assert.Equal("extra.plan_not_done", (await BodyAsync(practice))["code"]!.GetValue<string>());

        Assert.Empty(await Db.LearningSessions.ToArrayAsync());
    }

    [DockerFact]
    public async Task Ac09_PracticeSkipsWordsAnsweredOrObtainedTodayAndOrdersByNextReview()
    {
        var learner = await SeedLearnerAsync(limit: 3);
        var course = await SeedCourseAsync(8);
        learner.NewWordsAvailableFrom = Clock.GetCurrentInstant() + Duration.FromHours(2);
        // Вивчені слова, отримані раніше, з датою повторення в майбутньому (план виконано, слів до повторення немає).
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(1));
        AddLearnerWord(learner, course[1], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(2));
        AddLearnerWord(learner, course[2], "recalled", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(3));
        // Слово з відповіддю сьогодні — виключається з практики цієї доби.
        AddLearnerWord(learner, course[3], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(2));
        Db.AnswerEvents.Add(new AnswerEvent
        {
            LearnerId = learner.Id,
            EntryId = course[3].Id,
            VersionId = course[3].PublishedVersionId!.Value,
            ExerciseKind = "review",
            Outcome = "correct",
            LearnerDay = Day,
            AnsweredAt = Clock.GetCurrentInstant(),
            TimeZone = "Europe/Warsaw",
            SchedulerVersion = "memorize-v1",
            StateBefore = "{}"
        });
        // Слово, отримане сьогодні (показане сьогодні) — також не потрапляє в практику.
        AddLearnerWord(learner, course[4], "review", firstShown: Day, dueOn: Day.PlusDays(2));
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var response = await app.Client.PostAsync(ExtraPracticePath, null);
        await AssertResponseAsync(response, "startExtraPractice", HttpStatusCode.Created);
        var body = await BodyAsync(response);
        Assert.Equal("extra_practice", body["kind"]!.GetValue<string>());

        var items = await Db.SessionItems.AsNoTracking()
            .Where(item => item.SessionId == body["id"]!.GetValue<Guid>())
            .OrderBy(item => item.Position).ToArrayAsync();
        Assert.Equal(new[] { course[0].Id, course[1].Id, course[2].Id }, items.Select(item => item.EntryId));
        Assert.All(items, item => Assert.Equal("extra_practice", item.Kind));
    }

    [DockerFact]
    public async Task Ac11_PracticeExcludesWordWhoseVersionLostApproval()
    {
        var learner = await SeedLearnerAsync(limit: 3);
        var course = await SeedCourseAsync(8);
        learner.NewWordsAvailableFrom = Clock.GetCurrentInstant() + Duration.FromHours(2);
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(1));
        AddLearnerWord(learner, course[1], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(2));
        AddLearnerWord(learner, course[2], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(3));
        await Db.SaveChangesAsync();
        // Лема втрачає approved у словнику — її не можна пропонувати для практики.
        course[1].PublishedVersionId = null;
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var response = await app.Client.PostAsync(ExtraPracticePath, null);
        await AssertResponseAsync(response, "startExtraPractice", HttpStatusCode.Created);
        var sessionId = (await BodyAsync(response))["id"]!.GetValue<Guid>();
        var items = await Db.SessionItems.AsNoTracking()
            .Where(item => item.SessionId == sessionId)
            .OrderBy(item => item.Position).ToArrayAsync();
        Assert.Equal(new[] { course[0].Id, course[2].Id }, items.Select(item => item.EntryId));
    }

    [DockerFact]
    public async Task Ac18_ExtraSessionsDoNotCountAsCompletedSessionsOnToday()
    {
        var learner = await SeedLearnerAsync(limit: 3);
        var course = await SeedCourseAsync(6);
        ExhaustDailyLimit(learner, course, 3);
        var now = Clock.GetCurrentInstant();
        Db.LearningSessions.AddRange(
            CompletedSession(learner, "plan", now),
            CompletedSession(learner, "extra_word", now),
            CompletedSession(learner, "extra_practice", now));
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var response = await app.Client.GetAsync(TodayPath);
        await AssertResponseAsync(response, "getToday", HttpStatusCode.OK);
        var body = await BodyAsync(response);
        Assert.Equal(1, body["completed_sessions_last_7_days"]!.GetValue<int>());
    }

    [DockerFact]
    public async Task Ac19_ExtraActionsAreAuthorizedAndIsolatedPerLearner()
    {
        var learner = await SeedLearnerAsync(limit: 3);
        var course = await SeedCourseAsync(6);
        ExhaustDailyLimit(learner, course, 3);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var owned = await app.Client.PostAsync(ExtraWordPath, null);
        await AssertResponseAsync(owned, "startExtraWord", HttpStatusCode.Created);

        app.Client.DefaultRequestHeaders.Authorization = null;
        using var anonymous = await app.Client.PostAsync(ExtraWordPath, null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // Інший Telegram-користувач без онбордингу бачить 404 (існування чужих даних приховано).
        Authenticate(app, UserId + 1);
        using var missing = await app.Client.PostAsync(ExtraWordPath, null);
        await AssertResponseAsync(missing, "startExtraWord", HttpStatusCode.NotFound);

        // Другий learner має власне слово до повторення: його план не виконано, а чужий стан на це не впливає.
        var other = await SeedLearnerAsync(limit: 3, userId: UserId + 1);
        AddLearnerWord(other, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day);
        await Db.SaveChangesAsync();
        using var blocked = await app.Client.PostAsync(ExtraWordPath, null);
        await AssertResponseAsync(blocked, "startExtraWord", HttpStatusCode.Conflict);
        Assert.Equal("extra.plan_not_done", (await BodyAsync(blocked))["code"]!.GetValue<string>());
        Assert.Single(await Db.LearningSessions.ToArrayAsync());
    }

    [DockerFact]
    public async Task Ac02_ExtraWordBypassesTheDailyLimitAndDoesNotConsumeTomorrowsQuota()
    {
        var learner = await SeedLearnerAsync(limit: 3);
        var course = await SeedCourseAsync(6);
        ExhaustDailyLimit(learner, course, 3);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        // План виконано, денний ліміт нових слів вичерпано, але «ще одне слово» лишається доступним (AC-02).
        using var todayBefore = await app.Client.GetAsync(TodayPath);
        await AssertResponseAsync(todayBefore, "getToday", HttpStatusCode.OK);
        var before = await BodyAsync(todayBefore);
        Assert.False(before["new_words_available_today"]!.GetValue<bool>());
        Assert.True(before["extra_actions"]!["plan_done"]!.GetValue<bool>());
        Assert.True(before["extra_actions"]!["extra_new_word"]!["available"]!.GetValue<bool>());

        using var started = await app.Client.PostAsync(ExtraWordPath, null);
        await AssertResponseAsync(started, "startExtraWord", HttpStatusCode.Created);
        var sessionId = (await BodyAsync(started))["id"]!.GetValue<Guid>();
        var card = await Db.SessionItems.AsNoTracking().SingleAsync(item => item.SessionId == sessionId);
        using var acknowledged = await app.Client.PostAsync($"/api/v1/sessions/{sessionId}/items/{card.Id}/acknowledge", null);
        await AssertResponseAsync(acknowledged, "acknowledgeNewWordCard", HttpStatusCode.OK);

        // Нова доба: extra-слово, взяте вчора, не з'їдає план наступного дня — ліміт знову доступний.
        Clock.Advance(Duration.FromHours(15));
        using var todayNext = await app.Client.GetAsync(TodayPath);
        await AssertResponseAsync(todayNext, "getToday", HttpStatusCode.OK);
        Assert.True((await BodyAsync(todayNext))["new_words_available_today"]!.GetValue<bool>());
    }

    [DockerFact]
    public async Task Ac07_ExtraWordCardLeftUnansweredAtDayBoundaryClosesWithoutObtainingAWord()
    {
        var learner = await SeedLearnerAsync(limit: 3);
        var course = await SeedCourseAsync(6);
        ExhaustDailyLimit(learner, course, 3);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var started = await app.Client.PostAsync(ExtraWordPath, null);
        await AssertResponseAsync(started, "startExtraWord", HttpStatusCode.Created);
        var sessionId = (await BodyAsync(started))["id"]!.GetValue<Guid>();
        var card = await Db.SessionItems.AsNoTracking().SingleAsync(item => item.SessionId == sessionId);

        // Межа доби настає до відповіді на картку.
        Clock.Advance(Duration.FromHours(15));
        using var current = await app.Client.GetAsync("/api/v1/sessions/current");
        Assert.Equal(HttpStatusCode.NotFound, current.StatusCode);

        var closed = await Db.LearningSessions.AsNoTracking().SingleAsync(session => session.Id == sessionId);
        Assert.Equal("incomplete", closed.Status);
        // Закриття по межі доби НЕ матеріалізує LearnerWord: слово не вважається отриманим і лишається в курсі.
        // Це суперечить формулюванню AC-07 ("слово стає отриманим") — реалізація навмисно нічого не фіксує
        // (SessionExpiryGuard лише виставляє Status='incomplete'); розбіжність передано власникам специфікації.
        Assert.False(await Db.LearnerWords.AsNoTracking().AnyAsync(word => word.EntryId == card.EntryId));
    }

    [DockerFact]
    public async Task Ac20_PracticePartlyAnsweredAtDayBoundaryLeavesUntouchedWordsEligibleNextDay()
    {
        var learner = await SeedLearnerAsync(limit: 3);
        var course = await SeedCourseAsync(6);
        learner.NewWordsAvailableFrom = Clock.GetCurrentInstant() + Duration.FromDays(30);
        AddLearnerWord(learner, course[0], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(5));
        AddLearnerWord(learner, course[1], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(6));
        AddLearnerWord(learner, course[2], "review", firstShown: Day.PlusDays(-5), dueOn: Day.PlusDays(7));
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        using var started = await app.Client.PostAsync(ExtraPracticePath, null);
        await AssertResponseAsync(started, "startExtraPractice", HttpStatusCode.Created);
        var sessionId = (await BodyAsync(started))["id"]!.GetValue<Guid>();
        var items = await Db.SessionItems.AsNoTracking().Where(item => item.SessionId == sessionId)
            .OrderBy(item => item.Position).ToArrayAsync();
        Assert.Equal(new[] { course[0].Id, course[1].Id, course[2].Id }, items.Select(item => item.EntryId));

        // Відповідаємо лише на перше слово; решта лишається pending.
        using var answered = await app.Client.PostAsync(
            $"/api/v1/sessions/{sessionId}/items/{items[0].Id}/answer",
            new StringContent("""{"answer":"slowo1","dont_know":false}""", System.Text.Encoding.UTF8, "application/json"));
        await AssertResponseAsync(answered, "answerSessionItem", HttpStatusCode.OK);

        // Межа доби закриває заняття.
        Clock.Advance(Duration.FromHours(15));
        using var current = await app.Client.GetAsync("/api/v1/sessions/current");
        Assert.Equal(HttpStatusCode.NotFound, current.StatusCode);
        Assert.Equal("incomplete", (await Db.LearningSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId)).Status);
        // Невідповіджені слова не отримали жодного AnswerEvent.
        var answeredEntries = await Db.AnswerEvents.AsNoTracking().Select(answer => answer.EntryId).ToArrayAsync();
        Assert.Equal(new[] { course[0].Id }, answeredEntries);

        // Наступного дня незаймані слова знову доступні для практики.
        using var nextDay = await app.Client.PostAsync(ExtraPracticePath, null);
        await AssertResponseAsync(nextDay, "startExtraPractice", HttpStatusCode.Created);
        var nextSessionId = (await BodyAsync(nextDay))["id"]!.GetValue<Guid>();
        var nextEntries = await Db.SessionItems.AsNoTracking().Where(item => item.SessionId == nextSessionId)
            .Select(item => item.EntryId).ToArrayAsync();
        Assert.Contains(course[1].Id, nextEntries);
        Assert.Contains(course[2].Id, nextEntries);
    }

    private static LearningSession CompletedSession(Learner learner, string kind, Instant at) => new()
    {
        LearnerId = learner.Id,
        LearnerDay = Day,
        EndsAt = Instant.FromUtc(2026, 10, 31, 23, 0),
        Status = "completed",
        Kind = kind,
        CompletedAt = at,
        LastActivityAt = at,
        CreatedAt = at
    };

    private void ExhaustDailyLimit(Learner learner, IReadOnlyList<DictionaryEntry> course, int limit)
    {
        // Слова, показані сьогодні за планом, вичерпують денний ліміт нових слів, тож план виконано,
        // але вони не придатні для практики (отримані сьогодні), а в курсі лишаються непоказані слова.
        for (var index = 0; index < limit; index++)
            AddLearnerWord(learner, course[index], "review", firstShown: Day, dueOn: Day.PlusDays(1));
    }

    private LearnerWord AddLearnerWord(Learner learner, DictionaryEntry entry, string status,
        LocalDate firstShown, LocalDate dueOn, int interval = 2)
    {
        var word = new LearnerWord
        {
            LearnerId = learner.Id,
            EntryId = entry.Id,
            Status = status,
            FirstShownDay = firstShown,
            LastReviewDay = firstShown,
            DueOn = dueOn,
            IntervalDays = interval,
            ModelState = "{}",
            SchedulerVersion = "memorize-v1"
        };
        Db.LearnerWords.Add(word);
        return word;
    }

    private async Task<Learner> SeedLearnerAsync(short limit = 3, long userId = UserId)
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
            version.LemmaPl = $"slowo{index}";
            version.FrequencyRank = index;
            Db.EntryVersions.Add(version);
            Db.VersionTranslations.Add(new() { VersionId = version.Id, Kind = "canonical", TextUk = "жаба", Position = 0 });
            Db.VersionAcceptedAnswers.Add(new() { VersionId = version.Id, TextPl = version.LemmaPl, IsCanonical = true });
            await Db.SaveChangesAsync();
            entry.PublishedVersionId = version.Id;
            entries.Add(entry);
        }
        await Db.SaveChangesAsync();
        return entries.ToArray();
    }

    private async Task<StartSessionTestApplication> StartAsync()
    {
        var app = await StartSessionTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app, UserId);
        return app;
    }

    private void Authenticate(StartSessionTestApplication app, long userId) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(userId, Clock.GetCurrentInstant()));

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private static async Task AssertResponseAsync(HttpResponseMessage response, string operationId, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        await ContractAssert.ResponseAsync(operationId, response);
    }
}
