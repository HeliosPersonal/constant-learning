"""Завантажує словникові джерела й відбирає кандидати з пов'язаними значеннями, перекладом та IPA."""

from __future__ import annotations

import argparse
import csv
import gzip
import hashlib
import json
import re
import subprocess
import urllib.parse
from pathlib import Path

from languages import config

EXCLUDED_POS = {"proper-noun", "name", "abbrev", "character", "phrase", "punct", "symbol", "suffix", "prefix"}
EXCLUDED_TAGS = {"vulgar", "obsolete", "archaic", "offensive"}


def checksum(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def download(url: str, path: Path) -> None:
    """Кеш-орієнтоване завантаження через curl --ssl-no-revoke.

    Kaikki/Wiktionary/Tatoeba вимагають пропуску перевірки відкликання сертифіката
    (сам сертифікат усе одно валідовано); urllib/requests падають із revocation error.
    """
    if not path.exists():
        temporary = path.with_suffix(path.suffix + ".part")
        subprocess.run(
            ["curl", "--ssl-no-revoke", "-fL", "-o", str(temporary), url],
            check=True,
        )
        temporary.replace(path)


def iter_frequency(path: Path, freq_format: str):
    """Повертає пари (слово, бал) незалежно від формату частотного джерела."""
    if freq_format == "kwjp":
        with gzip.open(path, "rt", encoding="utf-8", newline="") as stream:
            reader = csv.reader(stream)
            next(reader)
            for row in reader:
                yield row[0], float(row[4])
    elif freq_format == "hermitdave":
        with path.open("rt", encoding="utf-8") as stream:
            for line in stream:
                parts = line.split()
                if len(parts) >= 2:
                    yield parts[0], float(parts[1])
    else:
        raise SystemExit("Невідомий формат частотного джерела: " + freq_format)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lang", choices=sorted(("pl", "en", "de", "nl")), default="pl")
    parser.add_argument("--cache", type=Path, required=True)
    parser.add_argument("--candidate-limit", type=int, default=8000)
    parser.add_argument("--expected-wiktionary-sha256")
    parser.add_argument("--expected-frequency-sha256")
    args = parser.parse_args()
    lang = args.lang
    settings = config(lang)
    lemma_pattern = settings["lemma_regex"]
    gender_map = settings["gender_map"]
    args.cache.mkdir(parents=True, exist_ok=True)
    dictionary = args.cache / (settings["kaikki_slug"] + ".jsonl.gz")
    frequency = args.cache / settings["freq_filename"]
    download("https://kaikki.org/" + settings["kaikki_slug"] + "/raw-wiktextract-data.jsonl.gz", dictionary)
    download(settings["freq_url"], frequency)
    fingerprints = {"wiktionary": checksum(dictionary), "frequency": checksum(frequency)}
    for name in fingerprints:
        expected = getattr(args, "expected_" + name + "_sha256")
        if expected and expected != fingerprints[name]:
            raise SystemExit("Знімок джерела змінився: " + name + "; потрібна нова перевірка, старі approved не переносяться автоматично.")
    scores = {}
    for word, score in iter_frequency(frequency, settings["freq_format"]):
        if word not in scores or score > scores[word]:
            scores[word] = score
    ordered = sorted(scores, key=lambda word: -scores[word])
    rank = {word: index + 1 for index, word in enumerate(ordered)}
    words = {word for word in ordered[:args.candidate_limit] if re.fullmatch(lemma_pattern, word)}
    selected = {}
    with gzip.open(dictionary, "rt", encoding="utf-8") as stream:
        for line in stream:
            entry = json.loads(line)
            word = entry.get("word")
            if entry.get("lang_code") != lang or word not in words or entry.get("pos") in EXCLUDED_POS:
                continue
            tags = entry.get("tags", [])
            if EXCLUDED_TAGS.intersection(tags):
                continue
            ipa = [sound["ipa"] for sound in entry.get("sounds", []) if sound.get("ipa") and not {"Slavic-alphabet", "plural"}.intersection(sound.get("tags", []))]
            if not ipa:
                continue
            for sense in entry.get("senses", []):
                if sense.get("form_of") or (EXCLUDED_TAGS | {"figuratively", "slang"}).intersection(sense.get("tags", [])):
                    continue
                translations = [translation["word"].replace("́", "") for translation in entry.get("translations", []) if translation.get("lang_code") == "uk" and translation.get("sense_index") and translation["sense_index"] == sense.get("sense_index") and re.fullmatch("[а-яіїєґА-ЯІЇЄҐ́ ’'-]+", translation.get("word", ""))]
                examples = [example["text"] for example in sense.get("examples", []) if example.get("text") and 3 <= len(example["text"].split()) <= 10 and not re.search(r"[<>\[\]{}]", example["text"])]
                if not translations or not examples or word in selected:
                    continue
                selected[word] = {
                    "lemma": word, "frequency_rank": rank[word], "pos": entry["pos"], "tags": tags,
                    "gender": next((value for key, value in gender_map if key in tags), ""),
                    "sense": sense.get("sense_index"), "gloss": " ".join(sense.get("glosses", [""])[0].split()[:8]),
                    "translation_candidates": list(dict.fromkeys(translations))[:2], "ipa": ipa[0], "usage_example": examples[0],
                    "source_url": "https://" + settings["wiktionary_host"] + "/wiki/" + urllib.parse.quote(word),
                    "source_record_sha256": hashlib.sha256(line.encode("utf-8")).hexdigest(),
                }
                break
    rows = sorted(selected.values(), key=lambda row: row["frequency_rank"])
    (args.cache / "all-source-supported.jsonl").write_text("".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows), encoding="utf-8")
    (args.cache / "source-fingerprints.json").write_text(json.dumps(fingerprints, indent=2) + "\n", encoding="utf-8")
    print(f"Кандидатів із джерельними даними: {len(rows)}. Редакторське рішення ще потрібне.")


if __name__ == "__main__":
    main()
