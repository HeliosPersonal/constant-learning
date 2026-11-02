using System.Text.Json.Serialization;
using ConstantLearning.Features.Content.Course;
using ConstantLearning.Features.Learning.Data;
using ConstantLearning.Features.Learning.Sessions.Results;
using NodaTime.Text;

namespace ConstantLearning.Features.Learning.Sessions.Start;

public sealed record SessionError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

public sealed record NewWordCardResponse(
    [property: JsonPropertyName("lemma_pl")] string LemmaPl,
    [property: JsonPropertyName("translations_uk")] IReadOnlyList<string> TranslationsUk,
    [property: JsonPropertyName("pronunciation_uk")] string PronunciationUk,
    [property: JsonPropertyName("example_pl")] string ExamplePl,
    [property: JsonPropertyName("example_uk")] string ExampleUk,
    [property: JsonPropertyName("gender")] string? Gender);

public sealed record SessionItemResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("prompt_uk")] string PromptUk,
    [property: JsonPropertyName("context_uk")] string? ContextUk,
    [property: JsonPropertyName("card")] NewWordCardResponse? Card)
{
    public static SessionItemResponse From(SessionItem item, PublishedCourseWord word) => new(item.Id,
        item.Position, item.Kind, word.PromptUk, word.ShowContextBefore ? word.ExampleUk : null,
        item.Kind == "new_word_card" ? new NewWordCardResponse(word.LemmaPl, word.TranslationsUk,
            word.PronunciationUk, word.ExamplePl, word.ExampleUk, word.Gender) : null);
}

public sealed record SessionResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("learner_day")] string LearnerDay,
    [property: JsonPropertyName("ends_at")] string EndsAt,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("total_items")] int TotalItems,
    [property: JsonPropertyName("completed_items")] int CompletedItems,
    [property: JsonPropertyName("notices")] IReadOnlyList<string> Notices,
    [property: JsonPropertyName("current_item")] SessionItemResponse? CurrentItem,
    [property: JsonPropertyName("last_result")] LastResultResponse? LastResult)
{
    public static SessionResponse From(LearningSession session, IReadOnlyList<SessionItem> items,
        IReadOnlyList<string> notices, SessionItemResponse? current, LastResultResponse? result = null) => new(session.Id,
        LocalDatePattern.Iso.Format(session.LearnerDay), InstantPattern.ExtendedIso.Format(session.EndsAt),
        session.Status, items.Count, items.Count(item => item.State is "answered" or "skipped"), notices, current, result);
}
