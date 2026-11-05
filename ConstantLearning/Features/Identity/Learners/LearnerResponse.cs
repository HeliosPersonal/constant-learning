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
    [property: JsonPropertyName("onboarded_at")] string? OnboardedAt,
    [property: JsonPropertyName("active_target_language")] string? ActiveTargetLanguage)
{
    /// <summary>
    /// Налаштування доби й нагадування віддаються для активної мови (ADR-0005). Якщо передано стан мови, ліміт
    /// береться з нього (а не з дзеркала на learner, яке може відставати після перемикання). Колекція
    /// <c>ReminderSchedules</c> має бути завантажена викликувачем (Include), інакше нагадування буде відсутнім.
    /// </summary>
    public static LearnerResponse From(Learner learner, LearnerLanguageState? state = null)
    {
        // Інтеримний код до повного переходу на активну мову (як у LearnerDayStateService).
        var language = learner.ActiveTargetLanguage ?? "pl";
        var reminder = learner.ReminderSchedules.SingleOrDefault(schedule => schedule.TargetLanguage == language);
        return new(
            learner.Id, learner.FirstName, learner.TimeZone, learner.TimeZoneConfirmed,
            state?.DailyNewWordsLimit ?? learner.DailyNewWordsLimit,
            state is null ? learner.PendingDailyNewWordsLimit : state.PendingDailyNewWordsLimit,
            reminder is null ? null : ReminderScheduleCodec.ToResponse(reminder.LocalTime, reminder.DaysOfWeek),
            learner.OnboardedAt is { } onboardedAt ? InstantPattern.ExtendedIso.Format(onboardedAt) : null,
            learner.ActiveTargetLanguage);
    }
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
