using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Features.Identity.Settings;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

namespace ConstantLearning.Tests.Identity.Settings;

public sealed class LearnerSettingsTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const string Path = "/api/v1/learners/me/settings";
    private const long TelegramUserId = 900000001;

    [DockerFact]
    public async Task Ac44_ChangesZoneImmediately_DefersLimit_AndPreservesExistingSessionAndIssuanceBoundary()
    {
        var learner = await SeedAsync();
        var previousBoundary = Instant.FromUtc(2026, 1, 15, 23, 0);
        learner.NewWordsAvailableFrom = previousBoundary;
        Db.LearningSessions.Add(new LearningSession
        {
            LearnerId = learner.Id, LearnerDay = new LocalDate(2026, 1, 15), EndsAt = previousBoundary,
            Status = "active", CreatedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();

        using var response = await PatchAsync(app, """{"daily_new_words_limit":10,"time_zone":"Asia/Tokyo"}""");

        await AssertAsync(response, HttpStatusCode.OK);
        var body = await BodyAsync(response);
        Assert.Equal("Asia/Tokyo", body["time_zone"]!.GetValue<string>());
        Assert.True(body["time_zone_confirmed"]!.GetValue<bool>());
        Assert.Equal(5, body["daily_new_words_limit"]!.GetValue<int>());
        Assert.Equal(10, body["pending_daily_new_words_limit"]!.GetValue<int>());
        Assert.Equal("19:00", body["reminder"]!["local_time"]!.GetValue<string>());
        var stored = await StoredAsync();
        Assert.Equal(new LocalDate(2026, 1, 15), stored.CurrentDayDate);
        Assert.Equal("Europe/Warsaw", stored.CurrentDayTimeZoneAtStart);
        Assert.Equal(previousBoundary, stored.CurrentDayEndsAt);
        Assert.Equal(previousBoundary, stored.PendingLimitEffectiveFrom);
        Assert.Equal(previousBoundary, stored.NewWordsAvailableFrom);
        Assert.Equal(previousBoundary, (await Db.LearningSessions.AsNoTracking().SingleAsync()).EndsAt);
    }

    [DockerFact]
    public async Task Patch_OmittedReminderIsPreserved_NullDeletes_AndExplicitScheduleRecreates()
    {
        await SeedAsync();
        await using var app = await StartAsync();
        using var omitted = await PatchAsync(app, """{"daily_new_words_limit":3}""");
        await AssertAsync(omitted, HttpStatusCode.OK);
        Assert.NotNull((await BodyAsync(omitted))["reminder"]);
        using var removed = await PatchAsync(app, """{"reminder":null}""");
        await AssertAsync(removed, HttpStatusCode.OK);
        Assert.Null((await BodyAsync(removed))["reminder"]);
        Assert.Empty(await Db.ReminderSchedules.AsNoTracking().ToListAsync());
        using var recreated = await PatchAsync(app, """{"reminder":{"local_time":"08:30","days_of_week":["sun","tue"]}}""");
        await AssertAsync(recreated, HttpStatusCode.OK);
        var schedule = await Db.ReminderSchedules.AsNoTracking().SingleAsync();
        Assert.Equal(new LocalTime(8, 30), schedule.LocalTime);
        Assert.Equal(2 + 64, schedule.DaysOfWeek);
        var stored = await StoredAsync();
        Assert.Equal((short?)3, stored.PendingDailyNewWordsLimit);
        Assert.Equal(5, stored.DailyNewWordsLimit);
    }

    [DockerFact]
    public async Task Ac27_ReminderRequiresConfirmedZone_AndPatchCanConfirmZoneAtomically()
    {
        await SeedAsync(confirmed: false, reminder: false);
        await using var app = await StartAsync();
        using var rejected = await PatchAsync(app, """{"reminder":{"local_time":"19:00","days_of_week":["mon"]}}""");
        await AssertAsync(rejected, HttpStatusCode.BadRequest, "settings.time_zone_required");
        Assert.Null((await StoredAsync()).CurrentDayDate);
        Assert.Empty(await Db.ReminderSchedules.AsNoTracking().ToListAsync());

        using var accepted = await PatchAsync(app,
            """{"time_zone":"Europe/Warsaw","reminder":{"local_time":"19:00","days_of_week":["mon"]}}""");
        await AssertAsync(accepted, HttpStatusCode.OK);
        Assert.True((await StoredAsync()).TimeZoneConfirmed);
        Assert.Single(await Db.ReminderSchedules.AsNoTracking().ToListAsync());
    }

    [DockerFact]
    public async Task Ac45_StrictInvalidPatches_ReturnContractErrors_AndDoNotChangeAnyPersistedState()
    {
        var original = await SeedAsync();
        await using var app = await StartAsync();
        (string Json, string Code)[] invalid =
        [
            ("{}", "settings.invalid_request"), ("null", "settings.invalid_request"), ("{", "settings.invalid_request"),
            ("""{"unknown":5}""", "settings.invalid_request"),
            ("""{"daily_new_words_limit":5,"daily_new_words_limit":3}""", "settings.invalid_request"),
            ("""{"daily_new_words_limit":4}""", "settings.invalid_limit"),
            ("""{"daily_new_words_limit":null}""", "settings.invalid_limit"),
            ("""{"daily_new_words_limit":"5"}""", "settings.invalid_limit"),
            ("""{"daily_new_words_limit":3.5}""", "settings.invalid_limit"),
            ("""{"time_zone":null}""", "settings.invalid_time_zone"),
            ("""{"time_zone":"Unknown/Zone"}""", "settings.invalid_time_zone"),
            ("""{"time_zone":"Central European Standard Time"}""", "settings.invalid_time_zone"),
            ("""{"time_zone":42}""", "settings.invalid_time_zone"),
            ("""{"reminder":{"local_time":"19:00","days_of_week":[]}}""", "settings.reminder_days_required"),
            ("""{"reminder":{"local_time":"24:00","days_of_week":["mon"]}}""", "settings.invalid_reminder"),
            ("""{"reminder":{"local_time":"19:00","days_of_week":["mon","mon"]}}""", "settings.invalid_reminder"),
            ("""{"reminder":{"local_time":"19:00","days_of_week":["monday"]}}""", "settings.invalid_reminder")
        ];
        foreach (var (json, code) in invalid)
        {
            using var response = await PatchAsync(app, json);
            await AssertAsync(response, HttpStatusCode.BadRequest, code);
            var stored = await StoredAsync();
            Assert.Equal(original.Version, stored.Version);
            Assert.Null(stored.CurrentDayDate);
            Assert.Equal("Europe/Warsaw", stored.TimeZone);
            Assert.Equal(5, stored.DailyNewWordsLimit);
            Assert.Null(stored.PendingDailyNewWordsLimit);
            Assert.Equal(19, (await Db.ReminderSchedules.AsNoTracking().SingleAsync()).LocalTime.Hour);
        }
    }

    [DockerFact]
    public async Task RepeatedChangesBeforeMidnight_RecalculatePendingBoundaryAcrossApplicationRestarts()
    {
        await SeedAsync();
        await using (var first = await StartAsync())
        {
            using var response = await PatchAsync(first, """{"daily_new_words_limit":10,"time_zone":"America/New_York"}""");
            await AssertAsync(response, HttpStatusCode.OK);
            Assert.Equal(Instant.FromUtc(2026, 1, 16, 5, 0), (await StoredAsync()).PendingLimitEffectiveFrom);
        }

        Clock.Advance(Duration.FromHours(1));
        await using var restarted = await StartAsync();
        using var changed = await PatchAsync(restarted, """{"time_zone":"Asia/Tokyo"}""");
        await AssertAsync(changed, HttpStatusCode.OK);
        var stored = await StoredAsync();
        Assert.Equal(Instant.FromUtc(2026, 1, 15, 23, 0), stored.PendingLimitEffectiveFrom);
        Assert.Equal(stored.CurrentDayOriginalEndsAt, stored.CurrentDayEndsAt);
        Assert.Equal("Europe/Warsaw", stored.CurrentDayTimeZoneAtStart);
        Assert.Equal((short?)10, stored.PendingDailyNewWordsLimit);
        Assert.Equal(5, stored.DailyNewWordsLimit);
    }

    [DockerFact]
    public async Task ChangeDuringExtension_PreservesAlreadyStartedDay_AndMaturesLimitAtItsActualEnd()
    {
        await SeedAsync();
        await using (var first = await StartAsync())
        {
            using var response = await PatchAsync(first, """{"daily_new_words_limit":10,"time_zone":"America/New_York"}""");
            await AssertAsync(response, HttpStatusCode.OK);
        }

        Clock.Reset(Instant.FromUtc(2026, 1, 16, 0, 0));
        await using var app = await StartAsync();
        using var changed = await PatchAsync(app, """{"time_zone":"Asia/Tokyo"}""");
        await AssertAsync(changed, HttpStatusCode.OK);
        var stored = await StoredAsync();
        Assert.Equal(new LocalDate(2026, 1, 15), stored.CurrentDayDate);
        Assert.Equal(Instant.FromUtc(2026, 1, 16, 5, 0), stored.CurrentDayEndsAt);
        Assert.Equal(stored.CurrentDayEndsAt, stored.PendingLimitEffectiveFrom);

        Clock.Reset(stored.CurrentDayEndsAt!.Value - Duration.FromSeconds(1));
        using var before = await GetProfileAsync(app);
        Assert.Equal(5, (await BodyAsync(before))["daily_new_words_limit"]!.GetValue<int>());
        Clock.Advance(Duration.FromSeconds(1));
        using var after = await GetProfileAsync(app);
        Assert.Equal(10, (await BodyAsync(after))["daily_new_words_limit"]!.GetValue<int>());
        Assert.Null((await BodyAsync(after))["pending_daily_new_words_limit"]);
        stored = await StoredAsync();
        Assert.Equal(new LocalDate(2026, 1, 16), stored.CurrentDayDate);
        Assert.Equal(Instant.FromUtc(2026, 1, 16, 5, 0), stored.CurrentDayStartsAt);
        Assert.Equal(Instant.FromUtc(2026, 1, 16, 15, 0), stored.CurrentDayEndsAt);
        Assert.Null(stored.PendingLimitEffectiveFrom);
    }

    [DockerFact]
    public async Task ExplicitCurrentLimit_CancelsPendingChange_AndAcceptsAllThreeAllowedValues()
    {
        await SeedAsync();
        await using var app = await StartAsync();
        foreach (var limit in new[] { 3, 10, 5 })
        {
            using var response = await PatchAsync(app, $$"""{"daily_new_words_limit":{{limit}}}""");
            await AssertAsync(response, HttpStatusCode.OK);
            Assert.Equal(5, (await StoredAsync()).DailyNewWordsLimit);
            Assert.Equal(limit == 5 ? null : (short?)limit, (await StoredAsync()).PendingDailyNewWordsLimit);
        }

        Assert.Null((await StoredAsync()).PendingLimitEffectiveFrom);
    }

    [DockerFact]
    public async Task RepeatedOnboardingAfterBoundary_AppliesScheduledLimit_AndIgnoresInvalidOrChangedRequestBody()
    {
        await SeedAsync();
        await using var app = await StartAsync();
        using var scheduled = await PatchAsync(app, """{"daily_new_words_limit":10,"time_zone":"Asia/Tokyo"}""");
        await AssertAsync(scheduled, HttpStatusCode.OK);
        Clock.Reset((await StoredAsync()).PendingLimitEffectiveFrom!.Value);
        uint? versionAfterMaturity = null;
        string[] bodies = ["{", """{"daily_new_words_limit":3,"time_zone":"America/New_York","reminder":null}"""];
        foreach (var body in bodies)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/learners/me/onboarding") { Content = Json(body) };
            request.Headers.Authorization = new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(TelegramUserId, Clock.GetCurrentInstant()));
            using var response = await app.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await ContractAssert.ResponseAsync("completeOnboarding", response);
            var actual = await BodyAsync(response);
            Assert.Equal(10, actual["daily_new_words_limit"]!.GetValue<int>());
            Assert.Null(actual["pending_daily_new_words_limit"]);
            Assert.Equal("Asia/Tokyo", actual["time_zone"]!.GetValue<string>());
            Assert.NotNull(actual["reminder"]);
            var stored = await StoredAsync();
            if (versionAfterMaturity is { } version)
            {
                Assert.Equal(version, stored.Version);
            }

            versionAfterMaturity = stored.Version;
        }

        Assert.Null((await StoredAsync()).PendingLimitEffectiveFrom);
    }

    [DockerFact]
    public async Task ConcurrentPatches_SerializeOnLearnerRow_WithoutLostUnrelatedFields()
    {
        await SeedAsync(reminder: false);
        await using var app = await StartAsync();
        var responses = await Task.WhenAll(
            PatchAsync(app, """{"daily_new_words_limit":10}"""),
            PatchAsync(app, """{"time_zone":"America/New_York"}"""),
            PatchAsync(app, """{"reminder":{"local_time":"08:30","days_of_week":["thu"]}}"""));
        try
        {
            foreach (var response in responses)
            {
                await AssertAsync(response, HttpStatusCode.OK);
            }

            var stored = await StoredAsync();
            Assert.Equal("America/New_York", stored.TimeZone);
            Assert.Equal((short?)10, stored.PendingDailyNewWordsLimit);
            Assert.Equal(Instant.FromUtc(2026, 1, 16, 5, 0), stored.PendingLimitEffectiveFrom);
            Assert.Equal(8, (await Db.ReminderSchedules.AsNoTracking().SingleAsync()).LocalTime.Hour);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [DockerFact]
    public async Task SettingsChangedHook_SeesSavedSettingsAndCommitsOwnChangesInSameTransaction()
    {
        await SeedAsync();
        var changes = new ConcurrentQueue<LearnerSettingsChanged>();
        await using var app = await StartAsync(services => services.AddScoped<ILearnerSettingsChangedHandler>(provider =>
            new ProbeHandler(provider.GetRequiredService<AppDbContext>(), changes, fail: false)));
        using var response = await PatchAsync(app, """{"time_zone":"Asia/Tokyo"}""");
        await AssertAsync(response, HttpStatusCode.OK);
        var change = Assert.Single(changes);
        Assert.Equal((await StoredAsync()).Id, change.LearnerId);
        Assert.Equal(Clock.GetCurrentInstant(), change.ChangedAt);
        Assert.Equal("test-hook", (await StoredAsync()).ReporterAlias);
    }

    [DockerFact]
    public async Task SettingsChangedHookFailure_RollsBackSettingsSnapshotAndHandlerWrites()
    {
        var original = await SeedAsync();
        var changes = new ConcurrentQueue<LearnerSettingsChanged>();
        await using var app = await StartAsync(services => services.AddScoped<ILearnerSettingsChangedHandler>(provider =>
            new ProbeHandler(provider.GetRequiredService<AppDbContext>(), changes, fail: true)));
        await Assert.ThrowsAsync<HookFailureException>(() => PatchAsync(app, """{"time_zone":"Asia/Tokyo","daily_new_words_limit":10}"""));
        var stored = await StoredAsync();
        Assert.Equal(original.Version, stored.Version);
        Assert.Equal("Europe/Warsaw", stored.TimeZone);
        Assert.Null(stored.CurrentDayDate);
        Assert.Null(stored.PendingDailyNewWordsLimit);
        Assert.Null(stored.ReporterAlias);
    }

    [DockerFact]
    public async Task Endpoint_RequiresRealAuthenticationAndCompletedOnboarding_AndIgnoresQueryIdentity()
    {
        var learner = await SeedAsync();
        await using var app = await StartAsync();
        using var anonymous = await app.Client.PatchAsync(Path, Json("""{"reminder":null}"""));
        await AssertAsync(anonymous, HttpStatusCode.Unauthorized, "auth.invalid_init_data");
        using var forgedRequest = new HttpRequestMessage(HttpMethod.Patch, Path) { Content = Json("""{"reminder":null}""") };
        forgedRequest.Headers.Authorization = new AuthenticationHeaderValue("tma",
            new TelegramInitDataSigner("wrong-bot").Sign(TelegramUserId, Clock.GetCurrentInstant()));
        using var forged = await app.Client.SendAsync(forgedRequest);
        await AssertAsync(forged, HttpStatusCode.Unauthorized, "auth.invalid_init_data");
        using var foreign = await PatchAsync(app, """{"reminder":null}""", TelegramUserId + 1,
            $"{Path}?learner_id={learner.Id}&telegram_user_id={TelegramUserId}");
        await AssertAsync(foreign, HttpStatusCode.NotFound, "learner.not_onboarded");
        learner.OnboardedAt = null;
        await Db.SaveChangesAsync();
        using var incomplete = await PatchAsync(app, """{"reminder":null}""");
        await AssertAsync(incomplete, HttpStatusCode.NotFound, "learner.not_onboarded");
        Assert.Single(await Db.ReminderSchedules.AsNoTracking().ToListAsync());
    }

    private async Task<Learner> SeedAsync(bool confirmed = true, bool reminder = true)
    {
        Clock.Reset(Instant.FromUtc(2026, 1, 15, 11, 0));
        var learner = new Learner
        {
            TelegramUserId = TelegramUserId, FirstName = "Test User", TimeZone = "Europe/Warsaw",
            TimeZoneConfirmed = confirmed, DailyNewWordsLimit = 5, CanMessage = true,
            OnboardedAt = Clock.GetCurrentInstant(), CreatedAt = Clock.GetCurrentInstant()
        };
        if (reminder)
        {
            learner.ReminderSchedule = new ReminderSchedule { LocalTime = new LocalTime(19, 0), DaysOfWeek = 1 };
        }

        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        return learner;
    }

    private Task<Learner> StoredAsync() => Db.Learners.AsNoTracking().SingleAsync();
    private Task<SettingsTestApplication> StartAsync(Action<IServiceCollection>? configure = null) =>
        SettingsTestApplication.StartAsync(_postgres.ConnectionString, Clock, configure);

    private Task<HttpResponseMessage> PatchAsync(SettingsTestApplication app, string json, long telegramUserId = TelegramUserId, string path = Path)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, path) { Content = Json(json) };
        request.Headers.Authorization = new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(telegramUserId, Clock.GetCurrentInstant()));
        return SendAsync(app.Client, request);
    }

    private async Task<HttpResponseMessage> GetProfileAsync(SettingsTestApplication app)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/learners/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(TelegramUserId, Clock.GetCurrentInstant()));
        var response = await app.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("getCurrentLearner", response);
        return response;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request)
    {
        using (request)
        {
            return await client.SendAsync(request);
        }
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
    private static async Task<JsonNode> BodyAsync(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private static async Task AssertAsync(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        await ContractAssert.ResponseAsync("updateLearnerSettings", response);
        if (code is not null)
        {
            Assert.Equal(code, (await BodyAsync(response))["code"]!.GetValue<string>());
        }
    }

    private sealed class HookFailureException : Exception { }

    private sealed class ProbeHandler(AppDbContext db, ConcurrentQueue<LearnerSettingsChanged> changes, bool fail) : ILearnerSettingsChangedHandler
    {
        public async Task HandleAsync(LearnerSettingsChanged change, CancellationToken cancellationToken)
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            var learner = await db.Learners.AsNoTracking().SingleAsync(value => value.Id == change.LearnerId, cancellationToken);
            Assert.Equal("Asia/Tokyo", learner.TimeZone);
            changes.Enqueue(change);
            (await db.Learners.SingleAsync(value => value.Id == change.LearnerId, cancellationToken)).ReporterAlias = "test-hook";
            if (fail)
            {
                await db.SaveChangesAsync(cancellationToken);
                throw new HookFailureException();
            }
        }
    }
}
