namespace ConstantLearning.Platform.Telegram;

public enum TelegramDeliveryFailure
{
    RejectedTransient,
    RejectedPermanent,
    BotBlocked,
    Ambiguous
}

// Виняток навмисно не містить відповіді Telegram, URL запиту або внутрішнього винятку з токеном.
public sealed class TelegramDeliveryException(TelegramDeliveryFailure failure, int? retryAfterSeconds = null)
    : Exception("Не вдалося підтвердити доставку повідомлення Telegram.")
{
    public TelegramDeliveryFailure Failure { get; } = failure;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}
