using System.Text.Json.Serialization;
using ConstantLearning.Features.Learning.Sessions.Extra;

namespace ConstantLearning.Features.Learning.Today;

public sealed record TodayResponse(
    [property: JsonPropertyName("learner_day")] string LearnerDay,
    [property: JsonPropertyName("due_review_count")] int DueReviewCount,
    [property: JsonPropertyName("recalled_words_count")] int RecalledWordsCount,
    [property: JsonPropertyName("completed_sessions_last_7_days")] int CompletedSessionsLast7Days,
    [property: JsonPropertyName("has_active_session")] bool HasActiveSession,
    [property: JsonPropertyName("course_state")] string CourseState,
    [property: JsonPropertyName("course_replenished_notice")] bool CourseReplenishedNotice,
    [property: JsonPropertyName("new_words_available_today")] bool NewWordsAvailableToday,
    [property: JsonPropertyName("extra_actions")] ExtraActionsResponse ExtraActions);

public sealed record ExtraActionAvailabilityResponse(
    [property: JsonPropertyName("available")] bool Available,
    [property: JsonPropertyName("reason")] string? Reason);

public sealed record ExtraActionsResponse(
    [property: JsonPropertyName("plan_done")] bool PlanDone,
    [property: JsonPropertyName("extra_new_word")] ExtraActionAvailabilityResponse ExtraNewWord,
    [property: JsonPropertyName("extra_practice")] ExtraActionAvailabilityResponse ExtraPractice)
{
    public static ExtraActionsResponse From(ExtraActionsView view) => new(view.PlanDone,
        new(view.ExtraNewWord.Available, view.ExtraNewWord.Reason),
        new(view.ExtraPractice.Available, view.ExtraPractice.Reason));
}
