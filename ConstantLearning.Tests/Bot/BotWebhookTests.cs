using System.Net;
using System.Text;
using System.Text.Json;
using ConstantLearning.Tests.Infrastructure;
using NodaTime;

namespace ConstantLearning.Tests.Bot;

public sealed class BotWebhookTests
{
    internal const string StartUpdate = """
        {"update_id":1,"message":{"message_id":1,"date":1793440800,
        "chat":{"id":900000001,"type":"private"},
        "from":{"id":900000001,"is_bot":false,"first_name":"Приватне ім'я"},"text":"/start"}}
        """;

    [Fact]
    public async Task MissingIncorrectDuplicatedAndOversizedSecret_ReturnExactContractBeforeReadingBody()
    {
        var messaging = new StubMessagingPermissionService();
        await using var app = await StartAsync(messaging);
        string[]?[] secrets = [null, ["wrong-secret"], [BotTestApplication.Secret + " "],
            [BotTestApplication.Secret, BotTestApplication.Secret], [new string('x', 257)]];
        foreach (var values in secrets)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/telegram/webhook")
            {
                Content = new StringContent("not-json", Encoding.UTF8, "application/json")
            };
            if (values is not null)
            {
                request.Headers.TryAddWithoutValidation("X-Telegram-Bot-Api-Secret-Token", values);
            }
            using var response = await app.Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await ContractAssert.ResponseAsync("receiveTelegramUpdate", response);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("webhook.invalid_secret", body.RootElement.GetProperty("code").GetString());
            Assert.Equal("Невірний секрет.", body.RootElement.GetProperty("message").GetString());
            Assert.Equal(2, body.RootElement.EnumerateObject().Count());
        }
        Assert.Empty(messaging.Changes);
        Assert.Empty(app.Telegram.Messages);
    }

    [Fact]
    public async Task UnconfiguredSecret_RejectsRequest_AndHealthRemainsPublic()
    {
        await using var app = await BotTestApplication.StartAsync("Host=unused", SystemClock.Instance,
            new StubMessagingPermissionService(), secret: null);
        using var denied = await PostAsync(app.Client, StartUpdate);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var health = await app.Client.GetAsync("/api/telegram/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        using var body = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
        Assert.Equal("healthy", body.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Start_SendsMiniAppButton_WithoutInitDataAuthentication()
    {
        var messaging = new StubMessagingPermissionService();
        await using var app = await StartAsync(messaging);
        using var response = await PostAsync(app.Client, StartUpdate);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ContractAssert.ResponseAsync("receiveTelegramUpdate", response);
        var sent = Assert.Single(app.Telegram.Messages);
        Assert.Equal(900000001, sent.ChatId);
        var button = Assert.Single(Assert.Single(sent.Markup!.InlineKeyboard));
        Assert.Equal("Відкрити тренер", button.Text);
        Assert.Equal(BotTestApplication.MiniAppUrl, button.WebApp!.Url);
        Assert.Equal((900000001L, true), Assert.Single(messaging.Changes));
    }

    [Fact]
    public async Task UnknownAndGroupUpdates_AreAcknowledgedWithoutSideEffects()
    {
        var messaging = new StubMessagingPermissionService();
        await using var app = await StartAsync(messaging);
        var updates = new[] { """{"update_id":2}""", StartUpdate.Replace("/start", "/startlearning"),
            StartUpdate.Replace("/start", "/help"), StartUpdate.Replace("\"private\"", "\"group\""),
            MembershipUpdate("kicked", "supergroup") };
        foreach (var update in updates)
        {
            using var response = await PostAsync(app.Client, update);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await ContractAssert.ResponseAsync("receiveTelegramUpdate", response);
        }
        Assert.Empty(messaging.Changes);
        Assert.Empty(app.Telegram.Messages);
    }

    [Fact]
    public async Task HandlerAndJsonFailures_AreAcknowledged_WithoutLoggingSecretsOrPersonalData()
    {
        await using var app = await BotTestApplication.StartAsync("Host=unused", SystemClock.Instance,
            new StubMessagingPermissionService(), failSending: true);
        foreach (var update in new[] { StartUpdate, "invalid-json" })
        {
            using var response = await PostAsync(app.Client, update);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await ContractAssert.ResponseAsync("receiveTelegramUpdate", response);
        }
        var logs = string.Join('\n', app.Logs.Entries);
        Assert.Contains("Помилка обробки Telegram webhook", logs);
        Assert.DoesNotContain("SECRET", logs);
        Assert.DoesNotContain(BotTestApplication.Secret, logs);
        Assert.DoesNotContain("Приватне ім'я", logs);
    }

    internal static string MembershipUpdate(string status, string chatType = "private") => """
        {"update_id":2,"my_chat_member":{"chat":{"id":900000001,"type":"CHAT_TYPE"},
        "from":{"id":999999999,"is_bot":false,"first_name":"Ініціатор"},"date":1793440800,
        "old_chat_member":{"status":"member","user":{"id":123,"is_bot":true,"first_name":"Bot"}},
        "new_chat_member":{"status":"MEMBER_STATUS","user":{"id":123,"is_bot":true,"first_name":"Bot"},"until_date":0}}}
        """.Replace("CHAT_TYPE", chatType, StringComparison.Ordinal).Replace("MEMBER_STATUS", status, StringComparison.Ordinal);

    internal static Task<HttpResponseMessage> PostAsync(HttpClient client, string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/telegram/webhook")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", BotTestApplication.Secret);
        return client.SendAsync(request);
    }

    private static Task<BotTestApplication> StartAsync(StubMessagingPermissionService messaging) =>
        BotTestApplication.StartAsync("Host=unused", SystemClock.Instance, messaging);
}
