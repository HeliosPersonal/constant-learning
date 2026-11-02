using ConstantLearning.Features.Bot;
using ConstantLearning.Features.Identity;
using ConstantLearning.Features.Identity.Messaging;
using ConstantLearning.Features.Learning;
using ConstantLearning.Platform;
using ConstantLearning.Platform.Telegram;
using ConstantLearning.Tests.Identity.Auth;
using ConstantLearning.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodaTime;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConstantLearning.Tests.Bot;

internal sealed class BotTestApplication(WebApplication app, HttpClient client, StubTelegramClient telegram,
    CapturedAuthLogs logs) : IAsyncDisposable
{
    public const string Secret = "test-webhook-secret";
    public const string MiniAppUrl = "https://trainer.example.test/";
    public HttpClient Client { get; } = client;
    public StubTelegramClient Telegram { get; } = telegram;
    public CapturedAuthLogs Logs { get; } = logs;

    public static async Task<BotTestApplication> StartAsync(string connectionString, IClock clock,
        IMessagingPermissionService? messaging = null, bool failSending = false, string? secret = Secret)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Telegram:BotToken"] = TelegramInitDataSigner.TestBotToken,
            ["Telegram:SecretToken"] = secret,
            ["Telegram:MiniAppUrl"] = MiniAppUrl
        });
        var logs = new CapturedAuthLogs();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Services.AddSingleton(clock);
        builder.Services.AddPlatform(builder.Configuration);
        builder.Services.AddIdentity(builder.Configuration);
        builder.Services.AddLearning();
        builder.Services.AddBot();
        if (messaging is not null)
        {
            builder.Services.AddSingleton(messaging);
        }
        var telegram = new StubTelegramClient { FailSending = failSending };
        builder.Services.AddSingleton<ITelegramClient>(telegram);
        builder.Services.AddControllers().AddApplicationPart(typeof(BotModule).Assembly);
        var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return new BotTestApplication(app, app.GetTestClient(), telegram, logs);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
    }
}

internal sealed class StubTelegramClient : ITelegramClient
{
    public bool FailSending { get; init; }
    public List<(long ChatId, string Text, InlineKeyboardMarkup? Markup)> Messages { get; } = [];
    public List<(string Url, string Secret, IReadOnlyCollection<UpdateType> Updates)> Webhooks { get; } = [];

    public Task SendMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.None,
        InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        if (FailSending)
        {
            throw new HttpRequestException("https://api.telegram.org/botSECRET/sendMessage персональні дані");
        }
        Messages.Add((chatId, text, replyMarkup));
        return Task.CompletedTask;
    }

    public Task ConfigureWebhookAsync(string url, string secretToken, IReadOnlyCollection<UpdateType> allowedUpdates,
        CancellationToken cancellationToken)
    {
        Webhooks.Add((url, secretToken, allowedUpdates));
        return Task.CompletedTask;
    }
}

internal sealed class StubMessagingPermissionService : IMessagingPermissionService
{
    public Task<bool?> GetCanMessageAsync(long telegramUserId, CancellationToken cancellationToken) =>
        Task.FromResult<bool?>(null);

    public List<(long TelegramUserId, bool CanMessage)> Changes { get; } = [];

    public Task SetCanMessageAsync(long telegramUserId, bool canMessage, CancellationToken cancellationToken)
    {
        Changes.Add((telegramUserId, canMessage));
        return Task.CompletedTask;
    }
}
