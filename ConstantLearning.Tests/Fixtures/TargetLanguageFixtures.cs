using ConstantLearning.Features.Identity.Data;
using NodaTime;

namespace ConstantLearning.Tests.Fixtures;

public static class TargetLanguageFixtures
{
    /// <summary>
    /// Увімкнена мова каталогу з явним профілем нормалізації. Для тестів, яким потрібна
    /// доступна для старту мова понад seed-набір (напр. перевірки scoping/доступності).
    /// </summary>
    public static TargetLanguage NewEnabledLanguage(string code, string normalizationProfile,
        string? displayNameUk = null, short sortOrder = 100, IClock? clock = null) => new()
    {
        Code = code,
        DisplayNameUk = displayNameUk ?? $"Мова {code}",
        IsEnabled = true,
        SortOrder = sortOrder,
        NormalizationProfile = normalizationProfile,
        CreatedAt = clock?.GetCurrentInstant() ?? LearnerFixtures.DefaultInstant
    };
}
