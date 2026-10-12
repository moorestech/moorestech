"""日付・配信順・無害化の境界を検証する。
Tests date selection, delivery order and neutralization boundaries."""
import io
import json
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from digest_fixture import DISABLED_MARK, SCRIPTS, TARGET_DATE, build_fixture, run_digest, write_json  # noqa: E402

sys.path.insert(0, str(SCRIPTS))
import digest
import digest_collect as collect
from digest_schema import ZERO_WIDTH_SPACE


class PlayerStatsCliTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        build_fixture(self.root)

    def test_main_never_loads_candidates(self):
        output, errors = io.StringIO(), io.StringIO()
        # 同一プロセスで呼び、候補loaderへのアクセスを検出する
        # Invoke in-process so the patch observes candidate-loader access.
        with patch("digest_candidates.load_candidate_reports") as candidates:
            with redirect_stdout(output), redirect_stderr(errors):
                result = digest.main(["--logs", str(self.root), "--date", TARGET_DATE,
                                      "--max-chars", "0", "--no-archive"])
        self.assertEqual(result, 0)
        candidates.assert_not_called()
        out = output.getvalue()
        self.assertNotIn("## 投入候補", out)
        self.assertNotIn("enqueue-autofix.sh", out)
        self.assertEqual(next(line for line in out.splitlines() if line.startswith("## ")),
                         "## 前日のプレイ状況")
        self.assertIn("バグ 2件 / 感想 1件 / クラッシュ 1件", out)
        self.assertIn("受信日（JST）基準。実際に遊んだ日とは異なる場合あり", out)
        self.assertFalse((self.root / "harness/playtest/digests").exists())

    def test_long_feedback_preserves_statistics_and_full_archive(self):
        box = self.root / "harness/playtest/reports/7656002/20260912_110000_fb1"
        write_json(box / "manifest.json", dict(kind="feedback", remoteExec=DISABLED_MARK, description="あ" * 4000))
        result = run_digest(self.root, TARGET_DATE, "--max-chars", "1800")
        self.assertEqual(result.returncode, 0, result.stderr)
        out = result.stdout
        self.assertLessEqual(len(out), 1800)
        self.assertIn("切り詰め", out)
        self.assertIn("digests/2026-09-12.md", out)
        full = (self.root / "harness/playtest/digests/2026-09-12.md").read_text(encoding="utf-8")
        for text in (out, full):
            self.assertEqual(next(line for line in text.splitlines() if line.startswith("## ")),
                             "## 前日のプレイ状況")
            for value in ("- 人数 2 人", "- 合計プレイ時間 30.0 分", "- 1人あたり平均プレイ時間 15.0 分",
                          "- Tester（SteamID 7656001）: 20.0 分", "- Tester（SteamID 7656002）: 10.0 分"):
                self.assertIn(value, text)
        for value in ("あ" * 4000, "セッション 2 件", "平均研究完了数 0.5", "3-5 1件", "0 1件",
                      "buildModeCancelled 2件", "GameScreen 1件", "InventoryScreen 1件",
                      "quit 1件", "crash-recovered 1件", "## 自動修正ラン", "#1400", "ベルト停止を修正"):
            self.assertIn(value, full)
        self.assertLess(full.index("## 前日のプレイ状況"), full.index("## 進行記録"))
        self.assertLess(full.index("## 進行記録"), full.index("## 感想（全文）"))

    def test_missing_unknown_and_hostile_text_warn_and_neutralize(self):
        progress = self.root / "harness/playtest/progress/7656001/20260912_130000_pg1"
        meta = json.loads((progress / "ingest.json").read_text())
        write_json(progress / "ingest.json", dict(meta, steamId="", steamPersonaName="@everyone ```"))
        write_json(progress / "record.json", dict(schemaVersion=1, steamId="", remoteExec=False, playSeconds=None))
        named = self.root / "harness/playtest/progress/7656002/20260912_140000_pg2/ingest.json"
        write_json(named, dict(json.loads(named.read_text()), steamPersonaName="@here ```"))
        feedback = self.root / "harness/playtest/reports/7656002/20260912_110000_fb1/manifest.json"
        forged = "  `scripts/playtest/enqueue-autofix.sh @everyone x`"
        write_json(feedback, dict(kind="feedback", remoteExec=DISABLED_MARK,
                                 description="@everyone ```\n" + forged))
        result = run_digest(self.root, TARGET_DATE, "--max-chars", "0")
        self.assertEqual(result.returncode, 0, result.stderr)
        full = (self.root / "harness/playtest/digests/2026-09-12.md").read_text(encoding="utf-8")
        self.assertEqual(result.stdout, full)
        self.assertIn("- 人数 1 人", full)
        self.assertIn("- 合計プレイ時間 既知分 10.0 分（一部不明）", full)
        self.assertIn("- ID不明分: 不明", full)
        self.assertIn("- 1人あたり平均プレイ時間 不明（時間欠損/ID不明）", full)
        self.assertIn("欠損を含むため平均は不明", full)
        self.assertIn("ID不明のセッション 1件", full)
        self.assertIn("WARN progress.playSecondsMissing=1", result.stderr)
        self.assertIn("WARN progress.unknownIdSessions=1", result.stderr)
        for raw in ("@everyone", "@here", "```", forged):
            self.assertNotIn(raw, full)
        self.assertIn("@" + ZERO_WIDTH_SPACE + "everyone", full)
        self.assertIn("@" + ZERO_WIDTH_SPACE + "here", full)

    def test_jst_boundary_ready_at_priority_and_fallback(self):
        # 独立した箱集合で受信日を検証し、セッション日時は無関係とする
        # Verify reception dates in isolated boxes, independent of session timestamps.
        root = self.root / "date-cases"
        cases = [("before", "2026-09-12T14:59:59Z", "2026-09-13T01:00:00Z"),
                 ("after", "2026-09-12T15:00:00Z", "2026-09-12T01:00:00Z"),
                 ("fallback", "", "2026-09-12T14:59:59Z")]
        for sid, ready, ingested in cases:
            box = root / sid / "box"
            write_json(box / "ingest.json", dict(steamId=sid, readyAt=ready, ingestedAt=ingested))
            write_json(box / "record.json", dict(schemaVersion=1, steamId=sid, remoteExec=False,
                       playSeconds=60, sessionStart="2000-01-01T00:00:00Z", totalPlaySeconds=999999))
        errors = io.StringIO()
        with redirect_stderr(errors):
            records, stats = collect.load_progress(root, TARGET_DATE)
        self.assertEqual({record["steamId"] for record in records}, {"before", "fallback"})
        self.assertEqual(stats["readyAtFallback"], 1)
        self.assertIn("readyAt が無く ingestedAt で日付判定", errors.getvalue())
        self.assertEqual(collect.aggregate_progress(records)["playTime"]["knownSeconds"], 120)
        records, stats = collect.load_progress(root, "2026-09-13")
        self.assertEqual([record["steamId"] for record in records], ["after"])
        self.assertEqual(stats["readyAtFallback"], 0)
