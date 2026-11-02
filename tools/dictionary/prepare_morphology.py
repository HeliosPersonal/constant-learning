"""Відтворює компактну вибірку офіційного PoliMorf для лем поточного курсу."""
import argparse
import csv
import gzip
import hashlib
import json
from pathlib import Path
import unicodedata

SOURCE_URL = "https://zil.ipipan.waw.pl/PoliMorf?action=AttachFile&do=get&target=PoliMorf-0.6.7.tab.gz"
SOURCE_SHA256 = "e9b2633bf384b7a778bdb4d17dd4c8055b1d040da9f5113dae513718854216b2"


def normalize(text):
    return unicodedata.normalize("NFC", text.strip()).lower()


def prepare(source, course, output):
    source_hash = hashlib.file_digest(source.open("rb"), "sha256").hexdigest()
    if source_hash != SOURCE_SHA256:
        raise ValueError("Невідома контрольна сума офіційного PoliMorf 0.6.7")
    with course.open(encoding="utf-8-sig", newline="") as stream:
        seeds = {normalize(value) for row in csv.DictReader(stream)
                 for value in [row["lemma_pl"], *row["accepted_answers_pl"].split(";")] if value.strip()}
    selected = set()
    covered = set()
    with gzip.open(source, "rt", encoding="utf-8") as stream:
        for line in stream:
            fields = line.rstrip("\n").split("\t")
            lemma = normalize(fields[1].split(":")[0])
            if lemma in seeds:
                selected.add((fields[0], lemma, fields[2]))
                covered.add(lemma)
    output.mkdir(parents=True, exist_ok=True)
    payload = "".join("\t".join(row) + "\n" for row in sorted(selected)).encode("utf-8")
    compressed = gzip.compress(payload, mtime=0)
    (output / "polimorf-course.tab.gz").write_bytes(compressed)
    manifest = {
        "source": "PoliMorf 0.6.7", "source_url": SOURCE_URL,
        "source_sha256": source_hash, "license": "BSD-2-Clause",
        "license_url": "https://morfeusz.sgjp.pl/doc/license/en",
        "course_sha256": hashlib.file_digest(course.open("rb"), "sha256").hexdigest(),
        "subset_sha256": hashlib.sha256(compressed).hexdigest(),
        "forms": len(selected), "seeds": len(seeds), "covered_seeds": len(covered),
        "missing_seeds": sorted(seeds - covered),
        "changes": "Вибірка за лемами та прийнятними відповідями курсу; NFC/lowercase лем; вилучення омонімного суфікса та необов'язкових колонок; усунення тотожних рядків."
    }
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: manifest[key] for key in ("forms", "seeds", "covered_seeds", "missing_seeds", "subset_sha256")}, ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--course", type=Path, default=Path("docs/dictionary/course.csv"))
    parser.add_argument("--output", type=Path, default=Path("ConstantLearning/Resources/Morphology"))
    args = parser.parse_args()
    prepare(args.source, args.course, args.output)
