"""Завантажує словникові джерела й відбирає кандидати з пов'язаними значеннями, перекладом та IPA."""

from __future__ import annotations

import argparse
import csv
import gzip
import hashlib
import json
import re
import urllib.parse
import urllib.request
from pathlib import Path


WIKTIONARY_URL = "https://kaikki.org/plwiktionary/raw-wiktextract-data.jsonl.gz"
FREQUENCY_URL = "https://raw.githubusercontent.com/ipipan/kwjp100-varia/main/freqlists/kwjp100-slowa-lemma-all.csv.gz"
EXCLUDED_POS = {"proper-noun", "name", "abbrev", "character", "phrase", "punct", "symbol", "suffix", "prefix"}
EXCLUDED_TAGS = {"vulgar", "obsolete", "archaic", "offensive"}


def checksum(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def download(url: str, path: Path) -> None:
    if not path.exists():
        temporary = path.with_suffix(path.suffix + ".part")
        with urllib.request.urlopen(url, timeout=60) as response, temporary.open("wb") as target:
            while chunk := response.read(1024 * 1024):
                target.write(chunk)
        temporary.replace(path)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache", type=Path, required=True)
    parser.add_argument("--candidate-limit", type=int, default=8000)
    parser.add_argument("--expected-wiktionary-sha256")
    parser.add_argument("--expected-frequency-sha256")
    args = parser.parse_args()
    args.cache.mkdir(parents=True, exist_ok=True)
    dictionary = args.cache / "plwiktionary.jsonl.gz"
    frequency = args.cache / "kwjp-lemmas.csv.gz"
    download(WIKTIONARY_URL, dictionary)
    download(FREQUENCY_URL, frequency)
    fingerprints = {"wiktionary": checksum(dictionary), "frequency": checksum(frequency)}
    for name in fingerprints:
        expected = getattr(args, "expected_" + name + "_sha256")
        if expected and expected != fingerprints[name]:
            raise SystemExit("Знімок джерела змінився: " + name + "; потрібна нова перевірка, старі approved не переносяться автоматично.")
    scores = {}
    with gzip.open(frequency, "rt", encoding="utf-8", newline="") as stream:
        reader = csv.reader(stream)
        next(reader)
        for row in reader:
            word, score = row[0], float(row[4])
            if word not in scores or score > scores[word]:
                scores[word] = score
    ordered = sorted(scores, key=lambda word: -scores[word])
    rank = {word: index + 1 for index, word in enumerate(ordered)}
    words = {word for word in ordered[:args.candidate_limit] if re.fullmatch("[a-ząćęłńóśźż]+", word)}
    selected = {}
    with gzip.open(dictionary, "rt", encoding="utf-8") as stream:
        for line in stream:
            entry = json.loads(line)
            word = entry.get("word")
            if entry.get("lang_code") != "pl" or word not in words or entry.get("pos") in EXCLUDED_POS:
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
                translations = [translation["word"].replace("\u0301", "") for translation in entry.get("translations", []) if translation.get("lang_code") == "uk" and translation.get("sense_index") and translation["sense_index"] == sense.get("sense_index") and re.fullmatch("[а-яіїєґА-ЯІЇЄҐ\u0301 ’'-]+", translation.get("word", ""))]
                examples = [example["text"] for example in sense.get("examples", []) if example.get("text") and 3 <= len(example["text"].split()) <= 10 and not re.search(r"[<>\[\]{}]", example["text"])]
                if not translations or not examples or word in selected:
                    continue
                selected[word] = {
                    "lemma": word, "frequency_rank": rank[word], "pos": entry["pos"], "tags": tags,
                    "gender": next((value for key, value in [("masculine", "m"), ("feminine", "f"), ("neuter", "n")] if key in tags), ""),
                    "sense": sense.get("sense_index"), "gloss": " ".join(sense.get("glosses", [""])[0].split()[:8]),
                    "translation_candidates": list(dict.fromkeys(translations))[:2], "ipa": ipa[0], "usage_example": examples[0],
                    "source_url": "https://pl.wiktionary.org/wiki/" + urllib.parse.quote(word),
                    "source_record_sha256": hashlib.sha256(line.encode("utf-8")).hexdigest(),
                }
                break
    rows = sorted(selected.values(), key=lambda row: row["frequency_rank"])
    (args.cache / "all-source-supported.jsonl").write_text("".join(json.dumps(row, ensure_ascii=False) + "\n" for row in rows), encoding="utf-8")
    (args.cache / "source-fingerprints.json").write_text(json.dumps(fingerprints, indent=2) + "\n", encoding="utf-8")
    print(f"Кандидатів із джерельними даними: {len(rows)}. Редакторське рішення ще потрібне.")


if __name__ == "__main__":
    main()
