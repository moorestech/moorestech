# .claude/skills/moores-code-review/tests/test_applied_diff_checks.py
# 反映 diff の機械的動作確認（録画シナリオの前後比較コンパイル・セーブ往復テスト選択）の回帰テスト。
# 2026-10-07 Codex 指摘の4件（複数行シグネチャ・名前を含まない型不一致・追従後に対象から外れる・別型への移動）を
# 名前 grep をやめた前後比較で構造的に捕まえることを固定する。uloop は fake_uloop.py が代役を務める。
# Regression tests for the applied-diff mechanical checks: before/after scenario compile and save/load test selection.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parent.parent
SCRIPT = SKILL_DIR / "scripts/applied_diff_checks.py"
FAKE = Path(__file__).resolve().parent / "fake_uloop.py"
sys.path.insert(0, str(SKILL_DIR / "scripts"))
import scenario_compile  # noqa: E402
SCENARIO_DIR = ".agents/skills/unity-playmode-recorded-playtest/scenarios/misc"
SCENARIO = """// 説明
using Client.Playtest;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("x", options, async p =>
{
    FooService.DoThing(1);
});
"""
SRC = "moorestech_client/Assets/Scripts/Client.Game/FooService.cs"


def diff_for(path: str, removed: list, added: list) -> str:
    body = "".join(f"-{l}\n" for l in removed) + "".join(f"+{l}\n" for l in added)
    return f"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n@@ -1 +1 @@\n{body}"


class AppliedDiffChecksTest(unittest.TestCase):
    def setUp(self):
        self._td = tempfile.TemporaryDirectory()
        self.root = Path(self._td.name)
        (self.root / SCENARIO_DIR).mkdir(parents=True)
        self.write_scenario(SCENARIO)
        (self.root / "moorestech_client").mkdir()
        wrapper = self.root / "uloop"
        wrapper.write_text(f'#!/bin/sh\nexec "{sys.executable}" "{FAKE}" "$@"\n', encoding="utf-8")
        wrapper.chmod(0o755)
        self.env = dict(os.environ, MOORES_REVIEW_ULOOP=str(wrapper))

    def tearDown(self):
        self._td.cleanup()

    def write_scenario(self, text: str, name: str = "foo.cs"):
        (self.root / SCENARIO_DIR / name).write_text(text, encoding="utf-8")

    def set_api(self, api: dict | None):
        path = self.root / "fake_api.json"
        if api is None:
            path.unlink(missing_ok=True)
        else:
            path.write_text(json.dumps(api), encoding="utf-8")

    def run_cli(self, *args) -> dict:
        run = subprocess.run([sys.executable, str(SCRIPT), *args, "--repo-root", str(self.root),
                              "--run-dir", str(self.root / "run")], capture_output=True, text=True, env=self.env)
        self.assertEqual(run.returncode, 0, run.stderr)
        return json.loads(run.stdout)

    def check(self, diff_text: str) -> dict:
        (self.root / "applied.diff").write_text(diff_text, encoding="utf-8")
        return self.run_cli("check", str(self.root / "applied.diff"), "--from", "s0", "--to", "s1")

    def record_before(self, api: dict):
        self.set_api(api)
        return self.run_cli("record", "--name", "s0")

    def test_multiline_signature_type_change_is_caught_without_name_in_message(self):
        self.record_before({"FooService.DoThing": ["int"]})
        self.set_api({"FooService.DoThing": ["string"]})
        out = self.check(diff_for(SRC, ["        int value)"], ["        string value)"]))["scenarios"]
        self.assertEqual(out["status"], "new_errors")
        self.assertEqual([d["code"] for d in out["new"]], ["CS1503"])
        self.assertNotIn("DoThing", out["new"][0]["message"], "名前を含まない診断でも増分として捕まえる")

    def test_followed_scenario_stays_in_scope_after_rename(self):
        self.record_before({"FooService.DoThing": ["int"]})
        self.set_api({"FooService.DoThingRenamed": ["int"]})
        rename = diff_for(SRC, ["    public static void DoThing(int v)"], ["    public static void DoThingRenamed(int v)"])
        self.assertEqual(self.check(rename)["scenarios"]["status"], "new_errors")
        # 追従を誤ったシナリオも、改名後の名前しか含まなくなったシナリオも、全件コンパイルの対象に残る
        # A wrong follow-up is still compiled even though the scenario no longer mentions the old name
        self.write_scenario(SCENARIO.replace("DoThing(1)", "DoThingRenamd(1)"))
        self.assertEqual(self.check(rename)["scenarios"]["status"], "new_errors")
        self.write_scenario(SCENARIO.replace("DoThing(1)", "DoThingRenamed(1)"))
        out = self.check(rename)["scenarios"]
        self.assertEqual((out["status"], out["new"], out["compiled"]), ("ok", [], 1))

    def test_move_to_another_type_is_caught(self):
        self.record_before({"FooService.DoThing": ["int"]})
        self.set_api({"BarService.DoThing": ["int"]})
        moved = (diff_for(SRC, ["    public static void DoThing(int v)"], [])
                 + diff_for(SRC.replace("Foo", "Bar"), [], ["    public static void DoThing(int v)"]))
        out = self.check(moved)["scenarios"]
        self.assertEqual(out["status"], "new_errors")
        self.assertEqual(out["new"][0]["code"], "CS0117")

    def test_same_text_error_at_another_call_site_is_not_cancelled(self):
        # Codex 再監査: F(string)/G(int) → F(int)/G(string)。CS1503 は同文面だが別の呼び出しなので新規
        # Codex r2: swapping the parameter types yields same-text CS1503 at a different call site, which is new
        self.write_scenario(SCENARIO.replace("FooService.DoThing(1);", "A.F(1);\n    B.G(1);"))
        self.record_before({"A.F": ["string"], "B.G": ["int"]})
        self.set_api({"A.F": ["int"], "B.G": ["string"]})
        out = self.check(diff_for(SRC, ["    void F(string v)"], ["    void F(int v)"]))["scenarios"]
        self.assertEqual((out["status"], out["existing"]), ("new_errors", 0))
        self.assertEqual([(d["code"], d["source"]) for d in out["new"]], [("CS1503", "B.G(1);")])

    def test_source_less_match_is_unverified_not_existing(self):
        diag = {"code": "CS1503", "message": "Argument 1: cannot convert from 'int' to 'string'"}
        before = {"status": "ok", "scenarios": {"s.cs": {"status": "compiled", "diagnostics": [dict(diag, source="A.F(1);")]}}}
        after = {"status": "ok", "scenarios": {"s.cs": {"status": "compiled", "diagnostics": [dict(diag, source=None)]}}}
        out = scenario_compile.compare(before, after)
        self.assertEqual((out["status"], out["existing"], out["new"]), ("unverified", 0, []))

    def test_existing_errors_are_not_new_even_when_lines_shift(self):
        self.write_scenario(SCENARIO.replace("FooService.DoThing(1);", "Gone.Api(1);"), "old.cs")
        self.record_before({"FooService.DoThing": ["int"]})
        self.write_scenario("// 追記で行がずれる\n" + SCENARIO.replace("FooService.DoThing(1);", "Gone.Api(1);"), "old.cs")
        out = self.check(diff_for(SRC, ["    var a = 1;"], ["    var a = 2;"]))["scenarios"]
        self.assertEqual((out["status"], out["existing"], out["new"]), ("ok", 1, []))

    def test_missing_or_unavailable_before_is_unverified_not_existing(self):
        self.set_api({"FooService.DoThing": ["string"]})
        out = self.check(diff_for(SRC, ["        int value)"], ["        string value)"]))["scenarios"]
        self.assertEqual((out["status"], out["existing"]), ("unverified", 0))
        self.assertIn("反映前", out["unverified"][0])
        self.set_api(None)
        self.assertEqual(self.run_cli("record", "--name", "s0")["status"], "unavailable")
        self.set_api({"FooService.DoThing": ["string"]})
        out = self.check(diff_for(SRC, ["        int value)"], ["        string value)"]))["scenarios"]
        self.assertEqual((out["status"], out["existing"]), ("unverified", 0))

    def test_record_if_missing_reuses_and_snippet_never_runs_body(self):
        self.record_before({"FooService.DoThing": ["int"]})
        self.assertTrue(self.run_cli("record", "--name", "s0", "--if-missing")["reused"])
        snippet = (self.root / "run/refix/s0-scenarios/scenarios__misc__foo.cs.compile.cs").read_text(encoding="utf-8")
        # using 指令は先頭に残り、本体は呼ばれないローカル関数の中に入る（シナリオを実行しない）
        # Using directives stay on top and the body sits in a never-called local function (nothing runs)
        self.assertLess(snippet.index("using UnityEngine;"), snippet.index("object ScenarioCompileOnly()"))
        self.assertLess(snippet.index("object ScenarioCompileOnly()"), snippet.index("PlaytestRunner.Run"))
        self.assertTrue(snippet.rstrip().endswith('return "compile-only: foo.cs";'))

    def test_non_cs_or_test_only_diff_skips_compile(self):
        self.set_api(None)
        out = self.check(diff_for("docs/a.md", ["x"], ["y"])
                         + diff_for("moorestech_server/Assets/Scripts/Tests/UnitTest/FooTest.cs", ["a"], ["b"]))
        self.assertEqual(out["scenarios"]["status"], "not_required")

    def test_save_load_detection(self):
        self.set_api({"FooService.DoThing": ["int"]})
        touched = self.check(diff_for("moorestech_server/Assets/Scripts/Game.Block/WireSaveData.cs",
                                      ["    var a = 1;"], ["    var a = 2;"]))["save_load"]
        self.assertTrue(touched["touched"])
        self.assertIn("SaveLoad", touched["test_regex"])
        self.assertTrue(touched["unverified"], "クライアント起動経路の未確認が報告に載らない")
        by_line = self.check(diff_for("moorestech_server/Assets/Scripts/Game.Block/Wire.cs",
                                      ["    x.GetSaveState();"], ["    x.GetSaveStateV2();"]))["save_load"]
        self.assertTrue(by_line["touched"])
        untouched = self.check(diff_for("moorestech_client/Assets/Scripts/Client.Game/Hud.cs",
                                        ["    var a = 1;"], ["    var a = 2;"]))["save_load"]
        self.assertFalse(untouched["touched"])
        self.assertIsNone(untouched["test_regex"])


if __name__ == "__main__":
    unittest.main()
