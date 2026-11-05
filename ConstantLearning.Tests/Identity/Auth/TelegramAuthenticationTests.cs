using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ConstantLearning.Features.Identity.Data;
using ConstantLearning.Tests.Fixtures;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Tests.Identity.Auth;

public sealed class TelegramAuthenticationTests(PostgresFixture postgres) : PostgresIntegrationTest(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    [DockerFact]
    public async Task ValidIdentity_UsesDatabaseLearnerAndIgnoresClientClaims()
    {
        var learner = await CreateLearnerAsync();
        var other = await CreateLearnerAsync();
        var signedData = new TelegramInitDataSigner().Sign(learner.TelegramUserId, Clock.GetCurrentInstant(),
            new Dictionary<string, string>
            {
                ["learner_id"] = other.Id.ToString(),
                ["role"] = "ContentEditor",
                ["telegram_user_id"] = other.TelegramUserId.ToString(CultureInfo.InvariantCulture)
            });
        await using var app = await AuthTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma", signedData);
        app.Client.DefaultRequestHeaders.Add("X-Learner-Id", other.Id.ToString());

        var response = await app.Client.GetAsync($"/auth-test/me?learner_id={other.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(learner.Id, body.RootElement.GetProperty("learner_id").GetGuid());
        Assert.Equal(learner.TelegramUserId.ToString(CultureInfo.InvariantCulture), body.RootElement.GetProperty("telegram_user_id").GetString());
        Assert.Equal("Test User", body.RootElement.GetProperty("first_name").GetString());
        var editor = await app.Client.GetAsync("/auth-test/editor/queue");
        await AssertErrorAsync(editor, HttpStatusCode.Forbidden, "editor.role_required", "listEditorQueueByLanguage");
    }

    [DockerFact]
    public async Task NewTelegramIdentity_AuthenticatesWithoutCreatingLearner()
    {
        var identity = LearnerFixtures.NewLearner(clock: Clock);
        await using var app = await AuthTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        app.Client.DefaultRequestHeaders.Authorization = new TelegramInitDataSigner().Authorization(identity, Clock);

        var response = await app.Client.GetAsync("/auth-test/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("learner_id").ValueKind);
        Assert.Equal(identity.TelegramUserId.ToString(CultureInfo.InvariantCulture), body.RootElement.GetProperty("telegram_user_id").GetString());
        Assert.Equal("Test User", body.RootElement.GetProperty("first_name").GetString());
        Assert.Empty(await Db.Learners.ToListAsync());
    }

    [DockerFact]
    public async Task MissingMalformedForgedExpiredAndFutureCredentials_ReturnContract401()
    {
        await using var app = await AuthTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        var signer = new TelegramInitDataSigner();
        var now = Clock.GetCurrentInstant();
        string?[] headers =
        [
            null, "Bearer incorrect", "tma", "tma user=malformed", "tma hash=%XX",
            "tma " + new TelegramInitDataSigner("wrong-bot").Sign(123, now),
            "tma " + signer.Sign(123, now - Duration.FromSeconds(86401)),
            "tma " + signer.Sign(123, now + Duration.FromSeconds(1)),
            "tma " + signer.Sign(123, now) + "&user=%7B%22id%22%3A456%7D"
        ];
        foreach (var header in headers)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/telemetry/client-timings")
            {
                Content = JsonContent.Create(new { metric = "today_ready_ms", value_ms = 15 })
            };
            if (header is not null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", header);
            }

            var response = await app.Client.SendAsync(request);
            await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "auth.invalid_init_data", "reportClientTiming");
            Assert.Equal("tma", response.Headers.WwwAuthenticate.Single().Scheme);
        }

        using var duplicated = new HttpRequestMessage(HttpMethod.Get, "/auth-test/me");
        duplicated.Headers.TryAddWithoutValidation("Authorization", ["tma " + signer.Sign(123, now), "tma " + signer.Sign(456, now)]);
        await AssertErrorAsync(await app.Client.SendAsync(duplicated), HttpStatusCode.Unauthorized, "auth.invalid_init_data", "getCurrentLearner");
    }

    [DockerFact]
    public async Task EveryRequest_RechecksExpiryAndDatabaseIdentity()
    {
        var learner = await CreateLearnerAsync();
        await using var app = await AuthTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        app.Client.DefaultRequestHeaders.Authorization = new TelegramInitDataSigner().Authorization(learner, Clock);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync("/auth-test/me")).StatusCode);

        await Db.Learners.Where(value => value.Id == learner.Id).ExecuteDeleteAsync();
        using var withoutLearner = JsonDocument.Parse(await app.Client.GetStringAsync("/auth-test/me"));
        Assert.Equal(JsonValueKind.Null, withoutLearner.RootElement.GetProperty("learner_id").ValueKind);

        Clock.Advance(Duration.FromDays(1) + Duration.FromSeconds(1));
        await AssertErrorAsync(await app.Client.GetAsync("/auth-test/me"), HttpStatusCode.Unauthorized,
            "auth.invalid_init_data", "getCurrentLearner");
    }

    [DockerFact]
    public async Task ScopedResource_ConcealsOtherLearnerAndUnknownResourceWithSame404()
    {
        var learner = await CreateLearnerAsync();
        var other = await CreateLearnerAsync();
        await using var app = await AuthTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        app.Client.DefaultRequestHeaders.Authorization = new TelegramInitDataSigner().Authorization(learner, Clock);

        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync($"/auth-test/resources/{learner.Id}")).StatusCode);
        var foreign = await app.Client.GetAsync($"/auth-test/resources/{other.Id}?learner_id={other.Id}");
        var unknown = await app.Client.GetAsync($"/auth-test/resources/{Guid.NewGuid()}");
        await AssertErrorAsync(foreign, HttpStatusCode.NotFound, "session.item_not_found", "answerSessionItem");
        await AssertErrorAsync(unknown, HttpStatusCode.NotFound, "session.item_not_found", "answerSessionItem");
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await foreign.Content.ReadAsStringAsync());

        app.Client.DefaultRequestHeaders.Authorization = new TelegramInitDataSigner().Authorization(other, Clock);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync($"/auth-test/resources/{other.Id}")).StatusCode);
        await AssertErrorAsync(await app.Client.GetAsync($"/auth-test/resources/{learner.Id}"), HttpStatusCode.NotFound,
            "session.item_not_found", "answerSessionItem");
    }

    [DockerFact]
    public async Task EditorPolicy_RequiresConfiguredTelegramIdentityForReadAndRollback()
    {
        var ordinary = await CreateLearnerAsync();
        var editor = await CreateLearnerAsync();
        await using var app = await AuthTestApplication.StartAsync(_postgres.ConnectionString, Clock, editor.TelegramUserId);
        var cases = new[]
        {
            (HttpMethod.Get, "/auth-test/editor/queue", "listEditorQueueByLanguage"),
            (HttpMethod.Get, "/auth-test/editor/changes", "listAppliedChanges"),
            (HttpMethod.Post, "/auth-test/editor/rollback", "rollbackAppliedChange")
        };
        foreach (var (method, path, operation) in cases)
        {
            app.Client.DefaultRequestHeaders.Authorization = new TelegramInitDataSigner().Authorization(ordinary, Clock);
            await AssertErrorAsync(await app.Client.SendAsync(new HttpRequestMessage(method, path)), HttpStatusCode.Forbidden,
                "editor.role_required", operation);
            app.Client.DefaultRequestHeaders.Authorization = new TelegramInitDataSigner().Authorization(editor, Clock);
            Assert.Equal(HttpStatusCode.OK, (await app.Client.SendAsync(new HttpRequestMessage(method, path))).StatusCode);
        }
    }

    [DockerFact]
    public async Task LearnerAccess_ReportsEditorRoleOnlyForConfiguredIdentityAndRequiresAuthentication()
    {
        var ordinary = await CreateLearnerAsync();
        var editor = await CreateLearnerAsync();
        await using var app = await AuthTestApplication.StartAsync(_postgres.ConnectionString, Clock, editor.TelegramUserId);

        await AssertErrorAsync(await app.Client.GetAsync("/api/v1/learners/me/access"), HttpStatusCode.Unauthorized,
            "auth.invalid_init_data", "getLearnerAccess");
        foreach (var (learner, expected) in new[] { (ordinary, false), (editor, true) })
        {
            app.Client.DefaultRequestHeaders.Authorization = new TelegramInitDataSigner().Authorization(learner, Clock);
            var response = await app.Client.GetAsync("/api/v1/learners/me/access");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await ContractAssert.ResponseAsync("getLearnerAccess", response);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(expected, body.RootElement.GetProperty("content_editor").GetBoolean());
        }
    }

    [DockerFact]
    public async Task Telemetry_UsesRealAuthenticationAndLogsNeverContainCredentialsOrTelegramPersonalData()
    {
        var learner = await CreateLearnerAsync();
        var data = new TelegramInitDataSigner().Sign(learner.TelegramUserId, Clock.GetCurrentInstant());
        await using var app = await AuthTestApplication.StartAsync(_postgres.ConnectionString, Clock);
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma", data);
        var response = await app.Client.PostAsJsonAsync("/api/v1/telemetry/client-timings",
            new { metric = "today_ready_ms", value_ms = 18 });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await ContractAssert.ResponseAsync("reportClientTiming", response);
        await app.Client.GetAsync("/auth-test/editor/queue");
        app.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("tma", data + "&extra=secret-value");
        await app.Client.GetAsync("/auth-test/me");

        var logs = string.Join('\n', app.Logs.Entries);
        Assert.Contains(learner.Id.ToString(), logs, StringComparison.Ordinal);
        Assert.Contains("invalid_init_data", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(data, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.UnescapeDataString(data), logs, StringComparison.Ordinal);
        Assert.DoesNotContain(TelegramInitDataSigner.TestBotToken, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(learner.TelegramUserId.ToString(CultureInfo.InvariantCulture), logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Test User", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization:", logs, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<LearnerData> CreateLearnerAsync()
    {
        var fixture = LearnerFixtures.NewLearner(clock: Clock);
        Db.Learners.Add(new Learner
        {
            Id = fixture.Id,
            TelegramUserId = fixture.TelegramUserId,
            FirstName = fixture.FirstName,
            TimeZone = fixture.TimeZone,
            TimeZoneConfirmed = fixture.TimeZoneConfirmed,
            DailyNewWordsLimit = (short)fixture.DailyNewWordsLimit,
            CreatedAt = fixture.CreatedAt
        });
        await Db.SaveChangesAsync();
        return fixture;
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code, string operation)
    {
        Assert.Equal(status, response.StatusCode);
        await ContractAssert.ResponseAsync(operation, response);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.Equal(2, body.RootElement.EnumerateObject().Count());
    }
}
