"""Готує робочі пакети для авторів-агентів: нові слова-члени (source-backed → топап), ще без картки.

Членство повторює логіку випуску (build_release): спершу всі source-backed, потім топап за частотою,
доки не набереться target. Уже закартковані леми пропускаємо. Кожен рядок пакета містить джерельний
контекст, якого досить, щоб LLM-редактор уклав картку без доступу до дампа. Не входить у випуск.
"""

from __future__ import annotations

import argparse
import glob
import json
from pathlib import Path

from languages import config


def winnable(row: dict, noun_genders: set[str]) -> bool:
    """Чи може кандидат пройти гейт роду у build_release.

    Для мов із граматичним родом (pl/de/nl) іменник без роду й без позначки множини
    завжди блокується як `missing_noun_gender`. Такі слова марно давати авторам —
    відсіюємо їх із пакета, щоб редакторські зусилля йшли на здобувні кандидати.
    """
    if not noun_genders or row.get("pos") != "noun":
        return True
    return bool(row.get("gender")) or "plural" in row.get("tags", [])


def carded_lemmas(editorial_dir: Path) -> set[str]:
    out: set[str] = set()
    for path in glob.glob(str(editorial_dir / "*.jsonl")):
        for line in open(path, encoding="utf-8"):
            if line.strip():
                out.add(json.loads(line)["lemma"])
    return out


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--lang", required=True)
    parser.add_argument("--cache", type=Path, required=True)
    parser.add_argument("--editorial-dir", type=Path, required=True)
    parser.add_argument("--target", type=int, default=2000)
    parser.add_argument("--batch-size", type=int, default=60)
    parser.add_argument("--buffer", type=int, default=120, help="Зайві топап-слова як запас на відсів")
    parser.add_argument("--start-batch", type=int, default=0, help="Номер першого пакета (щоб не перетерти наявні)")
    args = parser.parse_args()

    noun_genders = config(args.lang)["noun_genders"]
    pool = [json.loads(l) for l in open(args.cache / "all-source-supported.jsonl", encoding="utf-8") if l.strip()]
    pool = [r for r in pool if winnable(r, noun_genders)]
    backed = sorted((r for r in pool if r.get("translation_backed", True)), key=lambda r: r["frequency_rank"])
    topup = sorted((r for r in pool if not r.get("translation_backed", True)), key=lambda r: r["frequency_rank"])
    need_topup = max(0, args.target - len(backed))
    members = backed + topup[: need_topup + args.buffer]

    carded = carded_lemmas(args.editorial_dir)
    new = [r for r in members if r["lemma"] not in carded]

    worklist = args.cache / (args.lang + "-worklist")
    worklist.mkdir(parents=True, exist_ok=True)
    for old in glob.glob(str(worklist / "*.jsonl")):
        Path(old).unlink()

    batches = 0
    for i in range(0, len(new), args.batch_size):
        chunk = new[i : i + args.batch_size]
        batch_no = args.start_batch + batches
        rows = []
        for r in chunk:
            rows.append({
                "lemma": r["lemma"], "pos": r["pos"], "gender": r.get("gender", ""),
                "ipa": r["ipa"], "gloss": r["gloss"], "sense": r["sense"],
                "frequency_rank": r["frequency_rank"],
                "translation_backed": r.get("translation_backed", True),
                "translation_candidates": r["translation_candidates"],
                "source_usage_example": r["usage_example"],
                "_author_batch": f"{args.lang}-batch-{batch_no}",
            })
        (worklist / f"batch-{batch_no}.jsonl").write_text(
            "".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")
        batches += 1

    backed_new = sum(1 for r in new if r.get("translation_backed", True))
    print(json.dumps({
        "lang": args.lang, "pool": len(pool), "backed_total": len(backed),
        "topup_selected": need_topup + args.buffer, "members": len(members),
        "already_carded": len(carded), "new_to_author": len(new),
        "new_backed": backed_new, "new_topup": len(new) - backed_new,
        "batches": batches, "first_batch": args.start_batch,
        "last_batch": args.start_batch + batches - 1 if batches else None,
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
