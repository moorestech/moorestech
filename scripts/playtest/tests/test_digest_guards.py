#!/usr/bin/env python3
"""日次ダイジェストの防御（Discord への無害化・record.json の値検証・投入候補の除外件数）を検証する。

Verifies the digest's guards: Discord neutralisation, record.json value checks and the candidate exclusion count.
"""
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from digest_fixture import SCRIPTS, TARGET_DATE, build_fixture, run_digest, write_json  # noqa: E402

sys.path.insert(0, str(SCRIPTS))
import digest_collect as dc  # noqa: E402

ZWSP = "​"
READY_AT = "2026-09-12T09:50:00Z"


class DigestGuardsTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        build_fixture(self.root)

    def tearDown(self):
        self.tmp.cleanup()

    def add_report(self, steam_id: str, box_id: str, manifest: dict, ingest_id: str = "") -> Path:
        box = self.root / "harness/playtest/reports" / steam_id / box_id
        write_json(box / "ingest.json", {"kind": "report", "steamId": steam_id,
                                         "id": ingest_id or box_id, "readyAt": READY_AT})
        write_json(box / "manifest.json", manifest)
        return box

    def digest(self) -> str:
        result = run_digest(self.root, TARGET_DATE, "--max-chars", "0", "--no-archive")
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout

    def test_tester_text_cannot_ping_or_break_code_fences(self):
        """感想全文・投入候補の見出しのメンションとコードフェンスを無害化する
        Mentions and code fences in feedback text and candidate headings are neutralised"""
        self.add_report("7656020", "20260912_170000_fbping",
                        {"kind": "feedback", "description": "@everyone @here <@123> ```\n閉じない"})
        self.add_report("7656021", "20260912_171000_bugping", {"kind": "bug", "description": "<@&456> ```バグ"})
        out = self.digest()
        for raw in ("@everyone", "@here", "<@123>", "<@&456>", "```"):
            self.assertNotIn(raw, out)
        self.assertIn(f"@{ZWSP}everyone", out)
        self.assertIn(f"`{ZWSP}`{ZWSP}`", out)

    def test_backtick_id_gets_no_pasteable_command(self):
        """バッククォート入りの id はコードスパンを閉じてしまうので貼り付けコマンドを出さない
        An id with a backtick would close the code span, so no pasteable command is printed"""
        self.add_report("7656022", "20260912_172000_tick", {"kind": "bug", "description": "x"},
                        ingest_id="a`@everyone`b")
        out = self.digest()
        self.assertIn("バッククォートを含むため貼り付けコマンドを出さない", out)
        self.assertNotIn("enqueue-autofix.sh 7656022", out)
        self.assertNotIn("@everyone", out)

    def test_unreadable_candidate_box_is_logged_and_counted(self):
        """全期間の候補走査で読めない ingest.json・型不一致 manifest を理由付きで数える
        The all-time candidate scan logs and counts an unreadable ingest.json and a mismatched manifest"""
        broken = self.root / "harness/playtest/reports/7656023/20260801_100000_broken"
        broken.mkdir(parents=True)
        (broken / "ingest.json").write_text("{", encoding="utf-8")
        self.add_report("7656024", "20260801_110000_badmanifest", {"kind": ["bug"]})
        result = run_digest(self.root, TARGET_DATE, "--max-chars", "0", "--no-archive")
        self.assertIn("読めず投入候補の判定から除外した箱 2件", result.stdout)
        self.assertIn("投入候補の判定から除外: JSON解析失敗", result.stderr)
        self.assertIn("投入候補の判定から除外: 型不一致: kind", result.stderr)

    def test_invalid_record_values_are_excluded(self):
        """NaN・Infinity・負数の playSeconds と schemaVersion≠1 は平均へ混ぜず除外件数へ
        NaN/Infinity/negative playSeconds and schemaVersion other than 1 are excluded and counted"""
        cases = [("nan", '{"schemaVersion": 1, "playSeconds": NaN}', "playSeconds"),
                 ("inf", '{"schemaVersion": 1, "playSeconds": Infinity}', "playSeconds"),
                 ("negative", '{"schemaVersion": 1, "playSeconds": -5}', "playSeconds"),
                 ("version2", '{"schemaVersion": 2, "playSeconds": 60}', "schemaVersion"),
                 ("noversion", '{"playSeconds": 60}', "schemaVersion")]
        for label, text, field in cases:
            with self.subTest(label), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                build_fixture(root)
                box = root / "harness/playtest/progress/7656025/20260912_180000_x"
                write_json(box / "ingest.json", {"kind": "progress", "steamId": "7656025",
                                                 "id": box.name, "readyAt": READY_AT})
                (box / "record.json").write_text(text, encoding="utf-8")
                records, stats = dc.load_progress(root / "harness/playtest/progress", TARGET_DATE)
                self.assertEqual(stats["invalidRecord"], 1)
                self.assertAlmostEqual(dc.aggregate_progress(records)["meanPlaySeconds"], 900.0)
                result = run_digest(root, TARGET_DATE, "--max-chars", "0", "--no-archive")
                self.assertIn("record.json の型・値が想定外で除外した件数 1件", result.stdout)
                self.assertIn(field, result.stderr)


if __name__ == "__main__":
    unittest.main()
