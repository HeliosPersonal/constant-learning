using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Identity.Languages;
using ConstantLearning.Tests.Infrastructure;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;

namespace ConstantLearning.Tests.Identity.Settings;

// ADR-0005: ліміт нових слів і нагадування належать активній мові. Перемикання мови показує налаштування
// тієї мови, а зміна однієї мови не чіпає іншу. Контракт — ContractAssert.
public sealed class ReminderPerLanguageApiTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private const string SettingsPath = "/api/v1/learners/me/settings";
    private const string ProfilePath = "/api/v1/learners/me";
    private const string ActiveLanguagePath = "/api/v1/learners/me/active-language";
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task ReminderAndLimit_FollowActiveLanguage_AcrossSwitches()
    {
        var learner = await AddStudyingLearnerAsync(950_300_000, active: "en", studied: ["en", "pl"]);

        await using var app = await LanguagesTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app, learner.TelegramUserId);

        // en: ліміт 3 (відкладено на наступну добу) і нагадування о 08:00 щопонеділка.
        using var enPatch = await PatchSettingsAsync(app,
            """{"daily_new_words_limit":3,"reminder":{"local_time":"08:00","days_of_week":["mon"]}}""");
        Assert.Equal(HttpStatusCode.OK, enPatch.StatusCode);
        await ContractAssert.ResponseAsync("updateLearnerSettings", enPatch);
        var en = await BodyAsync(enPatch);
        en["active_target_language"]!.GetValue<string>().Should().Be("en");
        en["daily_new_words_limit"]!.GetValue<int>().Should().Be(5);
        en["pending_daily_new_words_limit"]!.GetValue<int>().Should().Be(3);
        en["reminder"]!["local_time"]!.GetValue<string>().Should().Be("08:00");

        // pl ще без нагадування й з лімітом з онбордингу: налаштування en не просочуються.
        await SwitchAsync(app, "pl");
        var pl = await ReadLearnerAsync(app);
        pl["active_target_language"]!.GetValue<string>().Should().Be("pl");
        pl["daily_new_words_limit"]!.GetValue<int>().Should().Be(5);
        pl["pending_daily_new_words_limit"].Should().BeNull();
        pl["reminder"].Should().BeNull();

        using var plPatch = await PatchSettingsAsync(app, """{"reminder":{"local_time":"19:30","days_of_week":["tue"]}}""");
        Assert.Equal(HttpStatusCode.OK, plPatch.StatusCode);
        await ContractAssert.ResponseAsync("updateLearnerSettings", plPatch);
        (await BodyAsync(plPatch))["reminder"]!["local_time"]!.GetValue<string>().Should().Be("19:30");

        // Повернення на en відновлює її розклад і відкладений ліміт.
        await SwitchAsync(app, "en");
        var back = await ReadLearnerAsync(app);
        back["reminder"]!["local_time"]!.GetValue<string>().Should().Be("08:00");
        back["reminder"]!["days_of_week"]!.AsArray().Select(day => day!.GetValue<string>()).Should().Equal("mon");
        back["pending_daily_new_words_limit"]!.GetValue<int>().Should().Be(3);

        var rows = await Db.ReminderSchedules.AsNoTracking().Where(row => row.LearnerId == learner.Id)
            .OrderBy(row => row.TargetLanguage).ToArrayAsync();
        rows.Select(row => row.TargetLanguage).Should().Equal("en", "pl");
    }

    [DockerFact]
    public async Task RemovingReminder_InOneLanguage_LeavesOtherLanguageReminder()
    {
        var learner = await AddStudyingLearnerAsync(950_300_001, active: "pl", studied: ["pl", "en"]);
        Db.ReminderSchedules.AddRange(
            new ReminderSchedule { LearnerId = learner.Id, TargetLanguage = "pl", LocalTime = new NodaTime.LocalTime(19, 0),
                DaysOfWeek = 127, CreatedAt = Clock.GetCurrentInstant() },
            new ReminderSchedule { LearnerId = learner.Id, TargetLanguage = "en", LocalTime = new NodaTime.LocalTime(8, 0),
                DaysOfWeek = 127, CreatedAt = Clock.GetCurrentInstant() });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();

        await using var app = await LanguagesTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        Authenticate(app, learner.TelegramUserId);
        using var response = await PatchSettingsAsync(app, """{"reminder":null}""");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("updateLearnerSettings", response);
        (await BodyAsync(response))["reminder"].Should().BeNull();

        var remaining = await Db.ReminderSchedules.AsNoTracking().SingleAsync();
        remaining.TargetLanguage.Should().Be("en");
        remaining.LocalTime.Should().Be(new NodaTime.LocalTime(8, 0));
    }

    private async Task<Learner> AddStudyingLearnerAsync(long telegramUserId, string active, string[] studied)
    {
        var now = Clock.GetCurrentInstant();
        var learner = new Learner
        {
            Id = Guid.CreateVersion7(now.ToDateTimeOffset()),
            TelegramUserId = telegramUserId,
            FirstName = "Test User",
            TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = true,
            DailyNewWordsLimit = 5,
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
                DailyNewWordsLimit = 5,
                CreatedAt = now
            });
        }

        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return learner;
    }

    private void Authenticate(LanguagesTestApplication app, long telegramUserId) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(telegramUserId, Clock.GetCurrentInstant()));

    private static Task<HttpResponseMessage> PatchSettingsAsync(LanguagesTestApplication app, string body) =>
        app.Client.PatchAsync(SettingsPath, new StringContent(body, Encoding.UTF8, "application/json"));

    private static async Task SwitchAsync(LanguagesTestApplication app, string targetLanguage)
    {
        using var response = await app.Client.PutAsync(ActiveLanguagePath,
            new StringContent($$"""{"target_language":"{{targetLanguage}}"}""", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("setActiveLanguage", response);
    }

    private static async Task<JsonNode> ReadLearnerAsync(LanguagesTestApplication app)
    {
        using var response = await app.Client.GetAsync(ProfilePath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("getCurrentLearner", response);
        return await BodyAsync(response);
    }

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
}
