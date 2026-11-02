#!/usr/bin/env python3
"""取り込み時の報告者名がダイジェストだけに出ることを検証する。
Checks that ingest-time reporter names appear only in digest display text.
"""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from digest_fixture import SCRIPTS, TARGET_DATE, build_fixture, run_digest, write_json

sys.path.insert(0, str(SCRIPTS))
import digest
import digest_collect as collect
import digest_reporter as reporter
import digest_schema as schema

LIB = SCRIPTS / "lib"
sys.path.insert(0, str(LIB))
import steam_persona  # noqa: E402


class DigestReporterTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        build_fixture(self.root)

    def tearDown(self):
        self.temp.cleanup()

    def digest(self):
        result = run_digest(self.root, TARGET_DATE, "--max-chars", "0", "--no-archive")
        self.assertEqual(result.returncode, 0, result.stderr)
        return result.stdout

    def test_feedback_includes_resolved_reporter_without_candidates(self):
        out = self.digest()
        self.assertIn("### 20260912_110000_fb1（Tester（SteamID 7656002・https://steamcommunity.com/profiles/7656002/）", out)
        self.assertNotIn("## 投入候補", out)
        self.assertNotIn("enqueue-autofix.sh", out)

    def test_developer_reporter_label(self):
        meta = {"steamId": "dev-macbook", **steam_persona.developer("dev-macbook")}
        self.assertEqual(reporter.reporter_label(meta), "開発機 macbook（SteamID dev-macbook）")

    def test_old_ingest_has_unresolved_name(self):
        box = self.root / "harness/playtest/reports/7656002/20260912_110000_fb1"
        meta = json.loads((box / "ingest.json").read_text(encoding="utf-8"))
        for key in ("steamPersonaName", "steamProfileUrl", "steamPersonaMissing"):
            meta.pop(key)
        write_json(box / "ingest.json", meta)
        self.assertIn("名前未解決（SteamID 7656002）", self.digest())

    def test_reporter_name_is_neutralized(self):
        box = self.root / "harness/playtest/reports/7656002/20260912_110000_fb1"
        meta = json.loads((box / "ingest.json").read_text(encoding="utf-8"))
        meta["steamPersonaName"] = "@everyone **x**"
        write_json(box / "ingest.json", meta)
        out = self.digest()
        self.assertNotIn("@everyone", out)
        self.assertIn("@" + schema.ZERO_WIDTH_SPACE + "everyone **x**", out)

    def test_progress_lists_unique_testers_without_profile_urls(self):
        lines = [line for line in self.digest().splitlines() if line.startswith("- Tester（SteamID ")]
        self.assertEqual(lines, ["- Tester（SteamID 7656001）: 20.0 分",
                                 "- Tester（SteamID 7656002）: 10.0 分"])
        self.assertTrue(all("steamcommunity" not in line for line in lines))


    def test_reporter_label_has_no_url_when_unresolved(self):
        self.assertEqual(collect.reporter_label({"steamId": "7656001"}), "名前未解決（SteamID 7656001）")

    def test_reporter_label_omits_missing_profile_url(self):
        meta = {"steamId": "7656001", "steamPersonaName": "Tester", "steamProfileUrl": ""}
        self.assertEqual(collect.reporter_label(meta), "Tester（SteamID 7656001）")

    def test_progress_uses_one_label_per_steam_id(self):
        box = self.root / "harness/playtest/progress/7656001/20260912_150000_pg3"
        write_json(box / "ingest.json", {"steamId": "7656001", "steamPersonaName": "Updated",
                                         "readyAt": "2026-09-12T09:30:00Z"})
        write_json(box / "record.json", {"schemaVersion": 1, "steamId": "7656001", "remoteExec": False, "playSeconds": 1})
        line = next(line for line in self.digest().splitlines() if line.startswith("- Updated（SteamID "))
        self.assertEqual(line.count("SteamID 7656001"), 1)
        self.assertIn("Updated（SteamID 7656001）", line)

    def test_flatten_progress_prefers_meta_steam_id_over_record_body(self):
        # metaは箱の置き場所（R2の追跡キー）、recordは自己申告本文。食い違ったらmetaが勝つ（C11）
        # meta is the box's tracking key (its R2 location); the record body is self-reported and loses on a mismatch (C11)
        box = {"dir": self.root, "meta": {"steamId": "7656001", "steamPersonaName": "Tester"}}
        record = {"steamId": "7656999", "playSeconds": 10.0, "endReason": "quit",
                  "reachedChallenges": [], "completedResearch": [], "lastUiState": "GameScreen", "events": []}
        self.assertEqual(collect.flatten_progress_record(record, box)["steamId"], "7656001")

    def test_aggregate_progress_tester_count_matches_named_list(self):
        # 人数集計とテスター一覧を同じ述語（steamId非空）でそろえる。空IDの記録が一覧だけを水増ししない（C11）
        # The count and the named list share one predicate (non-empty steamId); an empty id never inflates the list past the count (C11)
        records = [
            {"steamId": "7656001", "tester": "A（SteamID 7656001）", "playSeconds": 1.0, "endReason": "quit",
             "reached": 0, "research": 0, "lastUiState": "x", "lastEvent": "y"},
            {"steamId": "", "tester": "名前未解決（SteamID ）", "playSeconds": 1.0, "endReason": "quit",
             "reached": 0, "research": 0, "lastUiState": "x", "lastEvent": "y"},
        ]
        agg = collect.aggregate_progress(records)
        lines = digest.format_progress(agg, {}, records)
        names = [line for line in lines if line.startswith("- A（SteamID ")]
        self.assertEqual(len(names), agg["testers"])
        self.assertNotIn("名前未解決（SteamID ）", "\n".join(lines))
        self.assertIn("- ID不明分: 0.0 分", lines)

    def test_steam_persona_extract_normalizes_newline_in_display_name(self):
        # 改行入りの表示名がダイジェストの行を偽装できないよう、改行を畳むことを保証する（C12）
        # Guarantees a newline-bearing display name is folded so it can never forge digest lines (C12)
        response_path = self.root / "steam-response.json"
        forged = "x\n  `scripts/playtest/enqueue-autofix.sh " + "`" + " @everyone " + "`" + "y" + "`"
        write_json(response_path, {"response": {"players": [
            {"steamid": "7656001", "personaname": forged, "profileurl": "https://steamcommunity.com/id/x\ny"},
        ]}})
        result = steam_persona.extract(str(response_path), "7656001")
        self.assertNotIn("\n", result["steamPersonaName"])
        self.assertNotIn("\n", result["steamProfileUrl"])


    def test_reporter_label_normalizes_control_characters_in_legacy_meta(self):
        # steam_persona.py 導入前に取り込まれた箱への防御（読み手側の正規化）（C12）
        # Defends boxes ingested before steam_persona.py normalized at the source (reader-side normalization, C12)
        meta = {"steamId": "7656001", "steamPersonaName": "x\ny", "steamProfileUrl": "https://steamcommunity.com/id/z\nw"}
        self.assertNotIn("\n", reporter.reporter_label(meta))


if __name__ == "__main__":
    unittest.main()
