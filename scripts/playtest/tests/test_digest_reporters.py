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
import digest_candidates as dcand
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

    def test_feedback_and_candidate_include_resolved_reporter(self):
        out = self.digest()
        label = "Tester（SteamID 7656001・https://steamcommunity.com/profiles/7656001/）"
        self.assertIn("### 20260912_110000_fb1（Tester（SteamID 7656002・https://steamcommunity.com/profiles/7656002/）", out)
        self.assertIn(f"20260912_100000_bug1（2026-09-12・{label}）", out)
        command = next(line for line in out.splitlines() if "enqueue-autofix.sh 7656001" in line)
        self.assertNotIn("Tester", command)
        self.assertNotIn("steamcommunity", command)

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
        out = self.digest()
        line = next(line for line in out.splitlines() if line.startswith("- テスター: "))
        self.assertIn("Tester（SteamID 7656001）", line)
        self.assertIn("Tester（SteamID 7656002）", line)
        self.assertNotIn("steamcommunity", line)

    def test_digest_lists_pre_target_day_candidate_until_enqueued(self):
        """対象日より前の readyAt を持つ未投入バグは、投入候補として JST 日付付きで出続け、
        件数節（バグN件）には数えず、AUTOFIX_QUEUED を付けると消える
        A pre-target-day un-enqueued bug keeps resurfacing in the candidates section with its
        JST date, is never counted in the "バグN件" tally, and disappears once AUTOFIX_QUEUED is written"""
        old_bug = self.root / "harness/playtest/reports/7656005/20260905_100000_oldbug"
        write_json(old_bug / "ingest.json", {"kind": "report", "steamId": "7656005",
                                             "id": "20260905_100000_oldbug", "readyAt": "2026-09-05T05:00:00Z"})
        write_json(old_bug / "manifest.json", {"kind": "bug", "description": "対象日より前の未投入バグ"})
        candidates, _stats = dcand.load_candidate_reports(self.root / "harness/playtest/reports")
        self.assertIn("20260905_100000_oldbug", {c["id"] for c in candidates})
        out = self.digest()
        self.assertIn("enqueue-autofix.sh 7656005 20260905_100000_oldbug", out)
        self.assertIn("20260905_100000_oldbug（2026-09-05・名前未解決（SteamID 7656005））", out)
        # 対象日フィルタで拾われないため、対象日のバグ件数節（2件）には混入しない
        # It falls outside the target-day filter, so it never inflates the "バグ2件" tally
        self.assertIn("バグ 2件 / 感想 1件 / クラッシュ 1件", out)
        # bug1 は対象日内かつ未投入 → 候補にも同時に出る
        # bug1 is within the target day and also un-enqueued, so it appears as a candidate too
        self.assertIn("enqueue-autofix.sh 7656001 20260912_100000_bug1", out)
        (old_bug / "AUTOFIX_QUEUED").write_text("queued\n", encoding="utf-8")
        out2 = self.digest()
        self.assertNotIn("enqueue-autofix.sh 7656005 20260905_100000_oldbug", out2)

    def test_reporter_label_has_no_url_when_unresolved(self):
        self.assertEqual(collect.reporter_label({"steamId": "7656001"}), "名前未解決（SteamID 7656001）")

    def test_reporter_label_omits_missing_profile_url(self):
        meta = {"steamId": "7656001", "steamPersonaName": "Tester", "steamProfileUrl": ""}
        self.assertEqual(collect.reporter_label(meta), "Tester（SteamID 7656001）")

    def test_progress_uses_one_label_per_steam_id(self):
        box = self.root / "harness/playtest/progress/7656001/20260912_150000_pg3"
        write_json(box / "ingest.json", {"steamId": "7656001", "steamPersonaName": "Updated",
                                         "readyAt": "2026-09-12T09:30:00Z"})
        write_json(box / "record.json", {"schemaVersion": 1, "steamId": "7656001", "playSeconds": 1})
        line = next(line for line in self.digest().splitlines() if line.startswith("- テスター: "))
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
        line = next(entry for entry in lines if entry.startswith("- テスター: "))
        names = line[len("- テスター: "):].split("、")
        self.assertEqual(len(names), agg["testers"])
        self.assertNotIn("名前未解決（SteamID ）", line)

    def test_steam_persona_extract_normalizes_newline_in_display_name(self):
        # 改行入りの表示名が、貼り付けコマンドの退避正規表現を偽装できないことを保証する（C12）
        # Guarantees a newline-bearing display name can never forge the pasteable-command stash regex (C12)
        response_path = self.root / "steam-response.json"
        forged = "x\n  `scripts/playtest/enqueue-autofix.sh " + "`" + " @everyone " + "`" + "y" + "`"
        write_json(response_path, {"response": {"players": [
            {"steamid": "7656001", "personaname": forged, "profileurl": "https://steamcommunity.com/id/x\ny"},
        ]}})
        result = steam_persona.extract(str(response_path), "7656001")
        self.assertNotIn("\n", result["steamPersonaName"])
        self.assertNotIn("\n", result["steamProfileUrl"])
        self.assertIsNone(schema.PASTEABLE_COMMAND_RE.search("  `" + result["steamPersonaName"] + "`"))

    def test_pasteable_command_regex_rejects_embedded_backtick(self):
        # 生成側の形（値にバッククォートを含まない）だけに一致し、偽装した行を退避対象にしない（C12）
        # Matches only the generator's own shape (no backtick inside the value); a forged line is never stashed (C12)
        forged = "  `scripts/playtest/enqueue-autofix.sh `@everyone`y`"
        self.assertIsNone(schema.PASTEABLE_COMMAND_RE.fullmatch(forged))

    def test_reporter_label_normalizes_control_characters_in_legacy_meta(self):
        # steam_persona.py 導入前に取り込まれた箱への防御（読み手側の正規化）（C12）
        # Defends boxes ingested before steam_persona.py normalized at the source (reader-side normalization, C12)
        meta = {"steamId": "7656001", "steamPersonaName": "x\ny", "steamProfileUrl": "https://steamcommunity.com/id/z\nw"}
        self.assertNotIn("\n", reporter.reporter_label(meta))


if __name__ == "__main__":
    unittest.main()
