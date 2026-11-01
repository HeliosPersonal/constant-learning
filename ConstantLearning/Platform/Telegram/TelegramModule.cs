using ConstantLearning.Configuration;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace ConstantLearning.Platform.Telegram;

public static class TelegramModule
{
    public static IServiceCollection AddTelegram(this IServiceCollection services)
    {
        // Повідомлення не повторюємо автоматично: після тайм-ауту Telegram міг уже прийняти його.
        // Стандартний HTTP-журнал містить URL із BotToken, тому прибираємо його для цього клієнта.
        services.AddHttpClient("Telegram", client => client.Timeout = TimeSpan.FromSeconds(15)).RemoveAllLoggers();
        services.AddSingleton<ITelegramClient>(provider => new TelegramClient(new TelegramBotClient(
            new TelegramBotClientOptions(provider.GetRequiredService<IOptions<TelegramOptions>>().Value.BotToken)
            {
                RetryCount = 0
            },
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("Telegram"))));
        return services;
    }
}
