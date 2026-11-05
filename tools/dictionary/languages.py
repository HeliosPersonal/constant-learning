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

# Kaikki публікує повний дамп кожного видання за шаблоном /<slug>/raw-wiktextract-data.jsonl.gz,
# але англійське видання має власний маршрут (/enwiktionary/ віддає 404): беремо вже відфільтрований
# за мовою файл /dictionary/English/…, де lang_code=="en".
_KAIKKI_RAW = "https://kaikki.org/{slug}/raw-wiktextract-data.jsonl.gz"

LANGUAGES: dict[str, dict] = {
    "pl": {
        "wiktionary_host": "pl.wiktionary.org",
        # Абетка однословної леми (тіло класу для re.fullmatch; еквівалент ^...$).
        "lemma_regex": "[a-ząćęłńóśźż]+",
        "noun_genders": {"m", "f", "n", "pl"},
        # Відображення тегів роду Wiktionary у код; порядок визначає пріоритет для next().
        "gender_map": [("masculine", "m"), ("feminine", "f"), ("neuter", "n")],
        "kaikki_slug": "plwiktionary",
        "kaikki_url": _KAIKKI_RAW.format(slug="plwiktionary"),
        # plwiktionary пов'язує переклад зі значенням через збіг рядка sense_index (висока точність).
        "sense_link": "index",
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
        # Англійське видання не має маршруту /enwiktionary/: беремо відфільтрований English-дамп.
        "kaikki_url": "https://kaikki.org/dictionary/English/kaikki.org-dictionary-English.jsonl.gz",
        # enwiktionary не має sense_index; пов'язуємо переклад зі значенням за збігом глоси (gloss),
        # з відкотом на порядковий номер значення, якщо глоса не збіглася.
        "sense_link": "gloss",
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
        "kaikki_url": _KAIKKI_RAW.format(slug="dewiktionary"),
        # dewiktionary, як і pl, пов'язує переклад зі значенням через збіг рядка sense_index.
        "sense_link": "index",
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
        "kaikki_url": _KAIKKI_RAW.format(slug="nlwiktionary"),
        # nlwiktionary не має sense_index на боці значення; пов'язуємо за збігом глоси (gloss),
        # з відкотом на порядковий номер значення.
        "sense_link": "gloss",
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
