"""Перевірки відмови у випуску неповного або непідтвердженого словника."""

import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import csv

from build_release import main, validate_editorial
from validate_release import validate


class ContentValidationTests(unittest.TestCase):
    def setUp(self):
        self.source = {
            "lemma": "dom", "pos": "noun", "gender": "m", "tags": ["masculine"],
            "translation_candidates": ["дім"], "usage_example": "To jest mój dom.",
            "frequency_rank": 1, "source_url": "https://pl.wiktionary.org/wiki/dom",
            "source_record_sha256": "a" * 64, "sense": "1.1", "ipa": "dɔm",
        }
        self.row = {
            "lemma": "dom", "translation_uk": "дім", "pronunciation_uk": "дом",
            "example_pl": "Mój dom stoi blisko szkoły.",
            "example_uk": "Мій дім стоїть поблизу школи.", "review_note": "",
        }

    def test_unbacked_translation_is_rejected(self):
        self.row["translation_uk"] = "квартира"
        self.assertIn("translation_not_in_evidence", validate_editorial(self.row, self.source))

    def test_missing_card_fields_are_rejected(self):
        self.row["example_uk"] = ""
        self.assertIn("missing_example_uk", validate_editorial(self.row, self.source))

    def test_unknown_part_of_speech_is_rejected(self):
        self.source["pos"] = "unknown"
        self.assertIn("unknown_part_of_speech", validate_editorial(self.row, self.source))

    def test_copied_source_example_is_rejected(self):
        self.row["example_pl"] = self.source["usage_example"]
        self.assertIn("copied_source_example", validate_editorial(self.row, self.source))

    def test_plural_only_noun_does_not_get_invented_gender(self):
        self.source.update(gender="", tags=["nonvirile", "plural"])
        self.assertNotIn("missing_noun_gender", validate_editorial(self.row, self.source))
        self.source["tags"] = []
        self.assertIn("missing_noun_gender", validate_editorial(self.row, self.source))

    def test_policy_and_target_gate_release(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            sources = root / "sources.jsonl"
            editorial = root / "editorial.jsonl"
            sources.write_text(json.dumps(self.source) + "\n", encoding="utf-8")
            editorial.write_text(json.dumps(self.row) + "\n", encoding="utf-8")
            arguments = ["build_release", "--sources", str(sources), "--editorial", str(editorial), "--output", str(root / "out"), "--target", "1"]
            with patch("sys.argv", arguments), self.assertRaises(SystemExit):
                main()
            self.assertFalse((root / "out/course.csv").exists())
            with patch("sys.argv", arguments + ["--approve-source-backed"]), contextlib.redirect_stdout(io.StringIO()):
                main()
            self.assertTrue((root / "out/course.csv").exists())
            readiness = json.loads((root / "out/readiness.json").read_text(encoding="utf-8"))
            self.assertTrue(readiness["ready"])

    def test_blocked_editorial_never_becomes_approved(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            self.row["review_note"] = "blocked: значення не відповідає навчальній підказці"
            (root / "sources.jsonl").write_text(json.dumps(self.source) + "\n", encoding="utf-8")
            (root / "editorial.jsonl").write_text(json.dumps(self.row) + "\n", encoding="utf-8")
            arguments = ["build_release", "--sources", str(root / "sources.jsonl"), "--editorial", str(root / "editorial.jsonl"), "--output", str(root / "out"), "--target", "1", "--approve-source-backed"]
            with patch("sys.argv", arguments), self.assertRaises(SystemExit):
                main()
            self.assertFalse((root / "out/course.csv").exists())


class EnglishContentValidationTests(unittest.TestCase):
    def setUp(self):
        # Англійський іменник без роду: gender порожній і НЕ повинен вимагатися.
        self.source = {
            "lemma": "water", "pos": "noun", "gender": "", "tags": [],
            "translation_candidates": ["вода"], "usage_example": "The water is very cold.",
            "frequency_rank": 1, "source_url": "https://en.wiktionary.org/wiki/water",
            "source_record_sha256": "b" * 64, "sense": "1.1", "ipa": "ˈwɔːtə",
        }
        self.row = {
            "lemma": "water", "translation_uk": "вода", "pronunciation_uk": "вотер",
            "example_pl": "I drink a glass of water.",
            "example_uk": "Я п'ю склянку води.", "review_note": "",
        }

    def test_english_noun_with_empty_gender_passes(self):
        # Для en набір родів порожній: відсутність роду не є помилкою.
        self.assertEqual(validate_editorial(self.row, self.source, "en"), [])

    def test_english_translation_match_and_mismatch(self):
        self.assertNotIn("translation_not_in_evidence", validate_editorial(self.row, self.source, "en"))
        self.row["translation_uk"] = "вогонь"
        self.assertIn("translation_not_in_evidence", validate_editorial(self.row, self.source, "en"))

    def test_english_verbatim_source_example_is_rejected(self):
        self.row["example_pl"] = self.source["usage_example"]
        self.assertIn("copied_source_example", validate_editorial(self.row, self.source, "en"))

    def test_english_release_builds_validates_and_tags_language(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "sources.jsonl").write_text(json.dumps(self.source) + "\n", encoding="utf-8")
            (root / "editorial.jsonl").write_text(json.dumps(self.row) + "\n", encoding="utf-8")
            out = root / "out"
            arguments = [
                "build_release", "--lang", "en",
                "--sources", str(root / "sources.jsonl"),
                "--editorial", str(root / "editorial.jsonl"),
                "--output", str(out), "--target", "1", "--approve-source-backed",
            ]
            with patch("sys.argv", arguments), contextlib.redirect_stdout(io.StringIO()):
                main()
            readiness = json.loads((out / "readiness.json").read_text(encoding="utf-8"))
            self.assertEqual(readiness["language"], "en")
            with (out / "course.csv").open(encoding="utf-8", newline="") as stream:
                card = next(csv.DictReader(stream))
            self.assertTrue(card["id"].startswith("en-"))
            self.assertEqual(card["gender"], "")
            # Валідатор бере мову з readiness.language й приймає порожній рід англійського іменника.
            self.assertEqual(validate(out, 1)["approved_unique_lemmas"], 1)


if __name__ == "__main__":
    unittest.main()
