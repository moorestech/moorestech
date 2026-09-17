#!/usr/bin/env python3
"""壊れた・型が契約と食い違う外部 JSON でもダイジェストが止まらず、除外件数を出すことを検証する。

Verifies the digest never stops on broken or type-mismatched external JSON and reports what it excluded.
"""
import os
import sys
import tempfile
import time
import unittest
from datetime import datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from digest_fixture import SCRIPTS, TARGET_DATE, build_fixture, run_digest, write_json  # noqa: E402

sys.path.insert(0, str(SCRIPTS))
import digest_collect as dc  # noqa: E402

READY_INGEST = {"kind": "report", "steamId": "7656099", "id": "20260912_990000_x",
                "readyAt": "2026-09-12T09:50:00Z"}
UNREADABLE_BOX = "ingest.json を読めない/日付を解釈できず除外した箱 1件"
INVALID_MANIFEST = "manifest.json の型が想定外で除外した箱 1件"
INVALID_RECORD = "record.json の型が想定外で除外した件数 1件"
INVALID_RESULT = "fix-result.json の型が想定外で除外したラン 1件"

# (説明, 箱の種類, ファイル名→内容, 出力に出るべき警告) / (label, box area, files, expected warning)
TYPE_MISMATCH_CASES = [
    ("ingest readyAt int", "reports", {"ingest.json": {"readyAt": 123}}, UNREADABLE_BOX),
    ("manifest buildInfo str", "reports",
     {"ingest.json": READY_INGEST, "manifest.json": {"kind": "bug", "buildInfo": "s"}}, INVALID_MANIFEST),
    ("manifest description int", "reports",
     {"ingest.json": READY_INGEST, "manifest.json": {"kind": "bug", "description": 5}}, INVALID_MANIFEST),
    ("manifest kind list", "reports",
     {"ingest.json": READY_INGEST, "manifest.json": {"kind": ["bug"]}}, INVALID_MANIFEST),
    ("record lastUiState dict", "progress",
     {"ingest.json": READY_INGEST, "record.json": {"playSeconds": 1, "lastUiState": {"a": 1}}}, INVALID_RECORD),
    ("record endReason list", "progress",
     {"ingest.json": READY_INGEST, "record.json": {"endReason": ["q"]}}, INVALID_RECORD),
    ("record steamId list", "progress",
     {"ingest.json": READY_INGEST, "record.json": {"steamId": ["7656099"]}}, INVALID_RECORD),
    ("record playSeconds str / events non-dict", "progress",
     {"ingest.json": READY_INGEST, "record.json": {"playSeconds": "abc", "events": ["x"]}}, INVALID_RECORD),
]


class DigestDegradedTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        build_fixture(self.root)
        self.date = TARGET_DATE

    def tearDown(self):
        self.tmp.cleanup()

    def dated_run(self, name: str, result: dict) -> Path:
        """ランを作り mtime を対象日の正午にする / Creates a run whose mtime is noon of the target day"""
        path = self.root / "harness/bug-report/runs" / name / "fix-result.json"
        write_json(path, result)
        target = time.mktime(datetime.strptime(self.date, "%Y-%m-%d").replace(hour=12).timetuple())
        os.utime(path, (target, target))
        return path

    def test_type_mismatch_is_excluded_and_reported(self):
        """1件の型異常で全体を止めず exit 0、出力の ⚠ 行と stderr WARN に件数が出る
        One type mismatch never stops the run: exit 0 with a ⚠ line on stdout and WARN on stderr"""
        for label, area, files, warning in TYPE_MISMATCH_CASES:
            with self.subTest(label), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                build_fixture(root)
                box = root / "harness/playtest" / area / "7656099/20260912_990000_x"
                for filename, content in files.items():
                    write_json(box / filename, content)
                result = run_digest(root, self.date, "--max-chars", "0", "--no-archive")
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertIn(f"⚠ {warning}", result.stdout)
                self.assertIn("[digest] WARN", result.stderr)

    def test_fix_result_type_mismatch_is_excluded_and_reported(self):
        self.dated_run("20260912_082000_badpr", {"status": "fixed", "pr_number": "1403",
                                                 "finishedAt": "2026-09-12T10:00:00Z"})
        result = run_digest(self.root, self.date, "--max-chars", "0", "--no-archive")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(f"⚠ {INVALID_RESULT}", result.stdout)
        self.assertIn("[digest] WARN runs.invalidResult=1", result.stderr)

    def _write_raw(self, path: Path, text: str) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

    def test_manifest_broken_or_non_dict_json_is_excluded_and_reported(self):
        """manifest.json が壊れたJSON/非dict（list等）でも既定値へ混ざらず件数に数えて除外する
        A malformed or non-dict manifest.json (e.g. a list) is counted and excluded, never
        treated as a valid record with defaults"""
        for label, text, expected_reason in (
            ("broken json", "{", "JSON解析失敗"),
            ("json array", "[]", "JSONがオブジェクトでない"),
        ):
            with self.subTest(label), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                build_fixture(root)
                box = root / "harness/playtest/reports/7656099/20260912_990000_x"
                write_json(box / "ingest.json", READY_INGEST)
                self._write_raw(box / "manifest.json", text)
                result = run_digest(root, self.date, "--max-chars", "0", "--no-archive")
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertIn(f"⚠ {INVALID_MANIFEST}", result.stdout)
                self.assertIn("[digest] WARN reports.invalidManifest=1", result.stderr)
                self.assertIn(expected_reason, result.stderr)
                self.assertIn("manifest.json", result.stderr)

    def test_record_broken_or_non_dict_json_is_excluded_and_reported(self):
        """record.json が壊れたJSON/非dictでも既定値へ混ざらず件数に数えて除外する
        A malformed or non-dict record.json is counted and excluded, never treated as valid defaults"""
        for label, text, expected_reason in (
            ("broken json", "{", "JSON解析失敗"),
            ("json array", "[]", "JSONがオブジェクトでない"),
        ):
            with self.subTest(label), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                build_fixture(root)
                box = root / "harness/playtest/progress/7656099/20260912_990000_x"
                write_json(box / "ingest.json", READY_INGEST)
                self._write_raw(box / "record.json", text)
                result = run_digest(root, self.date, "--max-chars", "0", "--no-archive")
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertIn(f"⚠ {INVALID_RECORD}", result.stdout)
                self.assertIn("[digest] WARN progress.invalidRecord=1", result.stderr)
                self.assertIn(expected_reason, result.stderr)
                self.assertIn("record.json", result.stderr)

    def test_fix_result_broken_or_non_dict_json_is_excluded_and_reported(self):
        """fix-result.json が壊れたJSON/非dictでも既定値へ混ざらず件数に数えて除外する
        A malformed or non-dict fix-result.json is counted and excluded, never treated as valid defaults"""
        for label, text, expected_reason in (
            ("broken json", "{", "JSON解析失敗"),
            ("json array", "[]", "JSONがオブジェクトでない"),
        ):
            with self.subTest(label), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                build_fixture(root)
                path = root / "harness/bug-report/runs/20260912_083000_badraw/fix-result.json"
                self._write_raw(path, text)
                result = run_digest(root, self.date, "--max-chars", "0", "--no-archive")
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertIn(f"⚠ {INVALID_RESULT}", result.stdout)
                self.assertIn("[digest] WARN runs.invalidResult=1", result.stderr)
                self.assertIn(expected_reason, result.stderr)
                self.assertIn("badraw/fix-result.json", result.stderr)

    def test_load_reports_counts_broken_ingest_json(self):
        broken = self.root / "harness/playtest/reports/7656009/20260912_990000_broken"
        broken.mkdir(parents=True)
        (broken / "ingest.json").write_text("{broken", encoding="utf-8")
        reports, stats = dc.load_reports(self.root / "harness/playtest/reports", self.date)
        self.assertEqual(len(reports), 4)
        self.assertEqual(stats["unreadable"], 1)

    def test_load_reports_counts_ready_at_fallback(self):
        fb = self.root / "harness/playtest/reports/7656010/20260912_991000_noready"
        write_json(fb / "ingest.json", {"kind": "report", "steamId": "7656010",
                                        "id": "20260912_991000_noready", "ingestedAt": "2026-09-12T05:20:00Z"})
        write_json(fb / "manifest.json", {"kind": "bug", "description": "readyAt無し"})
        reports, stats = dc.load_reports(self.root / "harness/playtest/reports", self.date)
        self.assertEqual(len(reports), 5)
        self.assertEqual(stats["readyAtFallback"], 1)

    def test_load_fix_results_falls_back_on_missing_finished_at(self):
        self.dated_run("20260912_080000_nofinished", {"status": "fixed", "pr_number": 1401})
        runs, stats = dc.load_fix_results(self.root / "harness/bug-report/runs", self.date)
        self.assertEqual(stats["finishedAtFallback"], 1)
        self.assertEqual({r["id"] for r in runs}, {"20260910_090000_bug0", "20260912_080000_nofinished"})

    def test_load_fix_results_falls_back_on_unparseable_finished_at(self):
        """7桁小数の finishedAt は python3.9 の fromisoformat が拒否するため mtime へ落とす
        A 7-digit-fraction finishedAt is rejected by python3.9's fromisoformat and falls back to mtime"""
        self.dated_run("20260912_081000_weird", {"status": "fixed", "pr_number": 1402,
                                                 "finishedAt": "2026-09-12T10:00:00.1234567Z"})
        runs, stats = dc.load_fix_results(self.root / "harness/bug-report/runs", self.date)
        self.assertEqual(stats["finishedAtFallback"], 1)
        self.assertIn("20260912_081000_weird", {r["id"] for r in runs})


if __name__ == "__main__":
    unittest.main()
