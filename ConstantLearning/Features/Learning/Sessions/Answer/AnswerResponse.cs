using System.Text.Json.Serialization;
using ConstantLearning.Features.Learning.Sessions.Start;

namespace ConstantLearning.Features.Learning.Sessions.Answer;

public sealed record AnswerDiffResponse(
    [property: JsonPropertyName("op")] string Op,
    [property: JsonPropertyName("expected")] string Expected,
    [property: JsonPropertyName("actual")] string Actual);

public sealed record AnswerResponse(
    [property: JsonPropertyName("answer_event_id")] Guid AnswerEventId,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("correct_form")] string CorrectForm,
    [property: JsonPropertyName("diff")] IReadOnlyList<AnswerDiffResponse> Diff,
    [property: JsonPropertyName("pronunciation_uk")] string PronunciationUk,
    [property: JsonPropertyName("example_pl")] string ExamplePl,
    [property: JsonPropertyName("example_uk")] string ExampleUk,
    [property: JsonPropertyName("gender")] string? Gender,
    [property: JsonPropertyName("dispute_allowed")] bool DisputeAllowed,
    [property: JsonPropertyName("word_status")] string WordStatus,
    [property: JsonPropertyName("next_review_on")] string? NextReviewOn,
    [property: JsonPropertyName("next_item")] SessionItemResponse? NextItem,
    [property: JsonPropertyName("session_completed")] bool SessionCompleted);

public sealed record AnswerSessionResult(int StatusCode, AnswerResponse? Answer, SessionError? Error);
