"""Перевіряє готовий CSV та журнал доказів без мережі й без зміни даних."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import urllib.parse
from pathlib import Path

from build_release import FIELDS, KNOWN_POS, accepted_translations, normalize


def validate(directory: Path, target: int) -> dict:
    readiness = json.loads((directory / "readiness.json").read_text(encoding="utf-8"))
    if not readiness.get("ready") or not readiness.get("approval_policy_confirmed"):
        raise ValueError("Стандарт схвалення не погоджено або набір не готовий")
    if readiness.get("course_sha256") != hashlib.sha256((directory / "course.csv").read_bytes()).hexdigest():
        raise ValueError("CSV змінено після побудови звіту готовності")
    if readiness.get("evidence_sha256") != hashlib.sha256((directory / "evidence.jsonl").read_bytes()).hexdigest():
        raise ValueError("Журнал доказів змінено після побудови звіту")
    with (directory / "course.csv").open(encoding="utf-8", newline="") as stream:
        reader = csv.DictReader(stream)
        if reader.fieldnames != FIELDS:
            raise ValueError("Невідповідний заголовок CSV")
        rows = list(reader)
    evidence_rows = [json.loads(line) for line in (directory / "evidence.jsonl").read_text(encoding="utf-8").splitlines()]
    evidence = {row["id"]: row for row in evidence_rows}
    if len(evidence_rows) != len(evidence) or len(evidence) != len(rows):
        raise ValueError("Докази мають бути унікальними й відповідати всім карткам")
    if len(rows) != target or len({normalize(row["lemma_pl"]) for row in rows}) != target:
        raise ValueError("Потрібно рівно задану кількість унікальних лем")
    if len({row["id"] for row in rows}) != target:
        raise ValueError("ID карток повторюються")
    required = ["id", "lemma_pl", "part_of_speech", "translation_uk", "accepted_answers_pl", "pronunciation_uk", "domain", "topic", "frequency_rank", "example_pl", "example_uk", "reviewed_by"]
    ranks = []
    for row in rows:
        word = row["lemma_pl"]
        if row["part_of_speech"] not in KNOWN_POS:
            raise ValueError("Невизначена частина мови: " + word)
        if not re.fullmatch("[a-ząćęłńóśźż]+", word):
            raise ValueError("Очікується однословна польська лема: " + word)
        if any(not row[key].strip() for key in required) or row["validation_status"] != "approved":
            raise ValueError("Неповна або несхвалена картка: " + word)
        if row["part_of_speech"] == "noun" and row["gender"] not in ["m", "f", "n", "pl"]:
            raise ValueError("Не визначено рід іменника: " + word)
        proof = evidence.get(row["validation_sources"])
        if not proof or proof["lemma_pl"] != word or proof["part_of_speech"] != row["part_of_speech"]:
            raise ValueError("Доказ не відповідає лемі: " + word)
        if row["gender"] == "pl" and "plural" not in proof.get("source_tags", []):
            raise ValueError("Множину не підтверджено джерелом: " + word)
        if not proof.get("sense_index") or not proof.get("ipa") or not re.fullmatch("[0-9a-f]{64}", proof["source_record_sha256"]):
            raise ValueError("Неповний доказ: " + word)
        if proof["source_url"] != "https://pl.wiktionary.org/wiki/" + urllib.parse.quote(word):
            raise ValueError("Невідповідне посилання: " + word)
        variants = accepted_translations(word, proof["translation_candidates"])
        if any(normalize(value) not in variants for value in row["translation_uk"].split(";")):
            raise ValueError("Переклад не підтверджено джерелом: " + word)
        if hashlib.sha256(row["example_pl"].encode("utf-8")).hexdigest() == proof["usage_example_sha256"]:
            raise ValueError("Скопійовано приклад джерела: " + word)
        if re.search("[а-яіїєґА-ЯІЇЄҐ]", row["example_pl"]) or not re.search("[а-яіїєґА-ЯІЇЄҐ]", row["example_uk"]):
            raise ValueError("Невідповідна мова прикладу: " + word)
        if re.search("[a-zA-Z]", row["pronunciation_uk"]):
            raise ValueError("Латиниця в українській підказці вимови: " + word)
        if row["accepted_answers_pl"] != word:
            raise ValueError("Варіант відповіді потребує окремого підтвердження: " + word)
        ranks.append(int(row["frequency_rank"]))
    if ranks != sorted(ranks) or any(rank < 1 for rank in ranks):
        raise ValueError("Порушено частотний порядок")
    return {"ready": True, "approved_unique_lemmas": target, "evidence_records": len(evidence), "course_sha256": hashlib.sha256((directory / "course.csv").read_bytes()).hexdigest()}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, required=True)
    parser.add_argument("--target", type=int, default=2000)
    args = parser.parse_args()
    print(json.dumps(validate(args.directory, args.target), ensure_ascii=True, indent=2))


if __name__ == "__main__":
    main()
