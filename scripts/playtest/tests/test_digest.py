#!/usr/bin/env python3
"""日次ダイジェストの集計と出力を fixture で検証する。

Verifies the daily digest aggregation and output against a fixture tree.
"""
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(SCRIPTS))
import digest_collect as dc  # noqa: E402


def write_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data), encoding="utf-8")


def build_fixture(root: Path) -> None:
    """前日(2026-09-12 JST)分と対象外の日を1件ずつ混ぜた木を作る
    Builds a tree with boxes for the target day (2026-09-12 JST) plus one out-of-range box"""
    pt = root / "harness" / "playtest"
    bug = pt / "reports" / "7656001" / "20260912_100000_bug1"
    write_json(bug / "ingest.json", {"kind": "report", "steamId": "7656001",
                                     "id": "20260912_100000_bug1", "readyAt": "2026-09-12T05:00:00Z",
                                     "ingestedAt": "2026-09-12T05:01:00Z"})
    write_json(bug / "manifest.json", {"kind": "bug", "description": "ベルトが止まる\n2個目から",
                                       "buildInfo": {"steamBuildLabel": "playtest-20260912-1730"}})
    bug2 = pt / "reports" / "7656001" / "20260912_101000_bug2"
    write_json(bug2 / "ingest.json", {"kind": "report", "steamId": "7656001",
                                      "id": "20260912_101000_bug2", "readyAt": "2026-09-12T05:10:00Z",
                                      "ingestedAt": "2026-09-12T05:11:00Z"})
    write_json(bug2 / "manifest.json", {"kind": "bug", "description": "投入済みのバグ"})
    (bug2 / "AUTOFIX_QUEUED").write_text("queued at 2026-09-12T06:00:00Z\n", encoding="utf-8")
    fb = pt / "reports" / "7656002" / "20260912_110000_fb1"
    write_json(fb / "ingest.json", {"kind": "report", "steamId": "7656002",
                                    "id": "20260912_110000_fb1", "readyAt": "2026-09-12T06:00:00Z",
                                    "ingestedAt": "2026-09-12T06:01:00Z"})
    write_json(fb / "manifest.json", {"kind": "feedback", "description": "序盤の歩きが長い",
                                      "buildInfo": {"steamBuildLabel": "playtest-20260912-1730"}})
    cr = pt / "reports" / "7656003" / "20260912_120000_cr1"
    write_json(cr / "ingest.json", {"kind": "report", "steamId": "7656003",
                                    "id": "20260912_120000_cr1", "readyAt": "2026-09-12T07:00:00Z",
                                    "ingestedAt": "2026-09-12T07:01:00Z"})
    write_json(cr / "manifest.json", {"kind": "crash", "description": ""})
    old = pt / "reports" / "7656004" / "20260901_100000_old1"
    write_json(old / "ingest.json", {"kind": "report", "steamId": "7656004",
                                     "id": "20260901_100000_old1", "readyAt": "2026-09-01T05:00:00Z",
                                     "ingestedAt": "2026-09-01T05:01:00Z"})
    write_json(old / "manifest.json", {"kind": "feedback", "description": "対象外の日"})

    pg1 = pt / "progress" / "7656001" / "20260912_130000_pg1"
    write_json(pg1 / "ingest.json", {"kind": "progress", "steamId": "7656001",
                                     "id": "20260912_130000_pg1", "readyAt": "2026-09-12T08:00:00Z",
                                     "ingestedAt": "2026-09-12T08:01:00Z"})
    write_json(pg1 / "record.json", {"schemaVersion": 1, "steamId": "7656001", "playSeconds": 1200.0,
                                     "endReason": "quit", "reachedChallenges": ["a", "b", "c"],
                                     "completedResearch": ["r1"], "lastUiState": "GameScreen",
                                     "events": [{"type": "challengeCompleted"}, {"type": "buildModeCancelled"}]})
    pg2 = pt / "progress" / "7656002" / "20260912_140000_pg2"
    write_json(pg2 / "ingest.json", {"kind": "progress", "steamId": "7656002",
                                     "id": "20260912_140000_pg2", "readyAt": "2026-09-12T09:00:00Z",
                                     "ingestedAt": "2026-09-12T09:01:00Z"})
    write_json(pg2 / "record.json", {"schemaVersion": 1, "steamId": "7656002", "playSeconds": 600.0,
                                     "endReason": "crash-recovered", "reachedChallenges": [],
                                     "completedResearch": [], "lastUiState": "InventoryScreen",
                                     "events": [{"type": "buildModeCancelled"}]})

    run = root / "harness" / "bug-report" / "runs" / "20260910_090000_bug0"
    write_json(run / "fix-result.json", {"status": "fixed", "pr_number": 1400, "base": "master",
                                         "determinism": "ok", "summary": "ベルト停止を修正",
                                         "finishedAt": "2026-09-12T10:00:00Z"})


class DigestTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        build_fixture(self.root)
        self.date = "2026-09-12"

    def tearDown(self):
        self.tmp.cleanup()

    def test_jst_date_converts_utc(self):
        self.assertEqual(dc.jst_date("2026-09-12T15:30:00Z"), "2026-09-13")
        self.assertEqual(dc.jst_date(""), "")

    def test_load_reports_filters_by_ready_at(self):
        reports = dc.load_reports(self.root / "harness/playtest/reports", self.date)
        self.assertEqual({r["kind"] for r in reports}, {"bug", "feedback", "crash"})
        self.assertEqual(len(reports), 4)

    def test_load_reports_marks_queued(self):
        reports = {r["id"]: r for r in dc.load_reports(
            self.root / "harness/playtest/reports", self.date)}
        self.assertFalse(reports["20260912_100000_bug1"]["queued"])
        self.assertTrue(reports["20260912_101000_bug2"]["queued"])

    def test_aggregate_progress(self):
        records = dc.load_progress(self.root / "harness/playtest/progress", self.date)
        agg = dc.aggregate_progress(records)
        self.assertEqual(agg["testers"], 2)
        self.assertEqual(agg["sessions"], 2)
        self.assertAlmostEqual(agg["meanPlaySeconds"], 900.0)
        self.assertEqual(agg["reachBuckets"]["3-5"], 1)
        self.assertEqual(agg["reachBuckets"]["0"], 1)
        self.assertEqual(agg["lastEvents"]["buildModeCancelled"], 2)
        self.assertEqual(agg["lastUiStates"]["GameScreen"], 1)

    def test_load_fix_results_uses_finished_at(self):
        runs, fallback = dc.load_fix_results(self.root / "harness/bug-report/runs", self.date)
        self.assertEqual(len(runs), 1)
        self.assertEqual(runs[0]["prNumber"], 1400)
        self.assertEqual(fallback, 0)

    def run_digest(self, *extra):
        cmd = [sys.executable, str(SCRIPTS / "digest.py"), "--date", self.date,
               "--logs", str(self.root), *extra]
        return subprocess.run(cmd, capture_output=True, text=True, check=True).stdout

    def test_digest_sections(self):
        out = self.run_digest("--max-chars", "0")
        self.assertIn("# moorestech プレイテスト日次ダイジェスト 2026-09-12", out)
        self.assertIn("バグ 2件 / 感想 1件 / クラッシュ 1件", out)
        self.assertIn("序盤の歩きが長い", out)
        self.assertNotIn("対象外の日", out)
        self.assertIn("人数 2 人", out)
        self.assertIn("平均プレイ時間 15.0 分", out)
        self.assertIn("buildModeCancelled 2件", out)
        self.assertIn("#1400", out)
        self.assertTrue((self.root / "harness/playtest/digests/2026-09-12.md").is_file())

    def test_digest_lists_enqueue_candidates(self):
        """投入候補は未投入のバグだけ。コマンドはそのまま貼れる形で出る
        Only un-enqueued bugs are listed, with a copy-pastable command"""
        out = self.run_digest("--max-chars", "0")
        self.assertIn("## 投入候補のバグ報告", out)
        self.assertIn("enqueue-autofix.sh 7656001 20260912_100000_bug1", out)
        self.assertIn("ベルトが止まる", out)
        self.assertNotIn("enqueue-autofix.sh 7656001 20260912_101000_bug2", out)
        self.assertNotIn("投入済みのバグ", out)

    def test_digest_truncates_and_points_at_archive(self):
        long_box = self.root / "harness/playtest/reports/7656005/20260912_150000_fb2"
        write_json(long_box / "ingest.json", {"kind": "report", "steamId": "7656005",
                                              "id": "20260912_150000_fb2", "readyAt": "2026-09-12T09:30:00Z",
                                              "ingestedAt": "2026-09-12T09:31:00Z"})
        write_json(long_box / "manifest.json", {"kind": "feedback", "description": "あ" * 4000})
        out = self.run_digest("--max-chars", "600")
        self.assertLessEqual(len(out), 700)
        self.assertIn("digests/2026-09-12.md", out)

    def test_empty_day_still_prints_headings(self):
        out = subprocess.run([sys.executable, str(SCRIPTS / "digest.py"), "--date", "2026-01-01",
                              "--logs", str(self.root)], capture_output=True, text=True,
                             check=True).stdout
        self.assertIn("# moorestech プレイテスト日次ダイジェスト 2026-01-01", out)
        self.assertIn("## 進行記録", out)
        self.assertIn("なし", out)


if __name__ == "__main__":
    unittest.main()
