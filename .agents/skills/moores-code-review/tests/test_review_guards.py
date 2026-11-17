# 推奨の書き換え・依存の悪化・Codex の連続スキップを止める部品の回帰テスト
# Regression tests for the guards against rewritten recommendations, dependency regressions
# and repeated Codex skips.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parents[1]
SKILL_MD = (SKILL_DIR / "SKILL.md").read_text(encoding="utf-8") + (SKILL_DIR / "references" / "orchestrator-steps.md").read_text(encoding="utf-8")

SCRIPTS = SKILL_DIR / "scripts"
sys.path.insert(0, str(SCRIPTS))
import checks_dependency  # noqa: E402
from patch_util import parse_patch  # noqa: E402

INTEGRATED = """# integrated.md — test

## 採用Critical

### C1. 自動で直せる件
- **適用区分**: 自動適用可（§3）

### C2. 案が割れる件
- **適用区分**: 設計判断（§4）— 2 案が拮抗

## 設計判断

- **D1. 置き場所**
  - 案 A: 汎用層に置く。増える依存: Game.World→Game.SpaceElevator
  - 推奨: 両論併記

## 破棄
なし
"""


def run(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run([sys.executable, *args], capture_output=True, text=True)


class DesignTranscriptTest(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        self.integrated = self.tmp / "integrated.md"
        self.integrated.write_text(INTEGRATED, encoding="utf-8")
        self.design = self.tmp / "design.md"
        r = run(str(SCRIPTS / "design_from_integrated.py"), "build", "--integrated", str(self.integrated), "--out", str(self.design))
        self.assertEqual(r.returncode, 0, r.stderr)

    def check(self) -> int:
        return run(str(SCRIPTS / "design_from_integrated.py"), "check", "--integrated", str(self.integrated), "--design", str(self.design)).returncode

    def test_build_copies_only_design_class_criticals_and_design_section(self):
        text = self.design.read_text(encoding="utf-8")
        self.assertIn("### C2. 案が割れる件", text)
        self.assertNotIn("### C1.", text)
        self.assertIn("推奨: 両論併記", text)
        self.assertEqual(self.check(), 0)

    def test_appending_under_trailing_heading_passes(self):
        with self.design.open("a", encoding="utf-8") as f:
            f.write("\n## D9. apply で見つけた件\n- 推奨: 案 B\n")
        self.assertEqual(self.check(), 0)

    def test_rewritten_recommendation_is_rejected(self):
        # matching40 型: 統合報告の「両論併記」が design.md で「推奨 A」に変わる
        # matching40 shape: "both options" silently becomes "recommend A"
        text = self.design.read_text(encoding="utf-8").replace("推奨: 両論併記", "推奨: 案 A")
        self.design.write_text(text, encoding="utf-8")
        self.assertEqual(self.check(), 1)


class DesignTranscriptFormatVariantsTest(unittest.TestCase):
    def test_bold_and_heading_variants_are_all_transcribed(self):
        # 統合報告の実物にある書式の揺れ（太字の閉じ位置・太字なし・見出しの空白と括弧書き）
        # Format variants seen in real integrated reports
        text = ("## 採用 Critical（自動適用可）\n\n### C1. a\n- 適用区分: 自動適用\n\n"
                "## 採用 Critical（設計判断）\n\n### C2. b\n- **適用区分: 設計判断**\n\n### C3. c\n- 適用区分: 設計判断\n\n"
                "## 設計判断（ユーザーへ上げるもの）\n\n- D1. x\n")
        sys.path.insert(0, str(SCRIPTS))
        import design_from_integrated as d
        part = d.transcribed_part(text)
        self.assertIn("### C2. b", part)
        self.assertIn("### C3. c", part)
        self.assertNotIn("### C1.", part)
        self.assertIn("- D1. x", part)


class DependencyChangesTest(unittest.TestCase):
    def test_added_asmdef_reference_is_listed(self):
        patch = """diff --git a/S/Game.World/Game.World.asmdef b/S/Game.World/Game.World.asmdef
--- a/S/Game.World/Game.World.asmdef
+++ b/S/Game.World/Game.World.asmdef
@@ -3,2 +3,3 @@
     "references": [
+    "Game.SpaceElevator",
     "Core.Master",
"""
        changes = checks_dependency.run(parse_patch(patch))
        self.assertEqual([c["reference"] for c in changes], ["Game.SpaceElevator"])

    def test_package_json_version_rows_only(self):
        patch = """diff --git a/package.json b/package.json
--- a/package.json
+++ b/package.json
@@ -1,2 +1,4 @@
 {
+  "description": "not a dep",
+  "zod": "^3.23.0",
"""
        changes = checks_dependency.run(parse_patch(patch))
        self.assertEqual([c["reference"] for c in changes], ["zod"])


class CodexSkipLedgerTest(unittest.TestCase):
    def record(self, ledger: Path, status: str) -> dict:
        r = run(str(SCRIPTS / "codex_skip_ledger.py"), "record", "--ledger", str(ledger), "--status", status, "--target", "t")
        self.assertEqual(r.returncode, 0, r.stderr)
        return json.loads(r.stdout)

    def test_streak_alerts_from_second_skip_and_resets_on_ok(self):
        ledger = Path(tempfile.mkdtemp()) / "codex-skips.tsv"
        self.assertFalse(self.record(ledger, "missing_auth")["alert"])
        second = self.record(ledger, "quota")
        self.assertTrue(second["alert"])
        self.assertEqual(second["statuses"], ["missing_auth", "quota"])
        self.assertEqual(self.record(ledger, "ok")["streak"], 0)


class GuardWiringTest(unittest.TestCase):
    def test_guards_are_wired(self):
        for name in ("design_from_integrated.py check", "codex_skip_ledger.py record", "dependency_changes"):
            self.assertIn(name, SKILL_MD, f"{name} が SKILL.md に配線されていない")
        apply_js = (SCRIPTS / "review_workflow" / "40_apply.js").read_text(encoding="utf-8")
        self.assertIn("designFromIntegratedScript", apply_js, "apply 段が design.md を転記スクリプトで作っていない")
        args_py = (SCRIPTS / "build_workflow_args.py").read_text(encoding="utf-8")
        self.assertIn("design_from_integrated.py", args_py)


if __name__ == "__main__":
    unittest.main()
