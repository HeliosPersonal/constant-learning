using ConstantLearning.Configuration;
using ConstantLearning.Features.Identity.Messaging;
using ConstantLearning.Platform.Telegram;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConstantLearning.Features.Bot;

public sealed class BotUpdateHandler(ITelegramClient telegram, IMessagingPermissionService messaging,
    IOptions<TelegramOptions> options)
{
    public async Task HandleAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.MyChatMember is { Chat.Type: ChatType.Private } membership)
        {
            var canMessage = membership.NewChatMember.Status switch
            {
                ChatMemberStatus.Kicked or ChatMemberStatus.Left => false,
                ChatMemberStatus.Member => true,
                _ => (bool?)null
            };
            if (canMessage is { } allowed)
            {
                // chat.id — власник приватного чату; from може бути іншим ініціатором події.
                await messaging.SetCanMessageAsync(membership.Chat.Id, allowed, cancellationToken);
            }
            return;
        }

        if (update.Message is not { Chat.Type: ChatType.Private, From.IsBot: false, Text: { } text } message)
        {
            return;
        }

        var command = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.Equals(command, "/start", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var miniAppUrl = options.Value.MiniAppUrl;
        if (!Uri.TryCreate(miniAppUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Потрібна HTTPS-адреса Telegram:MiniAppUrl.");
        }

        await messaging.SetCanMessageAsync(message.Chat.Id, true, cancellationToken);
        await telegram.SendMessageAsync(message.Chat.Id, "Відкрийте тренер, щоб почати або продовжити заняття.",
            replyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithWebApp("Відкрити тренер",
                new WebAppInfo { Url = miniAppUrl! })), cancellationToken: cancellationToken);
    }
}
