using ConstantLearning.Configuration;
using ConstantLearning.Features.Content.Notifications;
using ConstantLearning.Features.Identity.NotificationAudience;
using ConstantLearning.Features.Notifications.Data;
using ConstantLearning.Features.Notifications.Dispatch;
using ConstantLearning.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConstantLearning.Features.Notifications.Summaries;

public sealed class EditorDigestHandler(AppDbContext db, IReportNotificationReader reports,
    INotificationAudienceReader audience, IOptions<TelegramOptions> options) : INotificationHandler
{
    public string Kind => "editor_digest";

    public async Task<NotificationPreparation> PrepareAsync(NotificationDispatch dispatch, CancellationToken cancellationToken)
    {
        if (!audience.ReadEditorTelegramIds().Contains(dispatch.RecipientTelegramUserId))
            return new NotificationPreparation.Skip("editor_role_removed");
        var previousDue = await db.NotificationDispatches.AsNoTracking().Where(row => row.Kind == Kind
            && row.RecipientTelegramUserId == dispatch.RecipientTelegramUserId && row.Status == "sent" && row.DueAt < dispatch.DueAt)
            .MaxAsync(row => (NodaTime.Instant?)row.DueAt, cancellationToken);
        var counts = await reports.ReadEditorCountsAsync(previousDue, dispatch.DueAt, cancellationToken);
        if (counts.Unresolved == 0) return new NotificationPreparation.Skip("editor_queue_empty");
        if (!Uri.TryCreate(options.Value.MiniAppUrl, UriKind.Absolute, out var app) || app.Scheme != "https"
            || app.UserInfo.Length != 0) return new NotificationPreparation.Skip("mini_app_url_not_configured");
        var queue = new UriBuilder(app) { Path = "/editor", Query = "", Fragment = "" }.Uri.AbsoluteUri;
        return new NotificationPreparation.Deliver(new NotificationMessage(
            $"Черга редактора\nНових повідомлень: {counts.NewlyReceived}.\nБез рішення: {counts.Unresolved}.",
            ReplyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithWebApp("Відкрити чергу", new WebAppInfo { Url = queue }))));
    }
}
