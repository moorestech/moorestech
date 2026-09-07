# .claude/skills/moores-code-review/tests/test_select_post_checks.py
# select_post_checks.py の発火条件を固定する。特に第3引数（反映diff）による
# applied-diff-correctness の発火（ソース変更あり）と非発火（テスト・コメントのみ・引数なし）。
# Pins select_post_checks.py firing rules, esp. applied-diff-correctness on the apply-only diff.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "select_post_checks.py"

SOURCE_APPLY = """--- a/moorestech_server/Assets/Scripts/Game.Block/Foo.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Foo.cs
@@ -10,3 +10,4 @@
     var x = 1;
+    if (x >= 2) return;
     // keep
"""
COMMENT_ONLY_APPLY = """--- a/moorestech_server/Assets/Scripts/Game.Block/Foo.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Foo.cs
@@ -10,3 +10,3 @@
-    // old comment
+    // new comment
     var x = 1;
"""
TEST_ONLY_APPLY = """--- a/moorestech_server/Assets/Scripts/Tests/FooTest.cs
+++ b/moorestech_server/Assets/Scripts/Tests/FooTest.cs
@@ -10,3 +10,4 @@
     var x = 1;
+    Assert.AreEqual(1, x);
"""


class SelectPostChecksTest(unittest.TestCase):
    def run_select(self, final_diff: str, apply_diff: str | None) -> str:
        with tempfile.TemporaryDirectory() as d:
            final_p = Path(d) / "final.diff"
            final_p.write_text(final_diff, encoding="utf-8")
            checks_p = Path(d) / "checks-final.json"
            checks_p.write_text(json.dumps({"candidates": {}}), encoding="utf-8")
            argv = [sys.executable, str(SCRIPT), str(final_p), str(checks_p)]
            if apply_diff is not None:
                apply_p = Path(d) / "apply.diff"
                apply_p.write_text(apply_diff, encoding="utf-8")
                argv.append(str(apply_p))
            return subprocess.run(argv, capture_output=True, text=True, check=True).stdout

    def test_applied_source_change_fires_correctness_recheck(self):
        # 反映diffにソースの実変更行があれば applied-diff-correctness が発火する
        # A real source change in the apply-only diff fires applied-diff-correctness
        out = self.run_select(SOURCE_APPLY, SOURCE_APPLY)
        self.assertIn("applied-diff-correctness.md\topus", out)

    def test_without_apply_diff_never_fires(self):
        # 第3引数なし（report-only・旧呼び出し）では発火しない（後方互換）
        # Without the 3rd arg (report-only / legacy call) it never fires
        out = self.run_select(SOURCE_APPLY, None)
        self.assertNotIn("applied-diff-correctness", out)

    def test_comment_only_and_test_only_apply_do_not_fire(self):
        # コメントのみ・テストのみの反映は再レビューしない（0トークン）
        # Comment-only / test-only applies are not re-reviewed
        self.assertNotIn("applied-diff-correctness", self.run_select(COMMENT_ONLY_APPLY, COMMENT_ONLY_APPLY))
        self.assertNotIn("applied-diff-correctness", self.run_select(TEST_ONLY_APPLY, TEST_ONLY_APPLY))


if __name__ == "__main__":
    unittest.main()
