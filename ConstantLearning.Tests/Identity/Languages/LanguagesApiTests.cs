using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Tests.Identity.Languages;

// T11 — read-API поверхні керування мовами. listTargetLanguages віддає весь каталог з обчисленим
// available (T06: увімкнена І зі схваленим контентом), listLearnerLanguages — лише мови власника-learner
// та його активну (AC-01, AC-10). Контракт multi-target-languages перевіряється ContractAssert.
public sealed class LanguagesApiTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task ListTargetLanguages_ReturnsFullCatalogOrderedBySortOrder_WithAvailabilityPerT06()
    {
        // pl має схвалений опублікований запис → available; en/de/nl вимкнені й без контенту → ні (AC-01, AC-02).
        await SeedApprovedEntryAsync("pl", "żaba", 1);

        await using var app = await StartAsync();
        Authenticate(app, 950_000_100);
        using var response = await app.Client.GetAsync("/api/v1/languages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("listTargetLanguages", response);
        var items = (await BodyAsync(response))["items"]!.AsArray();
        Assert.Equal(new[] { "pl", "en", "de", "nl" }, items.Select(item => item!["code"]!.GetValue<string>()).ToArray());
        var byCode = items.ToDictionary(item => item!["code"]!.GetValue<string>(), item => item!);
        Assert.True(byCode["pl"]["is_enabled"]!.GetValue<bool>());
        Assert.True(byCode["pl"]["available"]!.GetValue<bool>());
        Assert.False(byCode["en"]["is_enabled"]!.GetValue<bool>());
        Assert.False(byCode["en"]["available"]!.GetValue<bool>());
        Assert.False(byCode["de"]["available"]!.GetValue<bool>());
        Assert.False(byCode["nl"]["available"]!.GetValue<bool>());
    }

    [DockerFact]
    public async Task ListTargetLanguages_EnabledButWithoutApprovedContent_IsNotAvailable()
    {
        // Увімкнена, але без схваленого контенту → недоступна: available = is_enabled І наявний контент (T06).
        var pl = await Db.TargetLanguages.SingleAsync(language => language.Code == "pl");
        // Контент не засіваємо — dictionary_entries порожні після reset, тож pl лишається без схваленого слова.
        Assert.True(pl.IsEnabled);

        await using var app = await StartAsync();
        Authenticate(app, 950_000_101);
        using var response = await app.Client.GetAsync("/api/v1/languages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("listTargetLanguages", response);
        var byCode = (await BodyAsync(response))["items"]!.AsArray()
            .ToDictionary(item => item!["code"]!.GetValue<string>(), item => item!);
        Assert.False(byCode["pl"]["available"]!.GetValue<bool>());
    }

    [DockerFact]
    public async Task ListTargetLanguages_IsAccessibleBeforeOnboarding()
    {
        // Каталог доступний до завершення онбордингу — telegram-користувач без рядка learner бачить вибір (US-01).
        await SeedApprovedEntryAsync("pl", "żaba", 1);

        await using var app = await StartAsync();
        Authenticate(app, 950_000_102);
        using var response = await app.Client.GetAsync("/api/v1/languages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("listTargetLanguages", response);
    }

    [DockerFact]
    public async Task ListTargetLanguages_WithoutInitData_Returns401()
    {
        await using var app = await StartAsync();
        using var response = await app.Client.GetAsync("/api/v1/languages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await ContractAssert.ResponseAsync("listTargetLanguages", response);
        Assert.Equal("auth.invalid_init_data", (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task ListLearnerLanguages_ReturnsOwnLanguagesOrderedBySortOrder_WithActive()
    {
        // learner веде pl і en, активна — en (AC-02, AC-03).
        var studying = LearnerFixtures.NewLearnerStudying("en", clock: Clock);
        var learner = studying.Learner;
        Db.Add(learner);
        Db.Add(studying.State);
        Db.Add(new LearnerLanguageState
        {
            LearnerId = learner.Id,
            TargetLanguage = "pl",
            DailyNewWordsLimit = 5,
            CreatedAt = Instant.FromUtc(2026, 10, 20, 9, 0)
        });
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await app.Client.GetAsync("/api/v1/learners/me/languages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("listLearnerLanguages", response);
        var body = await BodyAsync(response);
        Assert.Equal("en", body["active_target_language"]!.GetValue<string>());
        var items = body["items"]!.AsArray();
        Assert.Equal(new[] { "pl", "en" }, items.Select(item => item!["target_language"]!.GetValue<string>()).ToArray());
        var byCode = items.ToDictionary(item => item!["target_language"]!.GetValue<string>(), item => item!);
        Assert.False(byCode["pl"]["is_active"]!.GetValue<bool>());
        Assert.True(byCode["en"]["is_active"]!.GetValue<bool>());
        Assert.Equal("Польська", byCode["pl"]["display_name_uk"]!.GetValue<string>());
        Assert.StartsWith("2026-10-20T09:00:00", byCode["pl"]["started_at"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task ListLearnerLanguages_EmptyWhenLearnerStartedNoLanguage()
    {
        // Онбордений learner без жодного рядка стану: порожній items, активна мова null.
        var learner = new Learner
        {
            Id = Guid.CreateVersion7(Clock.GetCurrentInstant().ToDateTimeOffset()),
            TelegramUserId = 950_000_200,
            TimeZone = "Europe/Warsaw",
            DailyNewWordsLimit = 5,
            OnboardedAt = Clock.GetCurrentInstant(),
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await app.Client.GetAsync("/api/v1/learners/me/languages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("listLearnerLanguages", response);
        var body = await BodyAsync(response);
        Assert.Null(body["active_target_language"]);
        Assert.Empty(body["items"]!.AsArray());
    }

    [DockerFact]
    public async Task ListLearnerLanguages_NotOnboarded_Returns404()
    {
        // Рядок learner існує (є learner_id claim), але онбординг не завершено → 404 learner.not_onboarded.
        var learner = new Learner
        {
            Id = Guid.CreateVersion7(Clock.GetCurrentInstant().ToDateTimeOffset()),
            TelegramUserId = 950_000_201,
            TimeZone = "Europe/Warsaw",
            DailyNewWordsLimit = 5,
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await app.Client.GetAsync("/api/v1/learners/me/languages");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ContractAssert.ResponseAsync("listLearnerLanguages", response);
        Assert.Equal("learner.not_onboarded", (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task ListLearnerLanguages_WithoutInitData_Returns401()
    {
        await using var app = await StartAsync();
        using var response = await app.Client.GetAsync("/api/v1/learners/me/languages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await ContractAssert.ResponseAsync("listLearnerLanguages", response);
        Assert.Equal("auth.invalid_init_data", (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task ListLearnerLanguages_DoesNotLeakOtherLearnersLanguages()
    {
        // AC-10: learner 2 бачить лише свої мови, не прогрес learner 1 у жодній мові.
        var first = LearnerFixtures.NewLearnerStudying("en", clock: Clock);
        Db.Add(first.Learner);
        Db.Add(first.State);
        Db.Add(new LearnerLanguageState
        {
            LearnerId = first.Learner.Id, TargetLanguage = "pl", DailyNewWordsLimit = 5, CreatedAt = Clock.GetCurrentInstant()
        });
        var second = LearnerFixtures.NewLearnerStudying("pl", clock: Clock);
        Db.Add(second.Learner);
        Db.Add(second.State);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        Authenticate(app, second.Learner.TelegramUserId);
        using var response = await app.Client.GetAsync("/api/v1/learners/me/languages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("listLearnerLanguages", response);
        var body = await BodyAsync(response);
        Assert.Equal("pl", body["active_target_language"]!.GetValue<string>());
        var items = body["items"]!.AsArray();
        Assert.Equal(new[] { "pl" }, items.Select(item => item!["target_language"]!.GetValue<string>()).ToArray());
    }

    private async Task SeedApprovedEntryAsync(string targetLanguage, string lemma, int coursePosition)
    {
        var graph = ContentFixtures.NewPublishedEntry(targetLanguage, lemma, coursePosition, clock: Clock);
        Db.Add(graph.Entry);
        Db.Add(graph.Version);
        Db.Add(graph.Translation);
        await Db.SaveChangesAsync();
        graph.Entry.PublishedVersionId = graph.Version.Id;
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    private async Task<LanguagesTestApplication> StartAsync() =>
        await LanguagesTestApplication.StartAsync(_postgres.ConnectionString, Clock);

    private void Authenticate(LanguagesTestApplication app, long telegramUserId) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(telegramUserId, Clock.GetCurrentInstant()));

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
}
