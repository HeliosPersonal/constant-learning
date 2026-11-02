using ConstantLearning.Configuration;
using ConstantLearning.Platform.Telegram;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types.Enums;

namespace ConstantLearning.HostedServices;

public class WebhookConfigurationService(ITelegramClient telegram, IOptions<TelegramOptions> telegramOptions,
    ILogger<WebhookConfigurationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = telegramOptions.Value;
        if (string.IsNullOrWhiteSpace(options.WebhookUrl))
        {
            logger.LogWarning("Telegram:WebhookUrl не налаштовано; реєстрацію webhook пропущено.");
            return;
        }

        if (options.SecretToken is not { Length: > 0 and <= 256 } secret
            || secret.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-')))
        {
            throw new InvalidOperationException("Telegram:SecretToken має містити 1–256 символів A-Z, a-z, 0-9, _ або -.");
        }

        if (!Uri.TryCreate(options.WebhookUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException("Telegram:WebhookUrl має бути HTTPS-адресою без query, fragment і credentials.");
        }

        if (!Uri.TryCreate(options.MiniAppUrl, UriKind.Absolute, out var miniApp) || miniApp.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Потрібна HTTPS-адреса Telegram:MiniAppUrl.");
        }

        const string endpoint = "/api/telegram/webhook";
        var webhookUrl = options.WebhookUrl.TrimEnd('/');
        if (!webhookUrl.EndsWith(endpoint, StringComparison.OrdinalIgnoreCase))
        {
            webhookUrl += endpoint;
        }

        await telegram.ConfigureWebhookAsync(webhookUrl, secret, [UpdateType.Message, UpdateType.MyChatMember], cancellationToken);
        logger.LogInformation("Telegram webhook налаштовано.");
    }

    // Спільний webhook зберігається під час зупинки або rolling deploy.
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
