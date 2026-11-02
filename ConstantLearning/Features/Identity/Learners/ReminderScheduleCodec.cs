using System.Text.Json;
using NodaTime;
using NodaTime.Text;

namespace ConstantLearning.Features.Identity.Learners;

internal sealed record ReminderScheduleValue(LocalTime LocalTime, short DaysOfWeek);

internal static class ReminderScheduleCodec
{
    private static readonly string[] Days = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"];
    private static readonly LocalTimePattern TimePattern = LocalTimePattern.CreateWithInvariantCulture("HH:mm");

    public static ReminderResponse ToResponse(LocalTime time, short days) => new(
        TimePattern.Format(time), Days.Where((_, index) => (days & (1 << index)) != 0).ToArray());

    public static IdentityError? Read(JsonElement root, out ReminderScheduleValue? reminder)
    {
        reminder = null;
        if (root.ValueKind != JsonValueKind.Object
            || root.EnumerateObject().Count() != 2
            || !root.TryGetProperty("local_time", out var time)
            || !root.TryGetProperty("days_of_week", out var days)
            || time.ValueKind != JsonValueKind.String || days.ValueKind != JsonValueKind.Array)
        {
            return InvalidReminder();
        }

        if (days.GetArrayLength() == 0)
        {
            return new IdentityError("settings.reminder_days_required",
                "Виберіть хоча б один день або пропустіть налаштування нагадувань.");
        }

        var parsedTime = TimePattern.Parse(time.GetString()!);
        if (!parsedTime.Success)
        {
            return InvalidReminder();
        }

        short mask = 0;
        foreach (var day in days.EnumerateArray())
        {
            var index = day.ValueKind == JsonValueKind.String ? Array.IndexOf(Days, day.GetString()) : -1;
            if (index < 0 || (mask & (1 << index)) != 0)
            {
                return InvalidReminder();
            }

            mask |= (short)(1 << index);
        }

        reminder = new ReminderScheduleValue(parsedTime.Value, mask);
        return null;
    }

    private static IdentityError InvalidReminder() => new("settings.invalid_reminder", "Некоректні час або дні нагадування.");
}
