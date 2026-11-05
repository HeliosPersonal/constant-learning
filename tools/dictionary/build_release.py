"""Зводить редакторські картки з доказами, перевіряє набір і готує CSV для T20."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import unicodedata
from collections import Counter
from pathlib import Path

from languages import config, default_out_dir


FIELDS = [
    "id", "lemma_pl", "part_of_speech", "gender", "gender_pair_id",
    "aspect_pair_id", "translation_uk", "accepted_answers_pl", "pronunciation_uk",
    "domain", "legalization_stage", "topic", "frequency_rank", "utility_rank",
    "show_context_before", "example_pl", "example_uk", "validation_status",
    "validation_sources", "reviewed_by", "review_note",
]

KNOWN_POS = {"noun", "verb", "adj", "adv", "pron", "det", "prep", "conj", "particle", "num", "intj"}


def accepted_translations(word: str, candidates: list[str], lang: str = "pl") -> set[str]:
    corrections = config(lang)["translation_corrections"]
    return {normalize(corrections.get((word, candidate), candidate)) for candidate in candidates}


def read_jsonl(path: Path) -> list[dict]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]


def normalize(value: str) -> str:
    return unicodedata.normalize("NFC", value).replace("\u0301", "").strip().casefold()


def validate_editorial(row: dict, source: dict, lang: str = "pl") -> list[str]:
    """Технічна перевірка не замінює редакторське рішення про значення й природність."""
    noun_genders = config(lang)["noun_genders"]
    problems = []
    if source.get("pos") not in KNOWN_POS:
        problems.append("unknown_part_of_speech")
    for field in ("translation_uk", "pronunciation_uk", "example_pl", "example_uk"):
        if not isinstance(row.get(field), str) or not row[field].strip():
            problems.append("missing_" + field)
    if problems:
        return problems
    candidates = accepted_translations(source["lemma"], source["translation_candidates"], lang)
    if not all(normalize(t) in candidates for t in row["translation_uk"].split(";")):
        problems.append("translation_not_in_evidence")
    if not re.search("[а-яіїєґА-ЯІЇЄҐ]", row["translation_uk"]):
        problems.append("translation_not_ukrainian")
    if re.search("[а-яіїєґА-ЯІЇЄҐ]", row["example_pl"]):
        problems.append("cyrillic_in_polish_example")
    if not re.search("[а-яіїєґА-ЯІЇЄҐ]", row["example_uk"]):
        problems.append("example_not_ukrainian")
    if re.search("[a-zA-Z]", row["pronunciation_uk"]):
        problems.append("latin_in_pronunciation")
    if normalize(row["example_pl"]) == normalize(source["usage_example"]):
        problems.append("copied_source_example")
    if len(row["example_pl"].split()) < 3 or len(row["example_pl"].split()) > 18:
        problems.append("example_length")
    if noun_genders and source["pos"] == "noun" and not source.get("gender") and "plural" not in source.get("tags", []):
        problems.append("missing_noun_gender")
    return problems


def write_csv(path: Path, fields: list[str], rows: list[dict]) -> None:
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lang", choices=sorted(("pl", "en", "de", "nl")), default="pl")
    parser.add_argument("--sources", type=Path, required=True)
    parser.add_argument("--editorial", type=Path, nargs="+", required=True)
    parser.add_argument("--output", type=Path, help="Каталог випуску; типово похідний від --lang (pl→docs/dictionary, інші→docs/dictionary/<lang>)")
    parser.add_argument("--out-dir", type=Path, dest="output", help="Синонім --output")
    parser.add_argument("--target", type=int, default=2000)
    parser.add_argument("--approve-source-backed", action="store_true", help="Застосувати погоджений користувачем стандарт звірки джерела + LLM-редактура")
    args = parser.parse_args()
    lang = args.lang
    settings = config(lang)
    noun_genders = settings["noun_genders"]
    correction_sources = settings["correction_sources"]
    translation_corrections = settings["translation_corrections"]
    output = args.output if args.output is not None else Path(default_out_dir(lang))
    sources = {row["lemma"]: row for row in read_jsonl(args.sources)}
    decisions = []
    for path in args.editorial:
        for row in read_jsonl(path):
            decisions.append((row, path.name))
    seen = set()
    accepted, blocked, evidence = [], [], []
    for row, reviewer_file in decisions:
        word = row["lemma"]
        if word in seen:
            raise ValueError("Повтор редакторського рішення: " + word)
        seen.add(word)
        source = sources.get(word)
        problems = ["source_missing"] if source is None else validate_editorial(row, source, lang)
        if row.get("review_note", "").startswith("blocked:"):
            problems.insert(0, "editorial_rejected")
        if problems:
            blocked.append({"lemma_pl": word, "reasons": ";".join(problems), "review_note": row.get("review_note", "")})
            continue
        evidence_id = "wikt-" + hashlib.sha256(word.encode("utf-8")).hexdigest()[:16]
        content = dict.fromkeys(FIELDS, "")
        content.update(
            id=settings["id_prefix"] + hashlib.sha256(word.encode("utf-8")).hexdigest()[:16],
            lemma_pl=word, part_of_speech=source["pos"], gender=source.get("gender") or ("pl" if source["pos"] == "noun" and "plural" in source.get("tags", []) and "pl" in noun_genders else ""),
            translation_uk=row["translation_uk"].strip(), accepted_answers_pl=word,
            pronunciation_uk=row["pronunciation_uk"].strip(), domain="everyday",
            topic="general", frequency_rank=source["frequency_rank"],
            show_context_before="true", example_pl=row["example_pl"].strip(),
            example_uk=row["example_uk"].strip(), validation_status="approved",
            validation_sources=evidence_id, reviewed_by="LLM-editorial/source-backed",
            review_note=row.get("review_note", ""),
        )
        accepted.append(content)
        evidence.append({
            "id": evidence_id, "lemma_pl": word, "source_url": source["source_url"],
            "source_record_sha256": source["source_record_sha256"],
            "part_of_speech": source["pos"], "sense_index": source["sense"],
            "translation_candidates": source["translation_candidates"], "ipa": source["ipa"],
            "source_tags": source.get("tags", []),
            "translation_correction": [{"original": original, "corrected": corrected, "source_url": correction_sources[word]} for (lemma, original), corrected in translation_corrections.items() if lemma == word],
            "usage_example_sha256": hashlib.sha256(source["usage_example"].encode("utf-8")).hexdigest(),
            "reviewer_batch": row.get("_author_batch", reviewer_file),
            "cross_review_batch": row.get("_cross_review"),
            "example_origin": "original/LLM-editorial",
        })
    accepted.sort(key=lambda row: int(row["frequency_rank"]))
    counts = Counter(reason for row in blocked for reason in row["reasons"].split(";"))
    report = {
        "language": lang,
        "target": args.target, "editorial_decisions": len(decisions),
        "approved_candidates": len(accepted), "blocked": len(blocked),
        "ready": len(accepted) >= args.target and args.approve_source_backed,
        "approval_policy_confirmed": args.approve_source_backed, "reasons": dict(counts),
        "scope": "Двомовний словник (цільова мова → українська); джерельна звірка й LLM-редактура, не незалежна перевірка людиною.",
    }
    output.mkdir(parents=True, exist_ok=True)
    (output / "readiness.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    write_csv(output / "review-queue.csv", ["lemma_pl", "reasons", "review_note"], blocked)
    if not report["ready"]:
        raise SystemExit(f"Набір не готовий: {len(accepted)} / {args.target}; див. readiness.json і review-queue.csv")
    chosen = accepted[:args.target]
    chosen_ids = {row["validation_sources"] for row in chosen}
    write_csv(output / "course.csv", FIELDS, chosen)
    (output / "evidence.jsonl").write_text("".join(json.dumps(row, ensure_ascii=False) + "\n" for row in evidence if row["id"] in chosen_ids), encoding="utf-8")
    report["released"] = len(chosen)
    report["spares"] = len(accepted) - len(chosen)
    report["course_sha256"] = hashlib.sha256((output / "course.csv").read_bytes()).hexdigest()
    report["evidence_sha256"] = hashlib.sha256((output / "evidence.jsonl").read_bytes()).hexdigest()
    (output / "readiness.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Готово: {len(chosen)} унікальних лем; відкладено {len(blocked)}.")


if __name__ == "__main__":
    main()
