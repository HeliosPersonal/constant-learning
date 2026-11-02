using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConstantLearning.Platform.Telegram;

internal sealed class TelegramClient(ITelegramBotClient client) : ITelegramClient
{
    public async Task SendMessageAsync(long chatId, string text, ParseMode parseMode = ParseMode.None,
        InlineKeyboardMarkup? replyMarkup = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.SendMessage(chatId, text, parseMode: parseMode, replyMarkup: replyMarkup,
                cancellationToken: cancellationToken);
        }
        catch (ApiRequestException exception)
        {
            var failure = exception.ErrorCode switch
            {
                429 => TelegramDeliveryFailure.RejectedTransient,
                403 when exception.Message.Contains("bot was blocked by the user", StringComparison.OrdinalIgnoreCase)
                    => TelegramDeliveryFailure.BotBlocked,
                >= 400 and < 500 => TelegramDeliveryFailure.RejectedPermanent,
                // Навіть HTTP 5xx не доводить, що повідомлення не було прийнято.
                _ => TelegramDeliveryFailure.Ambiguous
            };
            throw new TelegramDeliveryException(failure, exception.Parameters?.RetryAfter);
        }
        catch (Exception exception) when (exception is RequestException or HttpRequestException or OperationCanceledException)
        {
            throw new TelegramDeliveryException(TelegramDeliveryFailure.Ambiguous);
        }
    }

    public Task ConfigureWebhookAsync(string url, string secretToken, IReadOnlyCollection<UpdateType> allowedUpdates,
        CancellationToken cancellationToken) => client.SetWebhook(url, secretToken: secretToken,
            allowedUpdates: allowedUpdates, cancellationToken: cancellationToken);
}
