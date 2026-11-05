using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Data.Common;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ConstantLearning.Tests.Identity.Languages;

// T12 — startLearnerLanguage: ідемпотентний старт мови + активація. Перший старт створює рядок
// learner_language_states з власними дефолтами й робить мову активною (AC-01); повторний старт тієї ж
// мови нічого не змінює (AC-12); недоступна мова → 409 (AC-02); невідомий код → 404; конкурентний
// старт → 409 language.concurrent_update за xmin/PK (OQ-4). Контракт перевіряється ContractAssert.
public sealed class StartLearnerLanguageApiTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private const string LanguagesPath = "/api/v1/learners/me/languages";
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task Start_FirstTime_CreatesStateActivatesLanguage_Returns201()
    {
        // pl увімкнена й має схвалений контент → доступна; онбордений learner ще не веде жодної мови (AC-01).
        await SeedApprovedEntryAsync("pl", "żaba", 1);
        var learner = await AddOnboardedLearnerAsync(950_100_000, dailyLimit: 7);

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await PostAsync(app, """{"target_language":"pl"}""");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await ContractAssert.ResponseAsync("startLearnerLanguage", response);
        var body = await BodyAsync(response);
        Assert.Equal("pl", body["target_language"]!.GetValue<string>());
        Assert.Equal("Польська", body["display_name_uk"]!.GetValue<string>());
        Assert.True(body["is_active"]!.GetValue<bool>());
        Assert.StartsWith("2026-10-31T09:00:00", body["started_at"]!.GetValue<string>());

        var state = await Db.LearnerLanguageStates.AsNoTracking().SingleAsync(row => row.LearnerId == learner.Id);
        Assert.Equal("pl", state.TargetLanguage);
        Assert.Equal((short)7, state.DailyNewWordsLimit); // стартовий ліміт успадковано з онбордингу (OQ-1a).
        Assert.Null(state.CurrentDayDate); // свіжа доба: getToday ініціалізує «перший день» ліниво.
        Assert.Equal("pl", (await Db.Learners.AsNoTracking().SingleAsync(row => row.Id == learner.Id)).ActiveTargetLanguage);
    }

    [DockerFact]
    public async Task Start_AlreadyStudying_Returns200_WithoutSecondRow()
    {
        // learner уже веде en (активна); повторний старт не створює другого рядка й не чіпає стан (AC-12).
        var studying = LearnerFixtures.NewLearnerStudying("en", dailyLimit: 4, clock: Clock);
        Db.Add(studying.Learner);
        Db.Add(studying.State);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        Authenticate(app, studying.Learner.TelegramUserId);
        using var response = await PostAsync(app, """{"target_language":"en"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("startLearnerLanguage", response);
        var body = await BodyAsync(response);
        Assert.Equal("en", body["target_language"]!.GetValue<string>());
        Assert.True(body["is_active"]!.GetValue<bool>());

        var states = await Db.LearnerLanguageStates.AsNoTracking()
            .Where(row => row.LearnerId == studying.Learner.Id).ToListAsync();
        Assert.Single(states);
        Assert.Equal((short)4, states[0].DailyNewWordsLimit); // стан без змін.
    }

    [DockerFact]
    public async Task Start_UnknownCode_Returns404()
    {
        var learner = await AddOnboardedLearnerAsync(950_100_001);

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await PostAsync(app, """{"target_language":"zz"}""");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ContractAssert.ResponseAsync("startLearnerLanguage", response);
        Assert.Equal("language.unknown", (await BodyAsync(response))["code"]!.GetValue<string>());
        Assert.Empty(await Db.LearnerLanguageStates.AsNoTracking().Where(row => row.LearnerId == learner.Id).ToListAsync());
    }

    [DockerFact]
    public async Task Start_UnavailableLanguage_Returns409_ActiveLanguageUnchanged()
    {
        // en у seed вимкнена й без схваленого контенту → недоступна (AC-02); активна мова лишається незмінною.
        var learner = await AddOnboardedLearnerAsync(950_100_002);

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await PostAsync(app, """{"target_language":"en"}""");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await ContractAssert.ResponseAsync("startLearnerLanguage", response);
        Assert.Equal("language.not_available", (await BodyAsync(response))["code"]!.GetValue<string>());
        Assert.Empty(await Db.LearnerLanguageStates.AsNoTracking().Where(row => row.LearnerId == learner.Id).ToListAsync());
        Assert.Null((await Db.Learners.AsNoTracking().SingleAsync(row => row.Id == learner.Id)).ActiveTargetLanguage);
    }

    [DockerFact]
    public async Task Start_NotOnboarded_Returns404()
    {
        // Рядок learner існує (learner_id claim проставляється), але онбординг не завершено → learner.not_onboarded.
        var learner = new Learner
        {
            Id = Guid.CreateVersion7(Clock.GetCurrentInstant().ToDateTimeOffset()),
            TelegramUserId = 950_100_003,
            TimeZone = "Europe/Warsaw",
            DailyNewWordsLimit = 5,
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();

        await using var app = await StartAsync();
        Authenticate(app, learner.TelegramUserId);
        using var response = await PostAsync(app, """{"target_language":"pl"}""");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await ContractAssert.ResponseAsync("startLearnerLanguage", response);
        Assert.Equal("learner.not_onboarded", (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task Start_WithoutInitData_Returns401()
    {
        await using var app = await StartAsync();
        using var response = await PostAsync(app, """{"target_language":"pl"}""");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await ContractAssert.ResponseAsync("startLearnerLanguage", response);
        Assert.Equal("auth.invalid_init_data", (await BodyAsync(response))["code"]!.GetValue<string>());
    }

    [DockerFact]
    public async Task Start_ConcurrentSameLanguage_ReturnsOne201AndOne409ConcurrentUpdate()
    {
        // OQ-4: два одночасні старти тієї самої мови — один перемагає (201), другий ловить xmin/PK-конфлікт
        // і повертає 409 language.concurrent_update (клієнт повторює). У БД рівно один рядок стану.
        await SeedApprovedEntryAsync("pl", "żaba", 1);
        var learner = await AddOnboardedLearnerAsync(950_100_004);

        var barrier = new ConcurrentStateInsertBarrier();
        await using var app = await StartAsync(barrier);
        Authenticate(app, learner.TelegramUserId);
        var responses = await Task.WhenAll(
            PostAsync(app, """{"target_language":"pl"}"""),
            PostAsync(app, """{"target_language":"pl"}"""));
        try
        {
            Assert.Equal(2, barrier.WriteCount);
            Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict },
                responses.Select(response => response.StatusCode).Order());
            foreach (var response in responses)
            {
                await ContractAssert.ResponseAsync("startLearnerLanguage", response);
            }

            var conflict = responses.Single(response => response.StatusCode == HttpStatusCode.Conflict);
            var body = await BodyAsync(conflict);
            Assert.Equal("language.concurrent_update", body["code"]!.GetValue<string>());
            Assert.Single(await Db.LearnerLanguageStates.AsNoTracking().Where(row => row.LearnerId == learner.Id).ToListAsync());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private async Task<Learner> AddOnboardedLearnerAsync(long telegramUserId, short dailyLimit = 5)
    {
        var learner = new Learner
        {
            Id = Guid.CreateVersion7(Clock.GetCurrentInstant().ToDateTimeOffset()),
            TelegramUserId = telegramUserId,
            TimeZone = "Europe/Warsaw",
            DailyNewWordsLimit = dailyLimit,
            OnboardedAt = Clock.GetCurrentInstant(),
            CreatedAt = Clock.GetCurrentInstant()
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return learner;
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

    private async Task<LanguagesTestApplication> StartAsync(IInterceptor? interceptor = null) =>
        await LanguagesTestApplication.StartAsync(_postgres.ConnectionString, Clock, interceptor);

    private void Authenticate(LanguagesTestApplication app, long telegramUserId) =>
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner().Sign(telegramUserId, Clock.GetCurrentInstant()));

    private static Task<HttpResponseMessage> PostAsync(LanguagesTestApplication app, string body) =>
        app.Client.PostAsync(LanguagesPath, new StringContent(body, Encoding.UTF8, "application/json"));

    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    // Відпускає вставлення рядка стану лише коли обидва конкурентні запити вже пройшли читання стану,
    // гарантуючи справжню гонку на PK (learner_id, target_language) — як ConcurrentLearnerWriteBarrier онбордингу.
    private sealed class ConcurrentStateInsertBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _writeCount;
        public int WriteCount => _writeCount;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO learner_language_states", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _writeCount) == 2)
                {
                    _ready.SetResult();
                }

                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }

            return result;
        }
    }
}
