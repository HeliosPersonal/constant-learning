using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Content.Data;
using ConstantLearning.Features.Content.Search.Forms;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Tests.Identity.Languages;

// T13 — setActiveLanguage + scoping наявних ендпоінтів за активною мовою. Перемкнути можна лише вже
// вивчувану мову: невідомий чи невивчений код → 409 language.not_studying (existence-hiding, без 404).
// Після перемикання словник (і решта ендпоінтів) читаються у вимірі нової мови, облік старої незмінний і
// доступний назад (AC-03); незавершена session старої мови переживає перемикання тієї ж доби (AC-04);
// свіжообрана мова показує порожній початковий стан без лічильників інших мов (AC-07). Контракт — ContractAssert.
public sealed class SetActiveLanguageApiTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private const string ActiveLanguagePath = "/api/v1/learners/me/active-language";
    private const string SearchPath = "/api/v1/dictionary/search";
    private const string EntryPath = "/api/v1/dictionary/entries/";
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task SetActive_ToStudiedLanguage_Switches_Returns200()
    {
        // learner веде en (активна) і pl; перемикання на вивчувану pl оновлює active_target_language (AC-03).
        var learner = await AddStudyingLearnerAsync(950_200_000, active: "en", studied: ["en", "pl"]);

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await SetActiveAsync(app, """{"target_language":"pl"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("setActiveLanguage", response);
        Assert.Equal("pl", (await BodyAsync(response))["active_target_language"]!.GetValue<string>());
        Assert.Equal("pl", (await Db.Learners.AsNoTracking().SingleAsync(row => row.Id == learner.Id)).ActiveTargetLanguage);
    }

    [DockerFact]
    public async Task SetActive_NotStudyingLanguage_Returns409NotStudying_ActiveUnchanged()
    {
        // de learner не веде → 409 language.not_studying; активна мова лишається en (почати нову слід через start).
        var learner = await AddStudyingLearnerAsync(950_200_001, active: "en", studied: ["en"]);

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await SetActiveAsync(app, """{"target_language":"de"}""");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await ContractAssert.ResponseAsync("setActiveLanguage", response);
        Assert.Equal("language.not_studying", (await BodyAsync(response))["code"]!.GetValue<string>());
        Assert.Equal("en", (await Db.Learners.AsNoTracking().SingleAsync(row => row.Id == learner.Id)).ActiveTargetLanguage);
    }

    [DockerFact]
    public async Task SetActive_UnknownCode_Returns409NotStudying_WithoutRevealingExistence()
    {
        // Невідомий код трактуємо так само, як невивчену мову: 409 language.not_studying, без 404 (existence-hiding).
        var learner = await AddStudyingLearnerAsync(950_200_002, active: "en", studied: ["en"]);

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await SetActiveAsync(app, """{"target_language":"zz"}""");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await ContractAssert.ResponseAsync("setActiveLanguage", response);
        Assert.Equal("language.not_studying", (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task SetActive_AlreadyActiveLanguage_Returns200_NoChange()
    {
        // Повтор на вже активній мові безпечний (AC-03): 200, нічого не змінюється.
        var learner = await AddStudyingLearnerAsync(950_200_003, active: "en", studied: ["en"]);

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await SetActiveAsync(app, """{"target_language":"en"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("setActiveLanguage", response);
        Assert.Equal("en", (await BodyAsync(response))["active_target_language"]!.GetValue<string>());
        Assert.Equal("en", (await Db.Learners.AsNoTracking().SingleAsync(row => row.Id == learner.Id)).ActiveTargetLanguage);
    }

    [DockerFact]
    public async Task SetActive_NotOnboarded_Returns404()
    {
        // Рядок learner існує (claim проставляється), але онбординг не завершено → learner.not_onboarded.
        var learner = new Learner
        {
            Id = Guid.CreateVersion7(Clock.GetCurrentInstant().ToDateTimeOffset()),
            TelegramUserId = 950_200_004,
            TimeZone = "Europe/Warsaw",
            DailyNewWordsLimit = 5,
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await SetActiveAsync(app, """{"target_language":"pl"}""");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ContractAssert.ResponseAsync("setActiveLanguage", response);
        Assert.Equal("learner.not_onboarded", (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task SetActive_WithoutInitData_Returns401()
    {
        await using var app = await StartAsync();
        using var response = await SetActiveAsync(app, """{"target_language":"pl"}""");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await ContractAssert.ResponseAsync("setActiveLanguage", response);
        Assert.Equal("auth.invalid_init_data", (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task Ac03_SwitchingActiveLanguage_ScopesDictionaryToNewLanguage_OldReachableBack()
    {
        // AC-03: словник (пошук і картка) читається у вимірі активної мови; записи іншої недосяжні
        // (id чужої мови → 404, existence-hiding). Перемикання назад повертає попередню мову незмінною.
        var learner = await AddStudyingLearnerAsync(950_200_005, active: "en", studied: ["en", "pl"]);
        var en = await SeedSearchableEntryAsync("en", "frog", 1);
        var pl = await SeedSearchableEntryAsync("pl", "zaba", 1);

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);

        // Активна en: видно лише en-словник.
        Assert.Equal(HttpStatusCode.OK, await CardStatusAsync(app, en.Id));
        Assert.Equal(HttpStatusCode.NotFound, await CardStatusAsync(app, pl.Id));
        Assert.Equal(new[] { en.Id }, await SearchIdsAsync(app, "frog"));
        Assert.Empty(await SearchIdsAsync(app, "zaba"));

        // Перемикаємо на pl через ендпоінт — словник тепер у вимірі pl.
        await SwitchAsync(app, "pl");
        Assert.Equal(HttpStatusCode.OK, await CardStatusAsync(app, pl.Id));
        Assert.Equal(HttpStatusCode.NotFound, await CardStatusAsync(app, en.Id));
        Assert.Equal(new[] { pl.Id }, await SearchIdsAsync(app, "zaba"));
        Assert.Empty(await SearchIdsAsync(app, "frog"));

        // Назад на en — попередня мова доступна й незмінна (AC-03).
        await SwitchAsync(app, "en");
        Assert.Equal(HttpStatusCode.OK, await CardStatusAsync(app, en.Id));
        Assert.Equal(new[] { en.Id }, await SearchIdsAsync(app, "frog"));
    }

    [DockerFact]
    public async Task Ac03Ac04Ac07_EndpointSwitch_ScopesSessions_PreservesUnfinished_FreshLanguageEmpty()
    {
        // AC-03/AC-04/AC-07: перемикання через ендпоінт скоупить заняття за активною мовою. Незавершена
        // pl-session переживає перемикання тієї ж доби й доступна назад (AC-04); свіжообрана en показує лише
        // свій порожній старт — один свій новий card, без лічильників pl (AC-07).
        var learner = await AddStudyingLearnerAsync(950_200_006, active: "pl", studied: ["en", "pl"]);
        await SeedNewWordEntryAsync("pl", "zaba", 1);
        await SeedNewWordEntryAsync("pl", "kot", 2);
        await SeedNewWordEntryAsync("en", "cat", 1);

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);

        var plSession = await StartSessionAsync(app);
        var plId = plSession["id"]!.GetValue<Guid>();
        Assert.Equal("new_word_card", plSession["current_item"]!["kind"]!.GetValue<string>());
        await AcknowledgeAsync(app, plId, plSession["current_item"]!["id"]!.GetValue<Guid>());

        await SwitchAsync(app, "en");
        var enSession = await StartSessionAsync(app);
        var enId = enSession["id"]!.GetValue<Guid>();
        Assert.NotEqual(plId, enId);
        Assert.Equal(1, enSession["total_items"]!.GetValue<int>()); // лише єдине нове en-слово (AC-07).
        Assert.Equal("new_word_card", enSession["current_item"]!["kind"]!.GetValue<string>());
        Assert.Equal(enId, (await CurrentAsync(app))["id"]!.GetValue<Guid>());

        // Назад на pl — незавершена pl-session тієї ж доби лишається активною й доступною (AC-04).
        await SwitchAsync(app, "pl");
        Assert.Equal(plId, (await CurrentAsync(app))["id"]!.GetValue<Guid>());

        Db.ChangeTracker.Clear();
        Assert.Equal("active", (await Db.LearningSessions.AsNoTracking().SingleAsync(row => row.Id == plId)).Status);
        Assert.Equal("pl", (await Db.LearningSessions.AsNoTracking().SingleAsync(row => row.Id == plId)).TargetLanguage);
        Assert.Equal("en", (await Db.LearningSessions.AsNoTracking().SingleAsync(row => row.Id == enId)).TargetLanguage);
    }

    private async Task<Learner> AddStudyingLearnerAsync(long telegramUserId, string active, string[] studied,
        short limit = 5)
    {
        var now = Clock.GetCurrentInstant();
        var learner = new Learner
        {
            Id = Guid.CreateVersion7(now.ToDateTimeOffset()),
            TelegramUserId = telegramUserId,
            FirstName = "Test User",
            TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = true,
            DailyNewWordsLimit = limit,
            ActiveTargetLanguage = active,
            OnboardedAt = now,
            CreatedAt = now
        };
        Db.Learners.Add(learner);
        foreach (var code in studied)
        {
            Db.Add(new LearnerLanguageState
            {
                LearnerId = learner.Id,
                TargetLanguage = code,
                DailyNewWordsLimit = limit,
                CreatedAt = now
            });
        }

        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return learner;
    }

    private async Task<DictionaryEntry> SeedSearchableEntryAsync(string targetLanguage, string lemma, int coursePosition)
    {
        var entry = await SeedNewWordEntryAsync(targetLanguage, lemma, coursePosition);
        Db.SearchForms.Add(new SearchForm
        {
            EntryId = entry.Id,
            VersionId = (await Db.DictionaryEntries.AsNoTracking().SingleAsync(row => row.Id == entry.Id)).PublishedVersionId!.Value,
            Lang = targetLanguage,
            FormDisplay = lemma,
            FormFolded = SearchTextNormalizer.Fold(lemma),
            IsLemma = true,
            CreatedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return entry;
    }

    private async Task<DictionaryEntry> SeedNewWordEntryAsync(string targetLanguage, string lemma, int coursePosition)
    {
        var graph = ContentFixtures.NewPublishedEntry(targetLanguage, lemma, coursePosition, clock: Clock);
        Db.Add(graph.Entry);
        Db.Add(graph.Version);
        Db.Add(graph.Translation);
        await Db.SaveChangesAsync();
        graph.Entry.PublishedVersionId = graph.Version.Id;
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return graph.Entry;
    }

    private async Task<LanguagesTestApplication> StartAsync() =>
        await LanguagesTestApplication.StartAsync(_postgres.ConnectionString, Clock);

    private void Authenticate(LanguagesTestApplication app, long telegramUserId) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(telegramUserId, Clock.GetCurrentInstant()));

    private static Task<HttpResponseMessage> SetActiveAsync(LanguagesTestApplication app, string body) =>
        app.Client.PutAsync(ActiveLanguagePath, new StringContent(body, Encoding.UTF8, "application/json"));

    private static async Task SwitchAsync(LanguagesTestApplication app, string targetLanguage)
    {
        using var response = await SetActiveAsync(app, $$"""{"target_language":"{{targetLanguage}}"}""");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("setActiveLanguage", response);
    }

    private async Task<HttpStatusCode> CardStatusAsync(LanguagesTestApplication app, Guid entryId)
    {
        using var response = await app.Client.GetAsync(EntryPath + entryId);
        await ContractAssert.ResponseAsync("getDictionaryEntry", response);
        return response.StatusCode;
    }

    private async Task<Guid[]> SearchIdsAsync(LanguagesTestApplication app, string query)
    {
        using var response = await app.Client.GetAsync(SearchPath + "?q=" + Uri.EscapeDataString(query));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("searchDictionary", response);
        return (await BodyAsync(response))["items"]!.AsArray().Select(item => item!["entry_id"]!.GetValue<Guid>()).ToArray();
    }

    private async Task<JsonNode> StartSessionAsync(LanguagesTestApplication app)
    {
        using var response = await app.Client.PostAsync("/api/v1/sessions", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await ContractAssert.ResponseAsync("startSession", response);
        return await BodyAsync(response);
    }

    private static async Task<JsonNode> CurrentAsync(LanguagesTestApplication app)
    {
        using var response = await app.Client.GetAsync("/api/v1/sessions/current");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("getCurrentSession", response);
        return await BodyAsync(response);
    }

    private static async Task AcknowledgeAsync(LanguagesTestApplication app, Guid sessionId, Guid itemId)
    {
        using var response = await app.Client.PostAsync($"/api/v1/sessions/{sessionId}/items/{itemId}/acknowledge", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("acknowledgeNewWordCard", response);
    }

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
}
