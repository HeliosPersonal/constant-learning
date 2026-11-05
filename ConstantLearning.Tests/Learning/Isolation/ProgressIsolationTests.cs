using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Infrastructure;
using ConstantLearning.Tests.Learning.Sessions.Answer;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Tests.Learning.Isolation;

// T10 — облік прогресу ізольований за (learner_id, target_language). Денормалізований дискримінатор
// target_language (ADR-0001) фільтрує кожне читання/запис прогресу, тож мова A ніколи не торкається мови B,
// і learner не бачить чужого прогресу (AC-04, AC-05, AC-10). Сесійні сервіси доступні лише через HTTP-хост
// (базовий Services не реєструє AddLearning), тож живий pl-потік ганяємо через AnswerTestApplication,
// а мово-сусідні рядки (en) засіваємо напряму в Db й перевіряємо недоторканість на рівні таблиць.
public sealed class ProgressIsolationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task AnswerInOneLanguageTouchesNoCounterOfAnother()
    {
        // DoD #1: відповідь у мові A не змінює жоден лічильник мови B (0 витоків — NFR ізоляції).
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, count: 3, session: true);
        var en = await SeedEnEntryAsync("kat", 1);
        var enWord = AddLearnerWord(fixture.Learner.Id, en.Entry.Id, "en");
        AddAnswerEvent(fixture.Learner.Id, en.Entry.Id, en.Version.Id, "en");
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        var current = await CurrentAsync(app);
        var sessionId = current["id"]!.GetValue<Guid>();
        var itemId = current["current_item"]!["id"]!.GetValue<Guid>();
        await AnswerAsync(app, sessionId, itemId, "żaba");

        Db.ChangeTracker.Clear();
        var enAfter = await Db.LearnerWords.AsNoTracking().SingleAsync(word => word.Id == enWord.Id);
        Assert.Equal(enWord.Status, enAfter.Status);
        Assert.Equal(enWord.DueOn, enAfter.DueOn);
        Assert.Equal(enWord.IntervalDays, enAfter.IntervalDays);
        Assert.Equal(enWord.Version, enAfter.Version);
        Assert.Equal(1, await Db.AnswerEvents.CountAsync(answer => answer.TargetLanguage == "en"));
        var plEvents = await Db.AnswerEvents.AsNoTracking().Where(answer => answer.TargetLanguage == "pl").ToArrayAsync();
        Assert.Single(plEvents);
        Assert.Equal(fixture.Entries[0].Id, plEvents[0].EntryId);
    }

    [DockerFact]
    public async Task ExhaustedLimitInOneLanguageDoesNotAffectAnother()
    {
        // DoD #2 (AC-05): вичерпаний денний ліміт нових слів у pl не впливає на видачу en того ж learner.
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, count: 2, session: false);
        Db.LearnerWords.RemoveRange(await Db.LearnerWords.ToArrayAsync());
        fixture.Learner.DailyNewWordsLimit = 1;
        // Одне слово pl показане сьогодні вичерпує pl-квоту; due лише завтра, тож не стає повторенням.
        var consumed = AddLearnerWord(fixture.Learner.Id, fixture.Entries[0].Id, "pl");
        consumed.DueOn = AnswerTestData.Day.PlusDays(1);
        await Db.SaveChangesAsync();
        await SeedEnEntryAsync("kat", 1);

        await using var app = await StartAsync();
        var pl = await StartSessionAsync(app);
        Assert.Equal(0, pl["total_items"]!.GetValue<int>());
        Assert.Contains("new_words_limit_reached", pl["notices"]!.AsArray().Select(value => value!.GetValue<string>()));

        await SwitchActiveLanguageAsync("en");
        var en = await StartSessionAsync(app);
        Assert.Equal(1, en["total_items"]!.GetValue<int>());
        Assert.Equal("new_word_card", en["current_item"]!["kind"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task UnfinishedSessionSurvivesLanguageSwitchAndCardsDoNotMigrate()
    {
        // DoD #3 (AC-04): незавершена pl-session лишається доступною після перемикання на en; картки не мігрують.
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, count: 2, session: false);
        Db.LearnerWords.RemoveRange(await Db.LearnerWords.ToArrayAsync());
        fixture.Learner.DailyNewWordsLimit = 5;
        await Db.SaveChangesAsync();
        var en = await SeedEnEntryAsync("kat", 1);

        await using var app = await StartAsync();
        var plSession = await StartSessionAsync(app);
        var plId = plSession["id"]!.GetValue<Guid>();
        Assert.Equal("new_word_card", plSession["current_item"]!["kind"]!.GetValue<string>());
        await AcknowledgeAsync(app, plId, plSession["current_item"]!["id"]!.GetValue<Guid>());

        await SwitchActiveLanguageAsync("en");
        var enSession = await StartSessionAsync(app);
        var enId = enSession["id"]!.GetValue<Guid>();
        Assert.NotEqual(plId, enId);
        Assert.Equal("new_word_card", enSession["current_item"]!["kind"]!.GetValue<string>());

        Db.ChangeTracker.Clear();
        var plRow = await Db.LearningSessions.AsNoTracking().SingleAsync(session => session.Id == plId);
        Assert.Equal("active", plRow.Status);
        Assert.Equal("pl", plRow.TargetLanguage);
        Assert.Equal("en", (await Db.LearningSessions.AsNoTracking().SingleAsync(session => session.Id == enId)).TargetLanguage);

        var plEntryIds = (await Db.DictionaryEntries.AsNoTracking().Where(entry => entry.TargetLanguage == "pl")
            .Select(entry => entry.Id).ToArrayAsync()).ToHashSet();
        Assert.All(await Db.SessionItems.AsNoTracking().Where(item => item.SessionId == plId).ToArrayAsync(),
            item => Assert.Contains(item.EntryId, plEntryIds));
        Assert.All(await Db.SessionItems.AsNoTracking().Where(item => item.SessionId == enId).ToArrayAsync(),
            item => Assert.Equal(en.Entry.Id, item.EntryId));

        // Поточна session читається за активною мовою — обидві переживають перемикання.
        Assert.Equal(enId, (await CurrentAsync(app))["id"]!.GetValue<Guid>());
        await SwitchActiveLanguageAsync("pl");
        Assert.Equal(plId, (await CurrentAsync(app))["id"]!.GetValue<Guid>());
    }

    [DockerFact]
    public async Task OtherLearnerCannotSeeProgressInAnyLanguage()
    {
        // DoD #4 (AC-10): learner 2 не бачить прогрес learner 1 у жодній мові (приховування існування → 404).
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, count: 2, session: true);
        var en = await SeedEnEntryAsync("kat", 1);
        AddLearnerWord(fixture.Learner.Id, en.Entry.Id, "en");
        var enSession = new LearningSession
        {
            LearnerId = fixture.Learner.Id, TargetLanguage = "en", LearnerDay = AnswerTestData.Day,
            EndsAt = Instant.FromUtc(2026, 10, 31, 23, 0), Status = "active", Kind = "plan",
            LastActivityAt = Clock.GetCurrentInstant(), CreatedAt = Clock.GetCurrentInstant()
        };
        var enItem = new SessionItem
        {
            SessionId = enSession.Id, EntryId = en.Entry.Id, Kind = "review", Position = 1,
            State = "pending", CreatedAt = Clock.GetCurrentInstant()
        };
        Db.LearningSessions.Add(enSession);
        Db.SessionItems.Add(enItem);
        var intruder = new Features.Identity.Data.Learner
        {
            TelegramUserId = AnswerTestData.UserId + 1, TimeZone = "Europe/Warsaw", DailyNewWordsLimit = 5,
            ActiveTargetLanguage = "pl", OnboardedAt = Clock.GetCurrentInstant(), CreatedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(intruder);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        Authenticate(app, AnswerTestData.UserId + 1);

        using (var cur = await app.Client.GetAsync("/api/v1/sessions/current"))
            Assert.Equal(HttpStatusCode.NotFound, cur.StatusCode);
        await AssertHiddenAsync(app, fixture.Session!.Id, fixture.Items[0].Id, "żaba");
        await AssertHiddenAsync(app, enSession.Id, enItem.Id, "kat");

        Db.ChangeTracker.Clear();
        Assert.Empty(await Db.LearnerWords.AsNoTracking().Where(word => word.LearnerId == intruder.Id).ToArrayAsync());
        Assert.Empty(await Db.LearningSessions.AsNoTracking().Where(session => session.LearnerId == intruder.Id).ToArrayAsync());
        Assert.Empty(await Db.AnswerEvents.AsNoTracking().ToArrayAsync());
        Assert.Equal("active", (await Db.LearningSessions.AsNoTracking().SingleAsync(session => session.Id == fixture.Session!.Id)).Status);
        Assert.Equal("active", (await Db.LearningSessions.AsNoTracking().SingleAsync(session => session.Id == enSession.Id)).Status);
    }

    [DockerFact]
    public async Task EveryProgressRowLanguageMatchesItsEntryLanguage()
    {
        // DoD #5: інваріант «мова рядка = мова entry». Продакшн-фільтри (StartSessionService, AnswerSessionService,
        // SessionLimitsService) звужують за денормалізованою колонкою target_language без join до dictionary_entries —
        // гарячий запит не робить join заради мови; тест стверджує сам інваріант для pl (живий запис) і en (засів).
        var fixture = await AnswerTestData.SeedAsync(Db, Clock, count: 2, session: false);
        Db.LearnerWords.RemoveRange(await Db.LearnerWords.ToArrayAsync());
        fixture.Learner.DailyNewWordsLimit = 5;
        await Db.SaveChangesAsync();
        var en = await SeedEnEntryAsync("kat", 1);
        AddLearnerWord(fixture.Learner.Id, en.Entry.Id, "en");
        AddAnswerEvent(fixture.Learner.Id, en.Entry.Id, en.Version.Id, "en");
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        var plSession = await StartSessionAsync(app);
        await AlreadyKnownAsync(app, plSession["id"]!.GetValue<Guid>(), plSession["current_item"]!["id"]!.GetValue<Guid>());

        Db.ChangeTracker.Clear();
        var entryLanguage = await Db.DictionaryEntries.AsNoTracking().ToDictionaryAsync(entry => entry.Id, entry => entry.TargetLanguage);
        var words = await Db.LearnerWords.AsNoTracking().ToArrayAsync();
        var events = await Db.AnswerEvents.AsNoTracking().ToArrayAsync();
        Assert.Contains(words, word => word.TargetLanguage == "pl");
        Assert.Contains(words, word => word.TargetLanguage == "en");
        Assert.Contains(events, answer => answer.TargetLanguage == "pl");
        Assert.Contains(events, answer => answer.TargetLanguage == "en");
        Assert.All(words, word => Assert.Equal(entryLanguage[word.EntryId], word.TargetLanguage));
        Assert.All(events, answer => Assert.Equal(entryLanguage[answer.EntryId], answer.TargetLanguage));
    }

    private LearnerWord AddLearnerWord(Guid learnerId, Guid entryId, string language)
    {
        var word = new LearnerWord
        {
            LearnerId = learnerId, EntryId = entryId, TargetLanguage = language, Status = "review",
            FirstShownDay = AnswerTestData.Day, DueOn = AnswerTestData.Day, IntervalDays = 2,
            ModelState = "{\"forgetting_rate\":0.3}", SchedulerVersion = "memorize-v1", CreatedAt = Clock.GetCurrentInstant()
        };
        Db.LearnerWords.Add(word);
        return word;
    }

    private void AddAnswerEvent(Guid learnerId, Guid entryId, Guid versionId, string language) =>
        Db.AnswerEvents.Add(new AnswerEvent
        {
            LearnerId = learnerId, EntryId = entryId, VersionId = versionId, TargetLanguage = language,
            ExerciseKind = "review", Outcome = "correct", AnsweredAt = Clock.GetCurrentInstant(),
            LearnerDay = AnswerTestData.Day, TimeZone = "Europe/Warsaw", SchedulerVersion = "memorize-v1",
            StateBefore = "{}", CreatedAt = Clock.GetCurrentInstant()
        });

    private async Task<PublishedEntryGraph> SeedEnEntryAsync(string lemma, int coursePosition)
    {
        var graph = ContentFixtures.NewPublishedEntry("en", lemma, coursePosition, clock: Clock);
        Db.Add(graph.Entry);
        Db.Add(graph.Version);
        Db.Add(graph.Translation);
        await Db.SaveChangesAsync();
        graph.Entry.PublishedVersionId = graph.Version.Id;
        await Db.SaveChangesAsync();
        return graph;
    }

    private async Task SwitchActiveLanguageAsync(string language)
    {
        Db.ChangeTracker.Clear();
        var learner = await Db.Learners.SingleAsync(value => value.TelegramUserId == AnswerTestData.UserId);
        learner.ActiveTargetLanguage = language;
        await Db.SaveChangesAsync();
    }

    private async Task AssertHiddenAsync(AnswerTestApplication app, Guid sessionId, Guid itemId, string answer)
    {
        using var response = await app.Client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/items/{itemId}/answer",
            new { answer, dont_know = false });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("session.item_not_found", (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    private async Task<AnswerTestApplication> StartAsync()
    {
        var app = await AnswerTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app);
        return app;
    }

    private void Authenticate(AnswerTestApplication app, long userId = AnswerTestData.UserId) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(userId, Clock.GetCurrentInstant()));

    private async Task<JsonNode> StartSessionAsync(AnswerTestApplication app)
    {
        using var response = await app.Client.PostAsync("/api/v1/sessions", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await ContractAssert.ResponseAsync("startSession", response);
        return await BodyAsync(response);
    }

    private static async Task<JsonNode> CurrentAsync(AnswerTestApplication app)
    {
        using var response = await app.Client.GetAsync("/api/v1/sessions/current");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("getCurrentSession", response);
        return await BodyAsync(response);
    }

    private static async Task AcknowledgeAsync(AnswerTestApplication app, Guid sessionId, Guid itemId)
    {
        using var response = await app.Client.PostAsync($"/api/v1/sessions/{sessionId}/items/{itemId}/acknowledge", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("acknowledgeNewWordCard", response);
    }

    private static async Task AlreadyKnownAsync(AnswerTestApplication app, Guid sessionId, Guid itemId)
    {
        using var response = await app.Client.PostAsync($"/api/v1/sessions/{sessionId}/items/{itemId}/already-known", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("markAlreadyKnown", response);
    }

    private static async Task AnswerAsync(AnswerTestApplication app, Guid sessionId, Guid itemId, string answer)
    {
        using var response = await app.Client.PostAsJsonAsync($"/api/v1/sessions/{sessionId}/items/{itemId}/answer",
            new { answer, dont_know = false });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("answerSessionItem", response);
    }

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
}
