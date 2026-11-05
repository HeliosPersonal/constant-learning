"""Конфігурація цільових мов словника.

Запис `pl` відтворює точно ті значення, що раніше були захардкоджені в скриптах,
тому прогін із `--lang pl` дає байт-у-байт той самий `course.csv`/`evidence.jsonl`, що й сьогодні.
Інші мови (`en`, `de`, `nl`) додають власні хости Wiktionary, абетку лем, набори родів,
джерела Kaikki та частотності. Переклади завжди українські (`translation_uk`),
незалежно від цільової мови.
"""

from __future__ import annotations

# Частотні списки HermitDave FrequencyWords: формат «слово частота», уже за спаданням, без заголовка.
_HERMITDAVE = "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/{lang}/{lang}_50k.txt"

LANGUAGES: dict[str, dict] = {
    "pl": {
        "wiktionary_host": "pl.wiktionary.org",
        # Абетка однословної леми (тіло класу для re.fullmatch; еквівалент ^...$).
        "lemma_regex": "[a-ząćęłńóśźż]+",
        "noun_genders": {"m", "f", "n", "pl"},
        # Відображення тегів роду Wiktionary у код; порядок визначає пріоритет для next().
        "gender_map": [("masculine", "m"), ("feminine", "f"), ("neuter", "n")],
        "kaikki_slug": "plwiktionary",
        "freq_url": "https://raw.githubusercontent.com/ipipan/kwjp100-varia/main/freqlists/kwjp100-slowa-lemma-all.csv.gz",
        "freq_format": "kwjp",
        "freq_filename": "kwjp-lemmas.csv.gz",
        "source_label": "Wikisłownik",
        "attribution": "Wikisłownik contributors; CC BY-SA",
        "id_prefix": "pl-",
        # Адресні орфографічні виправлення українських перекладів, без загального злиття пробілів.
        "translation_corrections": {
            ("mimo", "не зважаючи на"): "незважаючи на",
            ("grono", "ґроно"): "гроно",
        },
        "correction_sources": {
            "mimo": "https://mova.gov.ua/storage/app/sites/19/pravopis.pdf",
            "grono": "https://slovnyk.ua/index.php?swrd=%D0%B3%D1%80%D0%BE%D0%BD%D0%BE",
        },
    },
    "en": {
        "wiktionary_host": "en.wiktionary.org",
        "lemma_regex": "[a-z]+",
        # Англійські іменники не мають граматичного роду: порожній набір, рід не вимагається.
        "noun_genders": set(),
        "gender_map": [],
        "kaikki_slug": "enwiktionary",
        "freq_url": _HERMITDAVE.format(lang="en"),
        "freq_format": "hermitdave",
        "freq_filename": "en_50k.txt",
        "source_label": "Wiktionary",
        "attribution": "Wiktionary contributors; CC BY-SA",
        "id_prefix": "en-",
        "translation_corrections": {},
        "correction_sources": {},
    },
    "de": {
        "wiktionary_host": "de.wiktionary.org",
        "lemma_regex": "[a-zäöüß]+",
        "noun_genders": {"m", "f", "n", "pl"},
        "gender_map": [("masculine", "m"), ("feminine", "f"), ("neuter", "n")],
        "kaikki_slug": "dewiktionary",
        "freq_url": _HERMITDAVE.format(lang="de"),
        "freq_format": "hermitdave",
        "freq_filename": "de_50k.txt",
        "source_label": "Wiktionary",
        "attribution": "Wiktionary contributors; CC BY-SA",
        "id_prefix": "de-",
        "translation_corrections": {},
        "correction_sources": {},
    },
    "nl": {
        "wiktionary_host": "nl.wiktionary.org",
        "lemma_regex": "[a-zàäéëïöü]+",
        # Нідерландські іменники: загальний рід (c) та середній (n); pl — позначка множинної форми.
        "noun_genders": {"c", "n", "pl"},
        "gender_map": [("common-gender", "c"), ("masculine", "c"), ("feminine", "c"), ("neuter", "n")],
        "kaikki_slug": "nlwiktionary",
        "freq_url": _HERMITDAVE.format(lang="nl"),
        "freq_format": "hermitdave",
        "freq_filename": "nl_50k.txt",
        "source_label": "Wiktionary",
        "attribution": "Wiktionary contributors; CC BY-SA",
        "id_prefix": "nl-",
        "translation_corrections": {},
        "correction_sources": {},
    },
}


def config(lang: str) -> dict:
    """Повертає конфігурацію мови або завершує з помилкою для невідомого коду."""
    try:
        return LANGUAGES[lang]
    except KeyError:
        raise SystemExit("Невідома цільова мова: " + lang + "; підтримуються: " + ", ".join(LANGUAGES))


def default_out_dir(lang: str) -> str:
    """pl лишається у пласкому docs/dictionary/; інші мови пишуть у docs/dictionary/<lang>/."""
    return "docs/dictionary" if lang == "pl" else "docs/dictionary/" + lang
