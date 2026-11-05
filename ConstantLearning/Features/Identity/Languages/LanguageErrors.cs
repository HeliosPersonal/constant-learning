using System.Text.Json.Serialization;

namespace ConstantLearning.Features.Identity.Languages;

/// <summary>
/// Конверт помилки для поверхні керування мовами — ті самі поля <c>{code, message}</c>, що в
/// контракті (<see href="../../../../docs/features/multi-target-languages/contracts/openapi.yaml"/>).
/// </summary>
public sealed record LanguageError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

/// <summary>
/// Єдине визначення кодів помилок фічі multi-target-languages: один код = один рядок, що дослівно
/// збігається з контрактом (openapi-rules «1 код = 1 визначення»). Коди споживають handler-и старту
/// (T12) і перемикання (T13); реєстр заводиться тут (T11), щоб усі повертали той самий текст.
/// </summary>
public static class LanguageErrors
{
    /// <summary>Невідомий код мови (немає в каталозі). HTTP 404.</summary>
    public static LanguageError Unknown() => new("language.unknown", "Такої мови немає.");

    /// <summary>Мова вимкнена або ще без схваленого контенту — старт неможливий. HTTP 409.</summary>
    public static LanguageError NotAvailable() => new("language.not_available", "Ця мова поки недоступна.");

    /// <summary>Мову не можна зробити активною, бо learner її ще не вивчає. HTTP 409.</summary>
    public static LanguageError NotStudying() => new("language.not_studying", "Спершу почніть вивчати цю мову.");

    /// <summary>Конкурентний запис відхилено за оптимістичною конкурентністю — повторити. HTTP 409.</summary>
    public static LanguageError ConcurrentUpdate() => new("language.concurrent_update", "Стан змінився — повторіть спробу.");
}
