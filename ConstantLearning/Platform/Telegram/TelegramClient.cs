using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConstantLearning.Platform.Telegram;

internal sealed class TelegramClient(ITelegramBotClient client) : ITelegramClient
{
    public async Task SendMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.None,
        InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default) =>
        await client.SendMessage(chatId, text, parseMode: parseMode, replyMarkup: replyMarkup,
            cancellationToken: cancellationToken);

    public Task ConfigureWebhookAsync(string url, string secretToken, IReadOnlyCollection<UpdateType> allowedUpdates,
        CancellationToken cancellationToken) => client.SetWebhook(url, secretToken: secretToken,
            allowedUpdates: allowedUpdates, cancellationToken: cancellationToken);
}
