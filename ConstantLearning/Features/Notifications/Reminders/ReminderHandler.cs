using ConstantLearning.Configuration;
using ConstantLearning.Features.Identity.NotificationAudience;
using ConstantLearning.Features.Identity.ReminderAudience;
using ConstantLearning.Features.Learning.Notifications;
using ConstantLearning.Features.Notifications.Data;
using ConstantLearning.Features.Notifications.Dispatch;
using Microsoft.Extensions.Options;
using NodaTime;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConstantLearning.Features.Notifications.Reminders;

public sealed class ReminderHandler(INotificationAudienceReader audience, IReminderAudienceReader reminders,
    IReminderLearningReader learning, IClock clock, IOptions<TelegramOptions> options) : INotificationHandler
{
    public string Kind => "reminder";

    public async Task<NotificationPreparation> PrepareAsync(NotificationDispatch dispatch, CancellationToken cancellationToken)
    {
        if (dispatch.LearnerId is not { } learnerId || await audience.ReadLearnerAsync(learnerId, cancellationToken) is not { } recipient
            || recipient.TelegramUserId != dispatch.RecipientTelegramUserId)
            return new NotificationPreparation.Skip("reminder_recipient_unavailable");
        if (recipient.LocalDate != dispatch.LocalDate) return new NotificationPreparation.Skip("reminder_day_changed");
        var preferences = await reminders.ReadAsync(learnerId, cancellationToken);
        if (preferences is null) return new NotificationPreparation.Skip("reminder_disabled");
        var due = ReminderSchedule.Due(recipient, preferences);
        if (due is null) return new NotificationPreparation.Skip("reminder_not_scheduled_today");
        var now = clock.GetCurrentInstant();
        if (now >= recipient.DayEndsAt || now > due + Duration.FromHours(2))
            return new NotificationPreparation.Skip("reminder_window_expired");
        if (due > now) return dispatch.Attempts == 0 ? new NotificationPreparation.Defer(due.Value)
            : new NotificationPreparation.Skip("reminder_timezone_changed_after_attempt");
        var state = await learning.ReadAsync(learnerId, recipient.LocalDate, preferences.DailyNewWordsLimit,
            preferences.NewWordsAvailableFrom, cancellationToken);
        if (state.CompletedToday) return new NotificationPreparation.Skip("reminder_session_completed");
        if (!state.HasWork) return new NotificationPreparation.Skip("reminder_no_words");
        if (state.LastAnsweredAt is { } answer && answer + Duration.FromMinutes(30) > now)
        {
            var afterPause = answer + Duration.FromMinutes(30);
            var deadline = Instant.Min(due.Value + Duration.FromHours(2), recipient.DayEndsAt);
            var next = Instant.Min(afterPause, deadline);
            // Межа двох годин завершує відкладення навіть за подальшої активності.
            // Кінець доби є виключною межею: на наступному тику нова доба відхилить старий dispatch.
            if (next > now) return new NotificationPreparation.Defer(next);
        }
        if (!Uri.TryCreate(options.Value.MiniAppUrl, UriKind.Absolute, out var app) || app.Scheme != "https"
            || app.UserInfo.Length != 0) return new NotificationPreparation.Skip("mini_app_url_not_configured");
        var resume = state.LastAnsweredAt is not null;
        return new NotificationPreparation.Deliver(new NotificationMessage(
            resume ? "Час продовжити заняття з польської." : "Час для щоденного заняття з польської.",
            ReplyMarkup: new InlineKeyboardMarkup(InlineKeyboardButton.WithWebApp(
                resume ? "Продовжити заняття" : "Відкрити заняття", new WebAppInfo { Url = app.AbsoluteUri }))));
    }
}
