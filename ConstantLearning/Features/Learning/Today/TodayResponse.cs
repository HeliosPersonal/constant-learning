using System.Text.Json.Serialization;

namespace ConstantLearning.Features.Learning.Today;

public sealed record TodayResponse(
    [property: JsonPropertyName("learner_day")] string LearnerDay,
    [property: JsonPropertyName("due_review_count")] int DueReviewCount,
    [property: JsonPropertyName("recalled_words_count")] int RecalledWordsCount,
    [property: JsonPropertyName("completed_sessions_last_7_days")] int CompletedSessionsLast7Days,
    [property: JsonPropertyName("has_active_session")] bool HasActiveSession,
    [property: JsonPropertyName("course_state")] string CourseState,
    [property: JsonPropertyName("course_replenished_notice")] bool CourseReplenishedNotice);
