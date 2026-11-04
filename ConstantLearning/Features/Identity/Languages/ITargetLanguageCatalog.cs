using ConstantLearning.Features.Identity.Data;

namespace ConstantLearning.Features.Identity.Languages;

/// <summary>
/// Читач каталогу цільових мов (<c>target_languages</c>). Віддає мови в порядку
/// <c>sort_order</c>; фільтрація за доступністю — на боці викликача.
/// </summary>
public interface ITargetLanguageCatalog
{
    /// <summary>Усі мови каталогу (увімкнені й вимкнені) у порядку <c>sort_order</c>.</summary>
    Task<IReadOnlyList<TargetLanguage>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Повертає мову за кодом або <c>null</c>, якщо коду немає в каталозі.</summary>
    Task<TargetLanguage?> FindAsync(string code, CancellationToken cancellationToken = default);
}
