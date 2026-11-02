using System.Net;
using System.Text;
using System.Text.Json;
using ConstantLearning.Configuration;
using ConstantLearning.HostedServices;
using ConstantLearning.Platform.Telegram;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types.Enums;

namespace ConstantLearning.Tests.Bot;

public sealed class WebhookConfigurationTests
{
    [Theory]
    [InlineData("https://trainer.example.test", "https://trainer.example.test/api/telegram/webhook")]
    [InlineData("https://trainer.example.test/api/telegram/webhook/", "https://trainer.example.test/api/telegram/webhook")]
    public async Task Start_ConfiguresSecretAndExplicitUpdates_AndStopDoesNotChangeWebhook(string input, string expected)
    {
        var telegram = new StubTelegramClient();
        var service = Create(telegram, input);
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        var webhook = Assert.Single(telegram.Webhooks);
        Assert.Equal(expected, webhook.Url);
        Assert.Equal(BotTestApplication.Secret, webhook.Secret);
        Assert.Equal(new[] { UpdateType.Message, UpdateType.MyChatMember }, webhook.Updates);
    }

    [Fact]
    public async Task MissingWebhookUrl_DoesNotMakeExternalCalls()
    {
        var telegram = new StubTelegramClient();
        await Create(telegram, null).StartAsync(CancellationToken.None);
        Assert.Empty(telegram.Webhooks);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("spaces are invalid")]
    public async Task ConfiguredWebhook_RequiresValidSecret(string? secret)
    {
        var telegram = new StubTelegramClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(telegram,
            "https://trainer.example.test", secret).StartAsync(CancellationToken.None));
        Assert.Empty(telegram.Webhooks);
    }

    [Fact]
    public async Task PlatformWrapper_SerializesWebhookAndMessage_ThroughStubHttpTransport()
    {
        var transport = new TelegramTransport();
        var services = new ServiceCollection();
        services.Configure<TelegramOptions>(options => options.BotToken = TelegramInitDataSigner.TestBotToken);
        services.AddTelegram();
        services.AddHttpClient("Telegram").ConfigurePrimaryHttpMessageHandler(() => transport);
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ITelegramClient>();
        Assert.Empty(transport.Requests);

        await client.ConfigureWebhookAsync("https://trainer.example.test/api/telegram/webhook",
            BotTestApplication.Secret, [UpdateType.Message, UpdateType.MyChatMember], CancellationToken.None);
        await client.SendMessageAsync(900000001, "Тестове повідомлення");

        Assert.Equal(2, transport.Requests.Count);
        using var webhook = JsonDocument.Parse(transport.Requests[0]);
        Assert.Equal(BotTestApplication.Secret, webhook.RootElement.GetProperty("secret_token").GetString());
        Assert.Equal(new[] { "message", "my_chat_member" }, webhook.RootElement.GetProperty("allowed_updates")
            .EnumerateArray().Select(value => value.GetString()));
        using var message = JsonDocument.Parse(transport.Requests[1]);
        Assert.Equal(900000001, message.RootElement.GetProperty("chat_id").GetInt64());
        Assert.Equal("Тестове повідомлення", message.RootElement.GetProperty("text").GetString());
    }

    private static WebhookConfigurationService Create(StubTelegramClient client, string? url,
        string? secret = BotTestApplication.Secret) => new(client, Options.Create(new TelegramOptions
        {
            BotToken = TelegramInitDataSigner.TestBotToken, WebhookUrl = url, SecretToken = secret,
            MiniAppUrl = BotTestApplication.MiniAppUrl
        }), NullLogger<WebhookConfigurationService>.Instance);

    private sealed class TelegramTransport : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var result = request.RequestUri!.AbsolutePath.EndsWith("setWebhook", StringComparison.Ordinal)
                ? """{"ok":true,"result":true}"""
                : """{"ok":true,"result":{"message_id":1,"date":1793440800,"chat":{"id":900000001,"type":"private"},"text":"Тестове повідомлення"}}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(result, Encoding.UTF8, "application/json") };
        }
    }
}
