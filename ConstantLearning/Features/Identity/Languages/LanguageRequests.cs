using System.Text.Json.Serialization;

namespace ConstantLearning.Features.Identity.Languages;

/// <summary>
/// Тіло <c>startLearnerLanguage</c>: код мови, яку learner починає вести. Нульове/невідоме значення
/// опрацьовується як <c>language.unknown</c> (контракт не визначає окремого 400 для цієї операції).
/// </summary>
public sealed record StartLanguageRequest(
    [property: JsonPropertyName("target_language")] string? TargetLanguage);
