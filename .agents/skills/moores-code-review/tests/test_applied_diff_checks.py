# .claude/skills/moores-code-review/tests/test_applied_diff_checks.py
# 反映 diff の機械的動作確認の対象選び（録画シナリオの参照・セーブ往復テスト）の回帰テスト。
# Regression tests for picking mechanical-check targets of an applied diff (playtest scenarios, save/load tests).
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parent.parent
SCRIPT = SKILL_DIR / "scripts/applied_diff_checks.py"
SCENARIO = """// 説明
using Client.Playtest;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("x", options, async p =>
{
    FooService.DoThing();
});
"""


def diff_for(path: str, removed: list, added: list) -> str:
    body = "".join(f"-{l}\n" for l in removed) + "".join(f"+{l}\n" for l in added)
    return f"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n@@ -1 +1 @@\n{body}"


def run_checks(root: Path, diff_text: str) -> dict:
    (root / "applied.diff").write_text(diff_text, encoding="utf-8")
    run = subprocess.run([sys.executable, str(SCRIPT), str(root / "applied.diff"), "--repo-root", str(root),
                          "--out-dir", str(root / "out")], capture_output=True, text=True)
    assert run.returncode == 0, run.stderr
    return json.loads(run.stdout)


class AppliedDiffChecksTest(unittest.TestCase):
    def setUp(self):
        self._td = tempfile.TemporaryDirectory()
        self.root = Path(self._td.name)
        scenarios = self.root / ".agents/skills/unity-playmode-recorded-playtest/scenarios/misc"
        scenarios.mkdir(parents=True)
        (scenarios / "foo.cs").write_text(SCENARIO, encoding="utf-8")

    def tearDown(self):
        self._td.cleanup()

    def test_renamed_public_method_flags_referencing_scenario_with_compile_snippet(self):
        out = run_checks(self.root, diff_for("moorestech_client/Assets/Scripts/Client.Game/FooService.cs",
                                             ["    public static void DoThing()"], ["    public static void DoThingRenamed()"]))
        self.assertIn("DoThing", out["changed_api"])
        self.assertEqual(out["scenario_files"], [".agents/skills/unity-playmode-recorded-playtest/scenarios/misc/foo.cs"])
        snippet = Path(out["compile_snippets"][0]).read_text(encoding="utf-8")
        # using 指令は先頭に残り、本体は呼ばれないローカル関数の中に入る（シナリオを実行しない）
        # Using directives stay on top and the body sits in a never-called local function (nothing runs)
        self.assertLess(snippet.index("using UnityEngine;"), snippet.index("object ScenarioCompileOnly()"))
        self.assertLess(snippet.index("object ScenarioCompileOnly()"), snippet.index("PlaytestRunner.Run"))
        self.assertTrue(snippet.rstrip().endswith('return "compile-only: foo.cs";'))

    def test_moved_declaration_and_test_files_are_not_flagged(self):
        moved = diff_for("moorestech_client/Assets/Scripts/Client.Game/FooService.cs",
                         ["    public static void DoThing()"], ["    public static void DoThing()"])
        test_only = diff_for("moorestech_server/Assets/Scripts/Tests/UnitTest/FooTest.cs",
                             ["    public void DoThing()"], ["    public void Other()"])
        out = run_checks(self.root, moved + test_only)
        self.assertEqual(out["changed_api"], [])
        self.assertEqual(out["scenario_files"], [])

    def test_type_removal_is_flagged(self):
        out = run_checks(self.root, diff_for("moorestech_client/Assets/Scripts/Client.Game/FooService.cs",
                                             ["public static class FooService"], []))
        self.assertIn("FooService", out["changed_api"])
        self.assertEqual(len(out["scenario_files"]), 1)

    def test_save_load_detection(self):
        touched = run_checks(self.root, diff_for("moorestech_server/Assets/Scripts/Game.Block/WireSaveData.cs",
                                                 ["    var a = 1;"], ["    var a = 2;"]))["save_load"]
        self.assertTrue(touched["touched"])
        self.assertIn("SaveLoad", touched["test_regex"])
        self.assertTrue(touched["unverified"], "クライアント起動経路の未確認が報告に載らない")
        by_line = run_checks(self.root, diff_for("moorestech_server/Assets/Scripts/Game.Block/Wire.cs",
                                                 ["    x.GetSaveState();"], ["    x.GetSaveStateV2();"]))["save_load"]
        self.assertTrue(by_line["touched"])
        untouched = run_checks(self.root, diff_for("moorestech_client/Assets/Scripts/Client.Game/Hud.cs",
                                                   ["    var a = 1;"], ["    var a = 2;"]))["save_load"]
        self.assertFalse(untouched["touched"])
        self.assertIsNone(untouched["test_regex"])


if __name__ == "__main__":
    unittest.main()
