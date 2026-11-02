using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConstantLearning.Platform.Telegram;

public interface ITelegramClient
{
    Task SendMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.None,
        InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default);

    Task ConfigureWebhookAsync(string url, string secretToken, IReadOnlyCollection<UpdateType> allowedUpdates,
        CancellationToken cancellationToken);
}
