using System.Text.Json.Serialization;
using ConstantLearning.Features.Identity.Data;
using NodaTime.Text;

namespace ConstantLearning.Features.Identity.Learners;

public sealed record LearnerResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("time_zone")] string TimeZone,
    [property: JsonPropertyName("time_zone_confirmed")] bool TimeZoneConfirmed,
    [property: JsonPropertyName("daily_new_words_limit")] short DailyNewWordsLimit,
    [property: JsonPropertyName("pending_daily_new_words_limit")] short? PendingDailyNewWordsLimit,
    [property: JsonPropertyName("reminder")] ReminderResponse? Reminder,
    [property: JsonPropertyName("onboarded_at")] string? OnboardedAt)
{
    public static LearnerResponse From(Learner learner) => new(
        learner.Id, learner.FirstName, learner.TimeZone, learner.TimeZoneConfirmed,
        learner.DailyNewWordsLimit, learner.PendingDailyNewWordsLimit,
        learner.ReminderSchedule is { } reminder
            ? ReminderScheduleCodec.ToResponse(reminder.LocalTime, reminder.DaysOfWeek)
            : null,
        learner.OnboardedAt is { } onboardedAt ? InstantPattern.ExtendedIso.Format(onboardedAt) : null);
}

public sealed record ReminderResponse(
    [property: JsonPropertyName("local_time")] string LocalTime,
    [property: JsonPropertyName("days_of_week")] IReadOnlyList<string> DaysOfWeek);

public sealed record IdentityError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message)
{
    /// <summary>Онбординг ще не завершено — єдине визначення коду <c>learner.not_onboarded</c> (рядок = контракт).</summary>
    public static IdentityError NotOnboarded() => new("learner.not_onboarded", "Спершу налаштуйте тренер.");
}
