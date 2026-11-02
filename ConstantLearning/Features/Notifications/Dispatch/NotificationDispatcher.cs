using ConstantLearning.Features.Content.Notifications;
using ConstantLearning.Features.Identity.Messaging;
using ConstantLearning.Features.Notifications.Data;
using ConstantLearning.Platform.Persistence;
using ConstantLearning.Platform.Telegram;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace ConstantLearning.Features.Notifications.Dispatch;

public sealed class NotificationDispatcher(
    AppDbContext db,
    IClock clock,
    ITelegramClient telegram,
    IMessagingPermissionService messagingPermission,
    IReportSummaryDispatchService reportSummaries,
    IEnumerable<INotificationHandler> handlers,
    ILogger<NotificationDispatcher> logger)
{
    public const int BatchSize = 1024;
    public static readonly Duration MaximumLateness = Duration.FromMinutes(60);
    public static readonly Duration AbandonedClaimAge = Duration.FromMinutes(2);
    private readonly IReadOnlyDictionary<string, INotificationHandler> _handlers =
        handlers.ToDictionary(handler => handler.Kind, StringComparer.Ordinal);

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        for (var count = 0; count < BatchSize; count++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var claim = await ClaimAsync(cancellationToken);
            if (claim is null)
            {
                return;
            }

            if (claim.Message is null)
            {
                continue;
            }

            TelegramDeliveryException? failure = null;
            try
            {
                // Стан sending уже зафіксовано: жодна репліка не повторює невизначену доставку.
                await telegram.SendMessageAsync(claim.Dispatch.RecipientTelegramUserId, claim.Message.Text,
                    claim.Message.ParseMode, claim.Message.ReplyMarkup, cancellationToken);
            }
            catch (TelegramDeliveryException exception)
            {
                failure = exception;
            }
            catch (Exception)
            {
                // Невідомий збій теж може трапитися після прийняття повідомлення Telegram.
                // Сирий виняток не журналюємо: транспорт може додати URL із токеном бота.
                failure = new TelegramDeliveryException(TelegramDeliveryFailure.Ambiguous);
            }

            // Якщо зупинка скасовує запис результату, залишений sending відновиться лише як failed.
            await CompleteAsync(claim.Dispatch, failure, cancellationToken);
        }
    }

    private async Task<Claim?> ClaimAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();
        var abandonedBefore = now - AbandonedClaimAge;
        var missedBefore = now - MaximumLateness;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // due_at залишається початковим часом доставки для оцінки запізнення та NFR.
        // reason зберігає лише стабільний код і затримку повтору, ніколи відповідь Telegram.
        var candidates = await db.NotificationDispatches.FromSqlInterpolated($"""
            SELECT dispatch.*, dispatch.xmin FROM notification_dispatches AS dispatch
            WHERE (status = 'sending' AND (claimed_at IS NULL OR claimed_at <= {abandonedBefore}))
               OR (status = 'pending' AND due_at <= {now} AND
                   (due_at < {missedBefore} OR claimed_at IS NULL OR
                    claimed_at + CASE
                        WHEN reason ~ '^telegram_rejected_transient:[0-9]+$'
                        THEN split_part(reason, ':', 2)::double precision * interval '1 second'
                        ELSE interval '0 seconds'
                    END <= {now}))
            ORDER BY due_at, id
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        var dispatch = candidates.SingleOrDefault();
        if (dispatch is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        NotificationMessage? message = null;
        if (dispatch.Status == "sending")
        {
            SetTerminal(dispatch, "failed", "delivery_unknown_after_restart");
        }
        else if (dispatch.DueAt < missedBefore)
        {
            SetTerminal(dispatch, "missed", "delivery_window_expired");
        }
        else if (dispatch.Attempts >= 3)
        {
            SetTerminal(dispatch, "failed", "attempts_exhausted");
        }
        else if (await messagingPermission.GetCanMessageAsync(dispatch.RecipientTelegramUserId, cancellationToken) == false)
        {
            SetTerminal(dispatch, "skipped", "messaging_disabled");
        }
        else if (!_handlers.TryGetValue(dispatch.Kind, out var handler))
        {
            SetTerminal(dispatch, "skipped", "kind_not_registered");
        }
        else
        {
            var preparation = await handler.PrepareAsync(dispatch, cancellationToken);
            switch (preparation)
            {
                case NotificationPreparation.Deliver delivery when !string.IsNullOrWhiteSpace(delivery.Message.Text):
                    dispatch.Status = "sending";
                    dispatch.ClaimedAt = clock.GetCurrentInstant();
                    dispatch.Attempts++;
                    dispatch.Reason = null;
                    message = delivery.Message;
                    break;
                case NotificationPreparation.Skip skip:
                    SetTerminal(dispatch, "skipped", skip.Reason);
                    break;
                case NotificationPreparation.Defer defer when defer.DueAt > now:
                    dispatch.DueAt = defer.DueAt;
                    dispatch.Reason = "precondition_deferred";
                    break;
                default:
                    SetTerminal(dispatch, "failed", "invalid_preparation");
                    break;
            }
        }

        await ReleaseUnsentSummaryAsync(dispatch, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogTransition(dispatch);
        // Контекст не накопичує знімки між транзакціями й повторними спробами.
        db.ChangeTracker.Clear();
        return new Claim(dispatch, message);
    }

    private async Task CompleteAsync(NotificationDispatch dispatch, TelegramDeliveryException? failure,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.NotificationDispatches.Attach(dispatch);
        if (failure is null)
        {
            dispatch.Status = "sent";
            dispatch.SentAt = clock.GetCurrentInstant();
            dispatch.Reason = null;
        }
        else if (failure.Failure == TelegramDeliveryFailure.RejectedTransient && dispatch.Attempts < 3)
        {
            dispatch.Status = "pending";
            var delay = Math.Max(30 * (1 << (dispatch.Attempts - 1)), failure.RetryAfterSeconds ?? 0);
            // Затримка відраховується після відповіді, щоб повністю витримати retry_after.
            dispatch.ClaimedAt = clock.GetCurrentInstant();
            dispatch.Reason = $"telegram_rejected_transient:{delay}";
        }
        else
        {
            var reason = failure.Failure switch
            {
                TelegramDeliveryFailure.BotBlocked => "telegram_bot_blocked",
                TelegramDeliveryFailure.RejectedTransient => "attempts_exhausted",
                TelegramDeliveryFailure.RejectedPermanent => "telegram_rejected_permanent",
                _ => "delivery_unknown"
            };
            SetTerminal(dispatch, "failed", reason);
            if (failure.Failure == TelegramDeliveryFailure.BotBlocked)
            {
                await messagingPermission.SetCanMessageAsync(dispatch.RecipientTelegramUserId, false, cancellationToken);
            }
        }

        await ReleaseUnsentSummaryAsync(dispatch, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogTransition(dispatch);
        db.ChangeTracker.Clear();
    }

    private Task ReleaseUnsentSummaryAsync(NotificationDispatch dispatch, CancellationToken cancellationToken) =>
        dispatch.Kind == "report_outcomes" && dispatch.Status is "failed" or "missed" or "skipped"
            ? reportSummaries.ReleaseAsync(dispatch.Id, cancellationToken)
            : Task.CompletedTask;

    private static void SetTerminal(NotificationDispatch dispatch, string status, string reason)
    {
        dispatch.Status = status;
        dispatch.Reason = reason.Length <= 200 ? reason : reason[..200];
    }

    private void LogTransition(NotificationDispatch dispatch) =>
        logger.LogInformation("Відправлення {DispatchId}: {Kind}, {Status}, спроб {Attempts}, причина {Reason}",
            dispatch.Id, dispatch.Kind, dispatch.Status, dispatch.Attempts, dispatch.Reason);

    private sealed record Claim(NotificationDispatch Dispatch, NotificationMessage? Message);
}
