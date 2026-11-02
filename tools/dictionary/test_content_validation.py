"""Перевірки відмови у випуску неповного або непідтвердженого словника."""

import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from build_release import main, validate_editorial


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


if __name__ == "__main__":
    unittest.main()
