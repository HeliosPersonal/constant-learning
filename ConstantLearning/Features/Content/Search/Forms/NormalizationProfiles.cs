namespace ConstantLearning.Features.Content.Search.Forms;

/// <summary>
/// Профіль нормалізації мови (ADR-0004: мови — конфіг-таблиця, не enum). Задає, які літери
/// згортаються у пошуковий ключ понад загальне зняття діакритики (NFD), і які артиклі відкидаються
/// з початку словникової форми. Код збігається зі <c>target_languages.normalization_profile</c>.
/// </summary>
/// <param name="Code">Код мови (<c>normalization_profile</c>).</param>
/// <param name="FoldSubstitutions">
/// Підстановки згортання понад NFD. Значення — рядок (не символ): німецьке розкриття умлаутів
/// дає дві літери (ä→ae, ß→ss). Застосовуються <b>до</b> NFD-декомпозиції (див.
/// <see cref="SearchTextNormalizer.Fold"/>), тож ловлять прекомпозований символ, перш ніж NFD
/// зніме його діакритику.
/// </param>
/// <param name="Articles">
/// Артиклі, що відкидаються з початку відображуваної форми (de: der/die/das, nl: de/het). Порожньо
/// для мов без артиклевих словникових форм (uk/pl/en).
/// </param>
public sealed record NormalizationProfile(
    string Code,
    IReadOnlyDictionary<char, string> FoldSubstitutions,
    IReadOnlyList<string> Articles)
{
    /// <summary>
    /// Повертає форму без провідного артикля (напр. «der Hund» → «Hund», «het huis» → «huis») або
    /// null, якщо провідного токена-артикля немає чи після нього нічого не лишається. Токенне (а не
    /// підрядкове) й регістро-незалежне порівняння: артикль — це саме перше ціле слово, не збіг усередині.
    /// </summary>
    public string? StripLeadingArticle(string display)
    {
        if (Articles.Count == 0) return null;
        var trimmed = display.TrimStart();
        var space = trimmed.IndexOf(' ');
        if (space <= 0) return null;
        if (!Articles.Contains(trimmed[..space], StringComparer.OrdinalIgnoreCase)) return null;
        var rest = trimmed[(space + 1)..].TrimStart();
        return rest.Length > 0 ? rest : null;
    }
}

/// <summary>
/// Реєстр профілів нормалізації — каркас діспетчу за <c>normalization_profile</c>. Набори підстановок
/// усіх профілів об'єднуються в один спільний fold (<see cref="SearchTextNormalizer.Fold"/>), щоб
/// згортання запису форм і згортання пошукового запиту лишалися однаковими, доки пошук не scoped за
/// активною мовою (до T13 запит згортається, ще не знаючи, якою мовою знайдеться збіг). Нові мови
/// додаються лише в <see cref="All"/>: узагальнений код проєкції/імпортера не змінюється.
/// </summary>
public static class NormalizationProfiles
{
    // Українська — мова пояснень (uk-переклади присутні завжди), тож її згортання входить у спільний fold.
    // ґ→г: кирилична «ґе» не має потрібної NFD-декомпозиції.
    public static readonly NormalizationProfile Ukrainian =
        new("uk", new Dictionary<char, string> { ['ґ'] = "г" }, []);

    // Польська: ł не має канонічної NFD-декомпозиції, тож згортання ł→l задається явно
    // (ż/ó/ą/… знімаються загальним NFD).
    public static readonly NormalizationProfile Polish =
        new("pl", new Dictionary<char, string> { ['ł'] = "l" }, []);

    // Англійська: латинська діакритика (café→cafe, naïve→naive) знімається загальним NFD — власних підстановок немає.
    public static readonly NormalizationProfile English =
        new("en", new Dictionary<char, string>(), []);

    // Німецька (T07b): умлаути й ескцет розкриваються у дві літери (DIN 5007-2 / набір без умлаутів),
    // щоб «fussball» знаходило «Fußball», а «schoen» — «schön». ß обов'язковий: NFD його не розкладає,
    // тож без підстановки «strasse» ніколи не збіглося б зі «straße». Капіталізація іменників
    // («Haus») згортається вже загальним ToLowerInvariant у Normalize — окремого правила не треба.
    // Артиклі роду der/die/das відкидаються на боці форм (StripLeadingArticle), а не у fold.
    public static readonly NormalizationProfile German =
        new("de", new Dictionary<char, string> { ['ä'] = "ae", ['ö'] = "oe", ['ü'] = "ue", ['ß'] = "ss" },
            ["der", "die", "das"]);

    // Нідерландська (T07b): власних підстановок згортання немає — трема (ë/ï у «drieën», «geïnteresseerd»)
    // знімається загальним NFD, а не розкривається. Артиклі de/het відкидаються на боці форм.
    // Відоме обмеження спільного суперсет-fold: ä/ö/ü у нідерландських словах згортаються за німецьким
    // правилом (coördinatie→cooerdinatie). Прийнятно: контенту nl ще немає, fold перебудовний,
    // а мово-свідомий fold заборонений інваріантом глобального пошуку (до T13).
    public static readonly NormalizationProfile Dutch =
        new("nl", new Dictionary<char, string>(), ["de", "het"]);

    /// <summary>Усі профілі; їхні підстановки об'єднуються у спільний мово-незалежний fold.</summary>
    public static readonly IReadOnlyList<NormalizationProfile> All =
        [Ukrainian, Polish, English, German, Dutch];

    /// <summary>Об'єднана мапа підстановок згортання з усіх профілів (спільний fold — суперсет мов).</summary>
    public static readonly IReadOnlyDictionary<char, string> FoldSubstitutions =
        All.SelectMany(profile => profile.FoldSubstitutions)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

    private static readonly IReadOnlyDictionary<string, NormalizationProfile> ByCodeMap =
        All.ToDictionary(profile => profile.Code, StringComparer.Ordinal);

    /// <summary>Профіль за кодом (<c>normalization_profile</c>), або null, якщо ще не заведено.</summary>
    public static NormalizationProfile? ByCode(string code) =>
        ByCodeMap.GetValueOrDefault(code);
}
