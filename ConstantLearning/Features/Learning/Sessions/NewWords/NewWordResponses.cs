using System.Text.Json.Serialization;
using ConstantLearning.Features.Learning.Sessions.Start;

namespace ConstantLearning.Features.Learning.Sessions.NewWords;

public sealed record SessionProgressResponse(
    [property: JsonPropertyName("next_item")] SessionItemResponse? NextItem,
    [property: JsonPropertyName("session_completed")] bool SessionCompleted);

public sealed record AlreadyKnownResponse(
    [property: JsonPropertyName("control_repetition_on")] string ControlRepetitionOn,
    [property: JsonPropertyName("replacement_added")] bool ReplacementAdded,
    [property: JsonPropertyName("next_item")] SessionItemResponse? NextItem,
    [property: JsonPropertyName("session_completed")] bool SessionCompleted);

public sealed record NewWordActionResult(int StatusCode, object? Response, SessionError? Error);
