using System.Text.Json.Serialization;

namespace ConstantLearning.Features.Content.Search.Api;

public sealed record DictionaryError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

public sealed record DictionarySearchItem(
    [property: JsonPropertyName("entry_id")] Guid EntryId,
    [property: JsonPropertyName("lemma_pl")] string LemmaPl,
    [property: JsonPropertyName("part_of_speech")] string PartOfSpeech,
    [property: JsonPropertyName("short_meaning_uk")] string ShortMeaningUk,
    [property: JsonPropertyName("matched_form")] string MatchedForm,
    [property: JsonPropertyName("word_status")] string WordStatus);

public sealed record DictionarySearchPage(
    [property: JsonPropertyName("items")] IReadOnlyList<DictionarySearchItem> Items,
    [property: JsonPropertyName("has_next")] bool HasNext,
    [property: JsonPropertyName("has_prev")] bool HasPrev,
    [property: JsonPropertyName("next_cursor")] Guid? NextCursor);

public sealed record WordTranslation(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("text_uk")] string TextUk);

public sealed record DictionaryEntryCard(
    [property: JsonPropertyName("entry_id")] Guid EntryId,
    [property: JsonPropertyName("lemma_pl")] string LemmaPl,
    [property: JsonPropertyName("part_of_speech")] string PartOfSpeech,
    [property: JsonPropertyName("gender")] string? Gender,
    [property: JsonPropertyName("translations_uk")] IReadOnlyList<WordTranslation> TranslationsUk,
    [property: JsonPropertyName("pronunciation_uk")] string PronunciationUk,
    [property: JsonPropertyName("example_pl")] string ExamplePl,
    [property: JsonPropertyName("example_uk")] string ExampleUk,
    [property: JsonPropertyName("word_status")] string WordStatus);
