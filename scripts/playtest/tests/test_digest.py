#!/usr/bin/env python3
"""日次ダイジェストの集計と出力（正常系）を fixture で検証する。

Verifies the daily digest aggregation and output (happy path) against a fixture tree.
"""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from digest_fixture import SCRIPTS, TARGET_DATE, build_fixture, run_digest, write_json  # noqa: E402

sys.path.insert(0, str(SCRIPTS))
import digest_collect as dc  # noqa: E402


class DigestTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        build_fixture(self.root)
        self.date = TARGET_DATE

    def tearDown(self):
        self.tmp.cleanup()

    def run_ok(self, *extra):
        result = run_digest(self.root, self.date, *extra)
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout

    def test_jst_date_converts_utc(self):
        self.assertEqual(dc.jst_date("2026-09-12T15:30:00Z"), "2026-09-13")
        self.assertEqual(dc.jst_date(""), "")

    def test_load_reports_filters_by_ready_at(self):
        reports, stats = dc.load_reports(self.root / "harness/playtest/reports", self.date)
        self.assertEqual({r["kind"] for r in reports}, {"bug", "feedback", "crash"})
        self.assertEqual(len(reports), 4)
        self.assertEqual(stats, {"unreadable": 0, "readyAtFallback": 0, "invalidManifest": 0})

    def test_load_reports_marks_queued(self):
        reports, _stats = dc.load_reports(self.root / "harness/playtest/reports", self.date)
        by_id = {r["id"]: r for r in reports}
        self.assertFalse(by_id["20260912_100000_bug1"]["queued"])
        self.assertTrue(by_id["20260912_101000_bug2"]["queued"])

    def test_aggregate_progress(self):
        records, stats = dc.load_progress(self.root / "harness/playtest/progress", self.date)
        self.assertEqual(stats["invalidRecord"], 0)
        agg = dc.aggregate_progress(records)
        self.assertEqual(agg["testers"], 2)
        self.assertEqual(agg["sessions"], 2)
        self.assertAlmostEqual(agg["meanPlaySeconds"], 900.0)
        self.assertEqual(agg["reachBuckets"]["3-5"], 1)
        self.assertEqual(agg["reachBuckets"]["0"], 1)
        self.assertEqual(agg["lastEvents"]["buildModeCancelled"], 2)
        self.assertEqual(agg["lastUiStates"]["GameScreen"], 1)

    def test_load_fix_results_uses_finished_at(self):
        runs, stats = dc.load_fix_results(self.root / "harness/bug-report/runs", self.date)
        self.assertEqual(len(runs), 1)
        self.assertEqual(runs[0]["prNumber"], 1400)
        self.assertEqual(stats, {"finishedAtFallback": 0, "invalidResult": 0})

    def test_digest_sections(self):
        out = self.run_ok("--max-chars", "0")
        self.assertIn("# moorestech プレイテスト日次ダイジェスト 2026-09-12", out)
        self.assertIn("バグ 2件 / 感想 1件 / クラッシュ 1件", out)
        self.assertIn("序盤の歩きが長い", out)
        self.assertNotIn("対象外の日", out)
        self.assertIn("人数 2 人", out)
        self.assertIn("平均プレイ時間 15.0 分", out)
        self.assertIn("buildModeCancelled 2件", out)
        self.assertIn("#1400", out)
        self.assertNotIn("⚠", out)
        self.assertTrue((self.root / "harness/playtest/digests/2026-09-12.md").is_file())

    def test_digest_lists_enqueue_candidates(self):
        """投入候補は未投入のバグだけ。コマンドはそのまま貼れる形で出る
        Only un-enqueued bugs are listed, with a copy-pastable command"""
        out = self.run_ok("--max-chars", "0")
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
        out = self.run_ok("--max-chars", "600")
        self.assertLessEqual(len(out), 700)
        self.assertIn("digests/2026-09-12.md", out)

    def test_empty_day_still_prints_headings(self):
        self.date = "2026-01-01"
        out = self.run_ok()
        self.assertIn("# moorestech プレイテスト日次ダイジェスト 2026-01-01", out)
        self.assertIn("## 進行記録", out)
        self.assertIn("なし", out)


if __name__ == "__main__":
    unittest.main()
