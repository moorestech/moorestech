# .claude/skills/moores-code-review/tests/test_select_post_checks.py
# select_post_checks.py の発火条件を固定する（rationale-guard はコメント削除行、convention-guard は
# comment_length 候補）。applied-diff-correctness は Refix（refix_snapshot.py）が直接起動するので
# セレクタからは決して出ないことも固定する（2026-09-10・旧 apply.diff 第3引数方式の廃止）。
# Pins select_post_checks.py firing rules and that applied-diff-correctness is never selected here
# (the Refix phase launches it directly on the refix_snapshot.py interdiff).
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "select_post_checks.py"

SOURCE_DIFF = """--- a/moorestech_server/Assets/Scripts/Game.Block/Foo.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Foo.cs
@@ -10,3 +10,4 @@
     var x = 1;
+    if (x >= 2) return;
     // keep
"""
DELETED_COMMENT_DIFF = """--- a/moorestech_server/Assets/Scripts/Game.Block/Foo.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Foo.cs
@@ -10,3 +10,2 @@
-    // なぜ必要か: 起動順の都合
     var x = 1;
"""


class SelectPostChecksTest(unittest.TestCase):
    def run_select(self, final_diff: str, comment_candidates: list, extra_argv: list[str] | None = None) -> str:
        with tempfile.TemporaryDirectory() as d:
            final_p = Path(d) / "final.diff"
            final_p.write_text(final_diff, encoding="utf-8")
            checks_p = Path(d) / "checks-final.json"
            checks_p.write_text(json.dumps({"candidates": {"comment_length": comment_candidates}}), encoding="utf-8")
            argv = [sys.executable, str(SCRIPT), str(final_p), str(checks_p), *(extra_argv or [])]
            return subprocess.run(argv, capture_output=True, text=True, check=True).stdout

    def test_deleted_comment_fires_rationale_guard_only(self):
        # コメント削除行があれば rationale-guard だけ発火する / A deleted comment line fires only rationale-guard
        out = self.run_select(DELETED_COMMENT_DIFF, [])
        self.assertIn("comment-rationale-guard.md\t", out)
        self.assertNotIn("comment-convention-guard", out)

    def test_comment_length_candidates_fire_convention_guard(self):
        # comment_length 候補があれば convention-guard が発火する / comment_length candidates fire convention-guard
        out = self.run_select(SOURCE_DIFF, [{"file": "Foo.cs", "line": 3, "length": 40}])
        self.assertIn("comment-convention-guard.md\t", out)
        self.assertNotIn("comment-rationale-guard", out)

    def test_source_change_alone_fires_nothing(self):
        # ソース変更だけでは何も発火しない（0トークン）/ A plain source change fires nothing
        self.assertEqual(self.run_select(SOURCE_DIFF, []), "")

    def test_applied_diff_correctness_is_never_selected_here(self):
        # 反映 diff の再レビューは Refix の持ち場。第3引数を渡しても無視され、セレクタからは出ない
        # The applied-diff re-review belongs to Refix; a stray 3rd argument is ignored and never selects it
        with tempfile.TemporaryDirectory() as d:
            apply_p = Path(d) / "apply.diff"
            apply_p.write_text(SOURCE_DIFF, encoding="utf-8")
            out = self.run_select(SOURCE_DIFF, [], [str(apply_p)])
        self.assertNotIn("applied-diff-correctness", out)


if __name__ == "__main__":
    unittest.main()
