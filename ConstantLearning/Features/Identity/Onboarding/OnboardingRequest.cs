using System.Text;
using System.Text.Json;
using ConstantLearning.Features.Identity.Learners;

namespace ConstantLearning.Features.Identity.Onboarding;

internal sealed record OnboardingRequest(short DailyNewWordsLimit, string? TimeZone, ReminderScheduleValue? Reminder)
{
    public static IdentityError? Read(JsonElement root, out OnboardingRequest? request)
    {
        request = null;
        if (root.ValueKind != JsonValueKind.Object
            || root.EnumerateObject().Any(property => property.Name is not ("daily_new_words_limit" or "time_zone" or "reminder"))
            || root.EnumerateObject().GroupBy(property => property.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            return InvalidRequest();
        }

        if (!root.TryGetProperty("daily_new_words_limit", out var limit)
            || limit.ValueKind != JsonValueKind.Number || !limit.TryGetDecimal(out var number)
            || number is not (3 or 5 or 10))
        {
            return new IdentityError("settings.invalid_limit", "Доступні лише 3, 5 або 10 нових слів на день.");
        }

        string? timeZone = null;
        if (root.TryGetProperty("time_zone", out var zone) && zone.ValueKind != JsonValueKind.Null)
        {
            if (zone.ValueKind != JsonValueKind.String || zone.GetString()!.EnumerateRunes().Count() > 64)
            {
                return InvalidRequest();
            }

            timeZone = zone.GetString();
        }

        ReminderScheduleValue? reminder = null;
        if (root.TryGetProperty("reminder", out var value) && value.ValueKind != JsonValueKind.Null
            && ReminderScheduleCodec.Read(value, out reminder) is { } error)
        {
            return error;
        }

        request = new OnboardingRequest((short)number, timeZone, reminder);
        return null;
    }

    public static IdentityError InvalidRequest() => new("settings.invalid_request", "Некоректні налаштування онбордингу.");
}
