using System.Text.Json.Serialization;

namespace ConstantLearning.Features.Identity.Languages;

/// <summary>
/// Тіло <c>startLearnerLanguage</c>: код мови, яку learner починає вести. Нульове/невідоме значення
/// опрацьовується як <c>language.unknown</c> (контракт не визначає окремого 400 для цієї операції).
/// </summary>
public sealed record StartLanguageRequest(
    [property: JsonPropertyName("target_language")] string? TargetLanguage);

/// <summary>
/// Тіло <c>setActiveLanguage</c>: код мови, яку learner робить активною. Невідомий чи невивчений код
/// дає <c>language.not_studying</c> (existence-hiding — контракт не розкриває, чи мова взагалі існує).
/// </summary>
public sealed record SetActiveLanguageRequest(
    [property: JsonPropertyName("target_language")] string? TargetLanguage);
