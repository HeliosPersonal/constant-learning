using NodaTime;

namespace ConstantLearning.Features.Identity.Data;

/// <summary>
/// Запис каталогу цільових мов тренера. Набір мов — дані/конфіг, а не enum у коді
/// (ADR-0004): нова мова = рядок у цій таблиці. Усі таблиці прогресу й контенту
/// посилаються на <see cref="Code"/> як на природний ключ-дискримінатор.
/// </summary>
public sealed class TargetLanguage
{
    /// <summary>Код мови ISO 639-1 (два малих латинських символи), природний первинний ключ.</summary>
    public required string Code { get; set; }

    /// <summary>Назва мови українською для UI перемикача.</summary>
    public required string DisplayNameUk { get; set; }

    /// <summary>Чи доступна мова для старту навчання (вимкнена = ще без контенту/релізу).</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Порядок сортування у списку мов.</summary>
    public short SortOrder { get; set; }

    /// <summary>Профіль нормалізації відповідей для цієї мови (напр. "pl", "en"); null — профіль ще не заведено.</summary>
    public string? NormalizationProfile { get; set; }

    public Instant CreatedAt { get; set; }
}
