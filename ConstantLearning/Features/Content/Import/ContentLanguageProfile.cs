namespace ConstantLearning.Features.Content.Import;

/// <summary>
/// Профіль цільової мови для імпорту/верифікації контенту: алфавіт схваленої леми, хост Вікісловника
/// для <c>source_url</c>, допустимі роди іменника та атрибуція джерела. Код збігається з
/// <c>target_languages.code</c> / <see cref="Data.DictionaryEntry.TargetLanguage"/>
/// (ADR-0003/0004: мови — конфіг-таблиця, не enum). Доповнює суто-пошуковий
/// <see cref="Search.Forms.NormalizationProfile"/> (той відповідає за згортання форм, цей — за правила
/// провенансу імпорту). Нова мова додається лише в <see cref="ContentLanguageProfiles.All"/>.
/// </summary>
/// <param name="Code">Код мови.</param>
/// <param name="WiktionaryHost">Хост Вікісловника цільової мови (<c>source_url</c> доказу).</param>
/// <param name="LemmaPattern">Регекс однослівної схваленої леми (малі літери алфавіту мови).</param>
/// <param name="NounGenders">
/// Допустимі позначки роду іменника. Порожньо — мова без роду (en): іменник не потребує позначки.
/// Токен <c>pl</c> (лише множина) додатково вимагає підтвердження <c>source_tags: plural</c>.
/// </param>
/// <param name="ReferenceSourceLabel">Назва джерела доказів (атрибуція).</param>
/// <param name="AttributionPrefix">Префікс рядка атрибуції CC BY-SA.</param>
public sealed record ContentLanguageProfile(
    string Code,
    string WiktionaryHost,
    string LemmaPattern,
    IReadOnlySet<string> NounGenders,
    string ReferenceSourceLabel,
    string AttributionPrefix);

/// <summary>Реєстр профілів цільових мов контенту. Pl відтворює історичну поведінку байт-у-байт.</summary>
public static class ContentLanguageProfiles
{
    /// <summary>Мова набору за замовчуванням, коли <c>readiness.json</c> не містить поля <c>language</c>.</summary>
    public const string DefaultCode = "pl";

    // Польська: ł/ó/ą/… входять до алфавіту; роди m/f/n + позначка множини pl; джерело — Wikisłownik.
    public static readonly ContentLanguageProfile Polish = new(
        "pl", "pl.wiktionary.org", "^[a-ząćęłńóśźż]+$",
        new HashSet<string>(StringComparer.Ordinal) { "m", "f", "n", "pl" },
        "Wikisłownik", "Wikisłownik contributors; CC BY-SA");

    // Англійська: іменник не має роду (порожній набір) — схвалений іменник не потребує позначки.
    public static readonly ContentLanguageProfile English = new(
        "en", "en.wiktionary.org", "^[a-z]+$",
        new HashSet<string>(StringComparer.Ordinal),
        "Wiktionary", "Wiktionary contributors; CC BY-SA");

    // Німецька: умлаути й ß у лемі; роди der/die/das → m/f/n + множина pl.
    public static readonly ContentLanguageProfile German = new(
        "de", "de.wiktionary.org", "^[a-zäöüß]+$",
        new HashSet<string>(StringComparer.Ordinal) { "m", "f", "n", "pl" },
        "Wiktionary", "Wiktionary contributors; CC BY-SA");

    // Нідерландська: трема й кілька запозичених діакритик у лемі; рід — спільний (c) / середній (n) + множина pl.
    public static readonly ContentLanguageProfile Dutch = new(
        "nl", "nl.wiktionary.org", "^[a-zàäéëïöü]+$",
        new HashSet<string>(StringComparer.Ordinal) { "c", "n", "pl" },
        "Wiktionary", "Wiktionary contributors; CC BY-SA");

    /// <summary>Усі профілі. Нова мова додається тільки сюди.</summary>
    public static readonly IReadOnlyList<ContentLanguageProfile> All = [Polish, English, German, Dutch];

    private static readonly IReadOnlyDictionary<string, ContentLanguageProfile> ByCodeMap =
        All.ToDictionary(profile => profile.Code, StringComparer.Ordinal);

    /// <summary>Профіль за кодом мови, або null, якщо мову ще не заведено.</summary>
    public static ContentLanguageProfile? ByCode(string code) => ByCodeMap.GetValueOrDefault(code);
}
