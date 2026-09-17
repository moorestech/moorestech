#!/usr/bin/env python3
"""日次ダイジェストの集計と出力（正常系）を fixture で検証する。

Verifies the daily digest aggregation and output (happy path) against a fixture tree.
"""
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from digest_fixture import SCRIPTS, TARGET_DATE, build_fixture, run_digest, write_json  # noqa: E402

sys.path.insert(0, str(SCRIPTS))
import digest_candidates as dcand  # noqa: E402
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

    def test_aggregate_progress_excludes_null_play_seconds(self):
        """playSeconds null の記録はセッション数に数えつつ平均の分母から外し、欠落件数を返す
        A record with a null playSeconds still counts as a session but is excluded from the mean, and is reported as missing"""
        pg3 = self.root / "harness/playtest/progress/7656003/20260912_150000_pg3"
        write_json(pg3 / "ingest.json", {"kind": "progress", "steamId": "7656003",
                                         "id": "20260912_150000_pg3", "readyAt": "2026-09-12T09:30:00Z"})
        write_json(pg3 / "record.json", {"schemaVersion": 1, "steamId": "7656003", "playSeconds": None,
                                         "endReason": "quit", "lastUiState": "GameScreen"})
        records, stats = dc.load_progress(self.root / "harness/playtest/progress", self.date)
        self.assertEqual(stats["invalidRecord"], 0)
        agg = dc.aggregate_progress(records)
        self.assertEqual(agg["sessions"], 3)
        self.assertEqual(agg["testers"], 3)
        self.assertEqual(agg["playSecondsMissing"], 1)
        self.assertAlmostEqual(agg["meanPlaySeconds"], 900.0)

    def test_digest_shows_unknown_when_all_play_seconds_missing(self):
        """playSeconds が全件 null の日は平均を 0 と偽らず「不明」と出し、⚠ 行と stderr WARN が出る
        A day where every playSeconds is null prints "unknown" instead of a false 0, with a ⚠ line and stderr WARN"""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for steam_id, box_id in (("9000001", "1"), ("9000002", "2")):
                box = root / "harness/playtest/progress" / steam_id / f"20260912_16{box_id}000_pgnull"
                write_json(box / "ingest.json", {"kind": "progress", "steamId": steam_id,
                                                 "id": box.name, "readyAt": "2026-09-12T10:00:00Z"})
                write_json(box / "record.json", {"schemaVersion": 1, "steamId": steam_id, "playSeconds": None,
                                                 "endReason": "quit", "lastUiState": "GameScreen"})
            result = run_digest(root, self.date, "--max-chars", "0", "--no-archive")
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertNotIn("平均プレイ時間 0.0 分", result.stdout)
            self.assertIn("平均プレイ時間 不明", result.stdout)
            self.assertIn("⚠ playSeconds が欠落し平均から除外したセッション 2件", result.stdout)
            self.assertIn("[digest] WARN progress.playSecondsMissing=2", result.stderr)

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

    def test_digest_lists_pre_target_day_candidate_until_enqueued(self):
        """対象日より前の readyAt を持つ未投入バグは、投入候補として JST 日付付きで出続け、
        件数節（バグN件）には数えず、AUTOFIX_QUEUED を付けると消える
        A pre-target-day un-enqueued bug keeps resurfacing in the candidates section with its
        JST date, is never counted in the "バグN件" tally, and disappears once AUTOFIX_QUEUED is written"""
        old_bug = self.root / "harness/playtest/reports/7656005/20260905_100000_oldbug"
        write_json(old_bug / "ingest.json", {"kind": "report", "steamId": "7656005",
                                             "id": "20260905_100000_oldbug", "readyAt": "2026-09-05T05:00:00Z"})
        write_json(old_bug / "manifest.json", {"kind": "bug", "description": "対象日より前の未投入バグ"})
        candidates = dcand.load_candidate_reports(self.root / "harness/playtest/reports")
        self.assertIn("20260905_100000_oldbug", {c["id"] for c in candidates})
        out = self.run_ok("--max-chars", "0")
        self.assertIn("enqueue-autofix.sh 7656005 20260905_100000_oldbug", out)
        self.assertIn("20260905_100000_oldbug（2026-09-05）", out)
        # 対象日フィルタで拾われないため、対象日のバグ件数節（2件）には混入しない
        # It falls outside the target-day filter, so it never inflates the "バグ2件" tally
        self.assertIn("バグ 2件 / 感想 1件 / クラッシュ 1件", out)
        # bug1 は対象日内かつ未投入 → 候補にも同時に出る
        # bug1 is within the target day and also un-enqueued, so it appears as a candidate too
        self.assertIn("enqueue-autofix.sh 7656001 20260912_100000_bug1", out)
        (old_bug / "AUTOFIX_QUEUED").write_text("queued\n", encoding="utf-8")
        out2 = self.run_ok("--max-chars", "0")
        self.assertNotIn("enqueue-autofix.sh 7656005 20260905_100000_oldbug", out2)

    def test_enqueue_command_id_is_shell_safe(self):
        """id にシェルメタ文字が混じっても、貼り付けたコマンドは注入されず literal として渡る
        A shell-meta-character id in the pasted command never injects; it is passed through literally"""
        malicious = self.root / "harness/playtest/reports/7656006/20260912_160000_evil"
        write_json(malicious / "ingest.json", {"kind": "report", "steamId": "7656006",
                                                "id": "$(touch pwned)", "readyAt": "2026-09-12T09:40:00Z"})
        write_json(malicious / "manifest.json", {"kind": "bug", "description": "injection test"})
        candidates = dcand.load_candidate_reports(self.root / "harness/playtest/reports")
        lines = dcand.format_candidates(candidates)
        cmd_line = next(line for line in lines if "7656006" in line).strip().strip("`")
        with tempfile.TemporaryDirectory() as tmp:
            subprocess.run(["bash", "-c", cmd_line + " 2>/dev/null || true"], cwd=tmp)
            self.assertFalse((Path(tmp) / "pwned").exists())

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
