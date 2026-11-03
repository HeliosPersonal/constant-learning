using System.Text.Json.Serialization;
using ConstantLearning.Features.Content.Search.Api;

namespace ConstantLearning.Features.Learning.Stats;

/// <summary>Конверт помилки екрана «Мої слова» (learned-words-stats); форма {code, message}.</summary>
public sealed record StatsError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

public sealed record MistakeBreakdownResponse(
    [property: JsonPropertyName("typo")] int Typo,
    [property: JsonPropertyName("diacritic")] int Diacritic,
    [property: JsonPropertyName("dont_know")] int DontKnow);

public sealed record LearnedWordStatResponse(
    [property: JsonPropertyName("entry_id")] Guid EntryId,
    [property: JsonPropertyName("lemma_pl")] string LemmaPl,
    [property: JsonPropertyName("transcription_uk")] string? TranscriptionUk,
    [property: JsonPropertyName("translation_uk")] string TranslationUk,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("shown_count")] int ShownCount,
    [property: JsonPropertyName("correct_count")] int CorrectCount,
    [property: JsonPropertyName("incorrect_count")] int IncorrectCount,
    [property: JsonPropertyName("streak")] int Streak,
    [property: JsonPropertyName("forgotten_count")] int ForgottenCount,
    [property: JsonPropertyName("first_shown_day")] string? FirstShownDay,
    [property: JsonPropertyName("last_review_day")] string? LastReviewDay,
    [property: JsonPropertyName("due_on")] string? DueOn,
    [property: JsonPropertyName("is_weak")] bool IsWeak);

public sealed record LearnedWordCardResponse(
    [property: JsonPropertyName("entry_id")] Guid EntryId,
    [property: JsonPropertyName("lemma_pl")] string LemmaPl,
    [property: JsonPropertyName("transcription_uk")] string? TranscriptionUk,
    [property: JsonPropertyName("translation_uk")] string TranslationUk,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("shown_count")] int ShownCount,
    [property: JsonPropertyName("correct_count")] int CorrectCount,
    [property: JsonPropertyName("incorrect_count")] int IncorrectCount,
    [property: JsonPropertyName("streak")] int Streak,
    [property: JsonPropertyName("forgotten_count")] int ForgottenCount,
    [property: JsonPropertyName("first_shown_day")] string? FirstShownDay,
    [property: JsonPropertyName("last_review_day")] string? LastReviewDay,
    [property: JsonPropertyName("due_on")] string? DueOn,
    [property: JsonPropertyName("is_weak")] bool IsWeak,
    [property: JsonPropertyName("translations_uk")] IReadOnlyList<WordTranslation> TranslationsUk,
    [property: JsonPropertyName("mistake_breakdown")] MistakeBreakdownResponse MistakeBreakdown);

public sealed record LearnedWordsPageResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<LearnedWordStatResponse> Items,
    [property: JsonPropertyName("has_next")] bool HasNext,
    [property: JsonPropertyName("has_prev")] bool HasPrev,
    [property: JsonPropertyName("next_cursor")] Guid? NextCursor);

public sealed record LearnedWordsSummaryResponse(
    [property: JsonPropertyName("seen_count")] int SeenCount,
    [property: JsonPropertyName("recalled_count")] int RecalledCount,
    [property: JsonPropertyName("review_count")] int ReviewCount,
    [property: JsonPropertyName("weak_count")] int WeakCount);
