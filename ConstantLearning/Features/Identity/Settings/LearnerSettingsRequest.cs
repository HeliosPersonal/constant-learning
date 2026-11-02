using System.Text;
using System.Text.Json;
using ConstantLearning.Features.Identity.Learners;

namespace ConstantLearning.Features.Identity.Settings;

internal sealed record LearnerSettingsRequest(short? DailyNewWordsLimit, string? TimeZone,
    bool HasReminder, ReminderScheduleValue? Reminder)
{
    public static IdentityError? Read(JsonElement root, out LearnerSettingsRequest? request)
    {
        request = null;
        if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Any()
            || root.EnumerateObject().Any(property => property.Name is not ("daily_new_words_limit" or "time_zone" or "reminder"))
            || root.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            return InvalidRequest();
        }

        short? dailyLimit = null;
        if (root.TryGetProperty("daily_new_words_limit", out var limit))
        {
            if (limit.ValueKind != JsonValueKind.Number || !limit.TryGetDecimal(out var number) || number is not (3 or 5 or 10))
            {
                return new IdentityError("settings.invalid_limit", "Доступні лише 3, 5 або 10 нових слів на день.");
            }

            dailyLimit = (short)number;
        }

        string? timeZone = null;
        if (root.TryGetProperty("time_zone", out var zone))
        {
            if (zone.ValueKind != JsonValueKind.String || zone.GetString()!.EnumerateRunes().Count() > 64)
            {
                return InvalidTimeZone();
            }

            timeZone = zone.GetString();
        }

        var hasReminder = root.TryGetProperty("reminder", out var value);
        ReminderScheduleValue? reminder = null;
        if (hasReminder && value.ValueKind != JsonValueKind.Null && ReminderScheduleCodec.Read(value, out reminder) is { } error)
        {
            return error.Code == "settings.reminder_days_required"
                ? new IdentityError(error.Code, "Виберіть хоча б один день тижня.")
                : error;
        }

        request = new LearnerSettingsRequest(dailyLimit, timeZone, hasReminder, reminder);
        return null;
    }

    public static IdentityError InvalidRequest() => new("settings.invalid_request", "Некоректні налаштування.");
    public static IdentityError InvalidTimeZone() => new("settings.invalid_time_zone", "Невідомий часовий пояс.");
}
