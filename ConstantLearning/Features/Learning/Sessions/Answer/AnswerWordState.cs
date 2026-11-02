using System.Text.Json;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Domain.Scheduling;
using NodaTime;
using NodaTime.Text;

namespace ConstantLearning.Features.Learning.Sessions.Answer;

/// <summary>Адаптер проєкції та повного знімка для відтворення й майбутнього оскарження відповіді.</summary>
public static class AnswerWordState
{
    public static ReviewState Read(LearnerWord word)
    {
        using var model = JsonDocument.Parse(word.ModelState);
        var rate = model.RootElement.TryGetProperty("forgetting_rate", out var value) ? value.GetDouble() : 0.3;
        LocalDate? attempted = model.RootElement.TryGetProperty("last_scheduled_attempt_day", out var day)
            && day.ValueKind != JsonValueKind.Null ? LocalDatePattern.Iso.Parse(day.GetString()!).Value : null;
        return new ReviewState(word.FirstShownDay ?? throw new InvalidOperationException("Вправа потребує підтвердженого показу слова."),
            word.LastReviewDay, attempted, word.DueOn ?? throw new InvalidOperationException("Слово не має дати повторення."),
            word.IntervalDays ?? throw new InvalidOperationException("Слово не має інтервалу повторення."),
            word.ConsecutiveScheduledCorrect, word.ControlPending, word.Lapses, rate, word.SchedulerVersion);
    }

    public static void Apply(LearnerWord word, ReviewState state)
    {
        word.Status = state.Status == ReviewWordStatus.Recalled ? "recalled" : "review";
        word.FirstShownDay = state.FirstShownDay;
        word.LastReviewDay = state.LastReviewDay;
        word.DueOn = state.DueOn;
        word.IntervalDays = state.IntervalDays;
        word.ConsecutiveScheduledCorrect = checked((short)state.ConsecutiveScheduledCorrect);
        word.ControlPending = state.ControlPending;
        word.Lapses = state.Lapses;
        word.SchedulerVersion = state.SchedulerVersion;
        word.ModelState = JsonSerializer.Serialize(new
        {
            forgetting_rate = state.ForgettingRate,
            last_scheduled_attempt_day = Format(state.LastScheduledAttemptDay)
        });
    }

    public static string Snapshot(LearnerWord word) => JsonSerializer.Serialize(new
    {
        schema_version = 1,
        status = word.Status,
        first_shown_day = Format(word.FirstShownDay),
        last_review_day = Format(word.LastReviewDay),
        due_on = Format(word.DueOn),
        interval_days = word.IntervalDays,
        consecutive_scheduled_correct = word.ConsecutiveScheduledCorrect,
        control_pending = word.ControlPending,
        lapses = word.Lapses,
        model_state = JsonSerializer.Deserialize<JsonElement>(word.ModelState),
        scheduler_version = word.SchedulerVersion
    });

    private static string? Format(LocalDate? day) => day is { } value ? LocalDatePattern.Iso.Format(value) : null;
}
