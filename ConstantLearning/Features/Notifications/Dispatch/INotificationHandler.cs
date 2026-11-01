using ConstantLearning.Features.Notifications.Data;
using NodaTime;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConstantLearning.Features.Notifications.Dispatch;

public interface INotificationHandler
{
    string Kind { get; }

    // Виконується всередині транзакції захоплення зі спільним scoped AppDbContext.
    // Перевіряє актуальні умови й резервує reports; не викликає Telegram і не фіксує транзакцію.
    // Повторна спроба використовує той самий dispatch.Id: враховуй також уже зарезервовані ним reports.
    Task<NotificationPreparation> PrepareAsync(NotificationDispatch dispatch, CancellationToken cancellationToken);
}

public sealed record NotificationMessage(string Text, ParseMode ParseMode = ParseMode.None,
    InlineKeyboardMarkup? ReplyMarkup = null);

public abstract record NotificationPreparation
{
    public sealed record Deliver(NotificationMessage Message) : NotificationPreparation;
    // Reason — стабільний код для журналу, без тексту повідомлення чи персональних даних.
    public sealed record Skip(string Reason) : NotificationPreparation;
    public sealed record Defer(Instant DueAt) : NotificationPreparation;
}
