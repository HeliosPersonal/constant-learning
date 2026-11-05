using System.Text.Json.Serialization;

namespace ConstantLearning.Features.Identity.Languages;

/// <summary>Елемент каталогу мов: запис <c>target_languages</c> плюс обчислений сервером <c>available</c>.</summary>
public sealed record TargetLanguageView(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("display_name_uk")] string DisplayNameUk,
    [property: JsonPropertyName("is_enabled")] bool IsEnabled,
    [property: JsonPropertyName("sort_order")] short SortOrder,
    [property: JsonPropertyName("available")] bool Available);

/// <summary>Повний каталог мов без пагінації (обмежений набір — ADR-0004).</summary>
public sealed record TargetLanguageCatalogResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<TargetLanguageView> Items);

/// <summary>Мова, яку learner вивчає: рядок <c>learner_language_states</c> з назвою мови й ознакою активності.</summary>
public sealed record LearnerLanguageView(
    [property: JsonPropertyName("target_language")] string TargetLanguage,
    [property: JsonPropertyName("display_name_uk")] string DisplayNameUk,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("started_at")] string StartedAt);

/// <summary>Мови learner-а та його активна мова (<c>null</c>, якщо жодної ще не почато).</summary>
public sealed record LearnerLanguagesResponse(
    [property: JsonPropertyName("active_target_language")] string? ActiveTargetLanguage,
    [property: JsonPropertyName("items")] IReadOnlyList<LearnerLanguageView> Items);

/// <summary>Активна мова learner-а після перемикання (<c>setActiveLanguage</c>).</summary>
public sealed record ActiveLanguageView(
    [property: JsonPropertyName("active_target_language")] string ActiveTargetLanguage);
