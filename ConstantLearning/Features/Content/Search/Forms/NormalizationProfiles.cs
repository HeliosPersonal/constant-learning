namespace ConstantLearning.Features.Content.Search.Forms;

/// <summary>
/// Профіль нормалізації мови (ADR-0004: мови — конфіг-таблиця, не enum). Задає, які літери
/// згортаються у пошуковий ключ понад загальне зняття діакритики (NFD), яке й так покриває
/// латиницю/кирилицю. Код збігається зі <c>target_languages.normalization_profile</c>.
/// </summary>
public sealed record NormalizationProfile(string Code, IReadOnlyDictionary<char, char> FoldSubstitutions);

/// <summary>
/// Реєстр профілів нормалізації — каркас діспетчу за <c>normalization_profile</c>. Набори підстановок
/// усіх профілів об'єднуються в один спільний fold (<see cref="SearchTextNormalizer.Fold"/>), щоб
/// згортання запису форм і згортання пошукового запиту лишалися однаковими, доки пошук не scoped за
/// активною мовою (до T13 запит згортається, ще не знаючи, якою мовою знайдеться збіг). Нові мови
/// (de/nl — T07b) додаються лише в <see cref="All"/>: узагальнений код проєкції/імпортера не змінюється.
/// </summary>
public static class NormalizationProfiles
{
    // Українська — мова пояснень (uk-переклади присутні завжди), тож її згортання входить у спільний fold.
    // ґ→г: кирилична «ґе» не має потрібної NFD-декомпозиції.
    public static readonly NormalizationProfile Ukrainian = new("uk", new Dictionary<char, char> { ['ґ'] = 'г' });

    // Польська: ł не має канонічної NFD-декомпозиції, тож згортання ł→l задається явно
    // (ż/ó/ą/… знімаються загальним NFD).
    public static readonly NormalizationProfile Polish = new("pl", new Dictionary<char, char> { ['ł'] = 'l' });

    // Англійська: латинська діакритика (café→cafe, naïve→naive) знімається загальним NFD — власних підстановок немає.
    public static readonly NormalizationProfile English = new("en", new Dictionary<char, char>());

    /// <summary>Усі профілі; їхні підстановки об'єднуються у спільний мово-незалежний fold.</summary>
    public static readonly IReadOnlyList<NormalizationProfile> All = [Ukrainian, Polish, English];

    /// <summary>Об'єднана мапа підстановок згортання з усіх профілів (спільний fold — суперсет мов).</summary>
    public static readonly IReadOnlyDictionary<char, char> FoldSubstitutions =
        All.SelectMany(profile => profile.FoldSubstitutions)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

    private static readonly IReadOnlyDictionary<string, NormalizationProfile> ByCodeMap =
        All.ToDictionary(profile => profile.Code, StringComparer.Ordinal);

    /// <summary>Профіль за кодом (<c>normalization_profile</c>), або null, якщо ще не заведено.</summary>
    public static NormalizationProfile? ByCode(string code) =>
        ByCodeMap.GetValueOrDefault(code);
}
