using System.Text.Json;
using ConstantLearning.Features.Learning.Data;
using NodaTime;
using NodaTime.Text;

namespace ConstantLearning.Features.Learning.Disputes;

internal static class DisputeWordState
{
    public static void Restore(LearnerWord word, string snapshot)
    {
        using var document = JsonDocument.Parse(snapshot);
        var state = document.RootElement;
        if (state.GetProperty("schema_version").GetInt32() != 1)
            throw new InvalidOperationException("Невідома версія знімка стану слова.");

        word.Status = state.GetProperty("status").GetString()!;
        word.FirstShownDay = Date(state.GetProperty("first_shown_day"));
        word.LastReviewDay = Date(state.GetProperty("last_review_day"));
        word.DueOn = Date(state.GetProperty("due_on"));
        var interval = state.GetProperty("interval_days");
        word.IntervalDays = interval.ValueKind == JsonValueKind.Null ? null : interval.GetInt32();
        word.ConsecutiveScheduledCorrect = state.GetProperty("consecutive_scheduled_correct").GetInt16();
        word.ControlPending = state.GetProperty("control_pending").GetBoolean();
        word.Lapses = state.GetProperty("lapses").GetInt32();
        word.ModelState = state.GetProperty("model_state").GetRawText();
        word.SchedulerVersion = state.GetProperty("scheduler_version").GetString()!;
    }

    private static LocalDate? Date(JsonElement value) => value.ValueKind == JsonValueKind.Null
        ? null : LocalDatePattern.Iso.Parse(value.GetString()!).Value;
}
