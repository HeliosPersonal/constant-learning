using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NodaTime;

namespace ConstantLearning.Tests.Identity.Onboarding;

public sealed class OnboardingTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;
    private const string OnboardingPath = "/api/v1/learners/me/onboarding";
    private const string ProfilePath = "/api/v1/learners/me";
    private const long TelegramUserId = 900000001;

    [DockerFact]
    public async Task FirstOnboarding_Returns201MatchingExample_AndPersistsAuthenticatedIdentity()
    {
        await using var app = await StartAsync();
        using var response = await PostAsync(app.Client,
            """{"daily_new_words_limit":5,"time_zone":"Europe/Warsaw","reminder":null}""");
        await AssertResponseAsync(response, HttpStatusCode.Created, "completeOnboarding");
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse($$"""
            {"id":"{{json["id"]}}","first_name":"Test User","time_zone":"Europe/Warsaw",
             "time_zone_confirmed":true,"daily_new_words_limit":5,"pending_daily_new_words_limit":null,
             "reminder":null,"onboarded_at":"2026-10-31T09:00:00Z"}
            """), json));

        var learner = await Db.Learners.AsNoTracking().SingleAsync();
        Assert.Equal(TelegramUserId, learner.TelegramUserId);
        Assert.Equal("Test User", learner.FirstName);
        Assert.True(learner.CanMessage);
        Assert.Equal('7', learner.Id.ToString()[14]);
        Assert.Equal(Clock.GetCurrentInstant(), learner.CreatedAt);
        Assert.Equal(Clock.GetCurrentInstant(), learner.OnboardedAt);
        Assert.Empty(await Db.ReminderSchedules.ToListAsync());

        using var profile = await app.Client.GetAsync(ProfilePath);
        await AssertResponseAsync(profile, HttpStatusCode.OK, "getCurrentLearner");
        Assert.Equal(await response.Content.ReadAsStringAsync(), await profile.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task MissingNullAndUnknownTimeZone_UseUnconfirmedWarsaw_AndAcceptAllLimits()
    {
        await using var app = await StartAsync();
        var zones = new[] { "", ",\"time_zone\":null", ",\"time_zone\":\"Unknown/Zone\"" };
        var limits = new[] { 3, 5, 10 };
        for (var index = 0; index < zones.Length; index++)
        {
            Authorize(app.Client, TelegramUserId + index);
            using var response = await PostAsync(app.Client, $$"""{"daily_new_words_limit":{{limits[index]}}{{zones[index]}}} """);
            await AssertResponseAsync(response, HttpStatusCode.Created, "completeOnboarding");
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Europe/Warsaw", body.RootElement.GetProperty("time_zone").GetString());
            Assert.False(body.RootElement.GetProperty("time_zone_confirmed").GetBoolean());
            Assert.Equal(limits[index], body.RootElement.GetProperty("daily_new_words_limit").GetInt32());
        }

        Assert.Equal(3, await Db.Learners.CountAsync());
    }

    [DockerFact]
    public async Task Reminder_RoundTripsMondayToSundayBitMask_WithConfirmedIanaZone()
    {
        await using var app = await StartAsync();
        using var response = await PostAsync(app.Client,
            """{"daily_new_words_limit":10,"time_zone":"Europe/Kyiv","reminder":{"local_time":"19:00","days_of_week":["sun","mon","wed","fri"]}}""");
        await AssertResponseAsync(response, HttpStatusCode.Created, "completeOnboarding");
        var reminder = await Db.ReminderSchedules.AsNoTracking().SingleAsync();
        Assert.Equal(new LocalTime(19, 0), reminder.LocalTime);
        Assert.Equal(1 + 4 + 16 + 64, reminder.DaysOfWeek);
        Assert.Equal(Clock.GetCurrentInstant(), reminder.CreatedAt);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actual = body.RootElement.GetProperty("reminder");
        Assert.Equal("19:00", actual.GetProperty("local_time").GetString());
        Assert.Equal(new[] { "mon", "wed", "fri", "sun" }, actual.GetProperty("days_of_week").EnumerateArray().Select(day => day.GetString()));
        Assert.True(body.RootElement.GetProperty("time_zone_confirmed").GetBoolean());
    }

    [DockerFact]
    public async Task InvalidSettings_ReturnContract400_WithoutSavingLearnerOrReminder()
    {
        await using var app = await StartAsync();
        (string Json, string Code)[] cases =
        [
            ("""{"daily_new_words_limit":5,"time_zone":"Europe/Warsaw","reminder":{"local_time":"19:00","days_of_week":[]}}""", "settings.reminder_days_required"),
            ("""{"daily_new_words_limit":5,"reminder":{"local_time":"19:00","days_of_week":["mon"]}}""", "settings.time_zone_required"),
            ("""{"daily_new_words_limit":5,"time_zone":"Unknown/Zone","reminder":{"local_time":"19:00","days_of_week":["mon"]}}""", "settings.time_zone_required"),
            ("""{"daily_new_words_limit":4}""", "settings.invalid_limit"),
            ("""{"daily_new_words_limit":3.5}""", "settings.invalid_limit"),
            ("""{"daily_new_words_limit":"5"}""", "settings.invalid_limit"),
            ("{}", "settings.invalid_limit"),
            ("null", "settings.invalid_request"),
            ("{", "settings.invalid_request"),
            ("""{"daily_new_words_limit":5,"telegram_user_id":42}""", "settings.invalid_request"),
            ("""{"daily_new_words_limit":3,"daily_new_words_limit":5}""", "settings.invalid_request"),
            ("""{"daily_new_words_limit":5,"time_zone":42}""", "settings.invalid_request"),
            ("""{"daily_new_words_limit":5,"time_zone":"Europe/Warsaw","reminder":{"local_time":"24:00","days_of_week":["mon"]}}""", "settings.invalid_reminder"),
            ("""{"daily_new_words_limit":5,"time_zone":"Europe/Warsaw","reminder":{"local_time":"19:00","days_of_week":["mon","mon"]}}""", "settings.invalid_reminder"),
            ("""{"daily_new_words_limit":5,"time_zone":"Europe/Warsaw","reminder":{"local_time":"19:00","days_of_week":["monday"]}}""", "settings.invalid_reminder")
        ];
        foreach (var (json, code) in cases)
        {
            using var response = await PostAsync(app.Client, json);
            await AssertResponseAsync(response, HttpStatusCode.BadRequest, "completeOnboarding", code);
            Assert.Empty(await Db.Learners.AsNoTracking().ToListAsync());
            Assert.Empty(await Db.ReminderSchedules.AsNoTracking().ToListAsync());
        }
    }

    [DockerFact]
    public async Task RepeatedOnboarding_IgnoresChangedAndInvalidBodies_BeforeValidation()
    {
        await using var app = await StartAsync();
        using var original = await PostAsync(app.Client,
            """{"daily_new_words_limit":5,"time_zone":"Europe/Warsaw","reminder":{"local_time":"19:00","days_of_week":["mon","wed","fri"]}}""");
        await AssertResponseAsync(original, HttpStatusCode.Created, "completeOnboarding");
        var expected = await original.Content.ReadAsStringAsync();
        var originalVersion = (await Db.Learners.AsNoTracking().SingleAsync()).Version;
        var reminderVersion = (await Db.ReminderSchedules.AsNoTracking().SingleAsync()).Version;
        Clock.Advance(Duration.FromHours(1));
        string[] bodies =
        [
            """{"daily_new_words_limit":10,"time_zone":"Europe/Kyiv","reminder":null}""",
            """{"daily_new_words_limit":4,"reminder":{"local_time":"19:00","days_of_week":[]}}""",
            "{}", "null", "{", ""
        ];
        foreach (var body in bodies)
        {
            using var response = await PostAsync(app.Client, body);
            await AssertResponseAsync(response, HttpStatusCode.OK, "completeOnboarding");
            Assert.Equal(expected, await response.Content.ReadAsStringAsync());
        }

        var learner = await Db.Learners.AsNoTracking().SingleAsync();
        Assert.Equal(originalVersion, learner.Version);
        Assert.Equal(reminderVersion, (await Db.ReminderSchedules.AsNoTracking().SingleAsync()).Version);
        Assert.Equal(Instant.FromUtc(2026, 10, 31, 9, 0), learner.OnboardedAt);
    }

    [DockerFact]
    public async Task ConcurrentOnboarding_ReturnsOne201AndOne200_WithExactlyOneUnmodifiedWinner()
    {
        var barrier = new ConcurrentLearnerWriteBarrier();
        await using var app = await StartAsync(barrier);
        var requests = new[]
        {
            PostAsync(app.Client, """{"daily_new_words_limit":3,"time_zone":"Europe/Warsaw","reminder":null}"""),
            PostAsync(app.Client, """{"daily_new_words_limit":10,"time_zone":"Europe/Kyiv","reminder":{"local_time":"08:30","days_of_week":["tue","sat"]}}""")
        };
        var responses = await Task.WhenAll(requests);
        try
        {
            Assert.Equal(2, barrier.WriteCount);
            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Created }, responses.Select(response => response.StatusCode).Order());
            foreach (var response in responses)
            {
                await ContractAssert.ResponseAsync("completeOnboarding", response);
            }

            Assert.Equal(await responses[0].Content.ReadAsStringAsync(), await responses[1].Content.ReadAsStringAsync());
            var learner = await Db.Learners.AsNoTracking().SingleAsync();
            using var body = JsonDocument.Parse(await responses[0].Content.ReadAsStringAsync());
            Assert.Equal(learner.Id, body.RootElement.GetProperty("id").GetGuid());
            Assert.Equal(learner.DailyNewWordsLimit == 10 ? 1 : 0, await Db.ReminderSchedules.CountAsync());
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
    public async Task Profile_RequiresCompletedOnboarding_AndNeverUsesQueryIdentity()
    {
        await using var app = await StartAsync();
        using var missing = await app.Client.GetAsync(ProfilePath);
        await AssertResponseAsync(missing, HttpStatusCode.NotFound, "getCurrentLearner", "learner.not_onboarded");
        Assert.Equal("Спершу налаштуйте тренер.", JsonNode.Parse(await missing.Content.ReadAsStringAsync())!["message"]!.GetValue<string>());

        using var created = await PostAsync(app.Client, """{"daily_new_words_limit":5}""");
        var learner = await Db.Learners.AsNoTracking().SingleAsync();
        Authorize(app.Client, TelegramUserId + 1);
        using var foreign = await app.Client.GetAsync($"{ProfilePath}?learner_id={learner.Id}&telegram_user_id={TelegramUserId}");
        await AssertResponseAsync(foreign, HttpStatusCode.NotFound, "getCurrentLearner", "learner.not_onboarded");

        learner.Id = Guid.CreateVersion7();
        learner.TelegramUserId = TelegramUserId + 1;
        learner.OnboardedAt = null;
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        using var incomplete = await app.Client.GetAsync(ProfilePath);
        await AssertResponseAsync(incomplete, HttpStatusCode.NotFound, "getCurrentLearner", "learner.not_onboarded");
    }

    [DockerFact]
    public async Task IncompleteProfile_IsCompletedAtomically_PreservingIdAndCreationTime()
    {
        var learner = new Learner
        {
            TelegramUserId = TelegramUserId,
            TimeZone = "Europe/Warsaw",
            DailyNewWordsLimit = 3,
            CreatedAt = Clock.GetCurrentInstant() - Duration.FromDays(1)
        };
        Db.Learners.Add(learner);
        await Db.SaveChangesAsync();
        await using var app = await StartAsync();
        using var invalid = await PostAsync(app.Client, """{"daily_new_words_limit":4}""");
        await AssertResponseAsync(invalid, HttpStatusCode.BadRequest, "completeOnboarding", "settings.invalid_limit");
        Assert.Null((await Db.Learners.AsNoTracking().SingleAsync()).OnboardedAt);

        using var completed = await PostAsync(app.Client,
            """{"daily_new_words_limit":10,"time_zone":"Europe/Kyiv","reminder":{"local_time":"08:30","days_of_week":["tue"]}}""");
        await AssertResponseAsync(completed, HttpStatusCode.Created, "completeOnboarding");
        var stored = await Db.Learners.AsNoTracking().SingleAsync();
        Assert.Equal(learner.Id, stored.Id);
        Assert.Equal(learner.CreatedAt, stored.CreatedAt);
        Assert.Equal(Clock.GetCurrentInstant(), stored.OnboardedAt);
        Assert.Equal("Test User", stored.FirstName);
        Assert.Equal("Europe/Kyiv", stored.TimeZone);
        Assert.True(stored.TimeZoneConfirmed);
        Assert.Equal(10, stored.DailyNewWordsLimit);
        Assert.False(stored.CanMessage);
        Assert.Single(await Db.ReminderSchedules.ToListAsync());
        using var profile = await app.Client.GetAsync(ProfilePath);
        await AssertResponseAsync(profile, HttpStatusCode.OK, "getCurrentLearner");
        Assert.Equal(await completed.Content.ReadAsStringAsync(), await profile.Content.ReadAsStringAsync());
    }

    [DockerFact]
    public async Task ConcurrentIncompleteProfileCompletion_PreservesFirstCompletedSettings()
    {
        Db.Learners.Add(new Learner
        {
            TelegramUserId = TelegramUserId,
            TimeZone = "Europe/Warsaw",
            DailyNewWordsLimit = 3,
            CreatedAt = Clock.GetCurrentInstant()
        });
        await Db.SaveChangesAsync();
        var barrier = new ConcurrentLearnerWriteBarrier("UPDATE learners");
        await using var app = await StartAsync(barrier);
        var responses = await Task.WhenAll(
            PostAsync(app.Client, """{"daily_new_words_limit":3,"time_zone":"Europe/Warsaw","reminder":{"local_time":"09:00","days_of_week":["mon"]}}"""),
            PostAsync(app.Client, """{"daily_new_words_limit":10,"time_zone":"Europe/Kyiv","reminder":{"local_time":"08:30","days_of_week":["tue"]}}"""));
        try
        {
            Assert.Equal(2, barrier.WriteCount);
            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Created }, responses.Select(response => response.StatusCode).Order());
            foreach (var response in responses)
            {
                await ContractAssert.ResponseAsync("completeOnboarding", response);
            }

            Assert.Equal(await responses[0].Content.ReadAsStringAsync(), await responses[1].Content.ReadAsStringAsync());
            Assert.Single(await Db.Learners.AsNoTracking().ToListAsync());
            Assert.Single(await Db.ReminderSchedules.AsNoTracking().ToListAsync());
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
    public async Task BothEndpoints_RequireRealValidTelegramSignature()
    {
        await using var app = await OnboardingTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        string?[] credentials = [null, "Bearer wrong", "tma " + new TelegramInitDataSigner("wrong-bot").Sign(TelegramUserId, Clock.GetCurrentInstant())];
        foreach (var credential in credentials)
        {
            app.Client.DefaultRequestHeaders.Remove("Authorization");
            if (credential is not null)
            {
                app.Client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", credential);
            }

            using var profile = await app.Client.GetAsync(ProfilePath);
            using var onboarding = await PostAsync(app.Client, """{"daily_new_words_limit":5}""");
            await AssertResponseAsync(profile, HttpStatusCode.Unauthorized, "getCurrentLearner", "auth.invalid_init_data");
            await AssertResponseAsync(onboarding, HttpStatusCode.Unauthorized, "completeOnboarding", "auth.invalid_init_data");
        }

        Assert.Empty(await Db.Learners.ToListAsync());
    }

    private async Task<OnboardingTestApplication> StartAsync(IInterceptor? interceptor = null)
    {
        var application = await OnboardingTestApplication.StartAsync(_postgres.ConnectionString, Clock, interceptor);
        Authorize(application.Client, TelegramUserId);
        return application;
    }

    private void Authorize(HttpClient client, long telegramUserId) => client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("tma", new TelegramInitDataSigner().Sign(telegramUserId, Clock.GetCurrentInstant()));

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string body) =>
        client.PostAsync(OnboardingPath, new StringContent(body, Encoding.UTF8, "application/json"));

    private static async Task AssertResponseAsync(HttpResponseMessage response, HttpStatusCode status, string operation, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        await ContractAssert.ResponseAsync(operation, response);
        if (code is not null)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        }
    }

    private sealed class ConcurrentLearnerWriteBarrier(string statement = "INSERT INTO learners") : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _writeCount;
        public int WriteCount => _writeCount;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(statement, StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _writeCount) == 2)
                {
                    _ready.SetResult();
                }

                // Обидва запити вже пройшли читання профілю, перш ніж дозволяємо вставлення.
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }

            return result;
        }
    }
}
