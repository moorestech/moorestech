# .claude/skills/moores-code-review/tests/test_skill_wiring.py
# 再発防止: 検出器・verifier・外部ツールがSKILL.mdの実行経路に配線されていることを機械検証する。
# 「単体では動くがレビュー時に発火しない検出器」を作ってしまう事故（DeadMemberAudit 2026-08-03）の再発防止。
# Recurrence prevention: every detector/verifier/tool must be wired into SKILL.md's execution path.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import re
import unittest
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parent.parent
SKILL_MD = (SKILL_DIR / "SKILL.md").read_text(encoding="utf-8")
REPO_ROOT = SKILL_DIR.parent.parent.parent


class SkillWiringTest(unittest.TestCase):
    def test_every_verifier_is_wired_in_skill_md(self):
        # verifiers/配下の全ファイルがSKILL.md本文から参照されていること
        # Every verifier file must be referenced from SKILL.md
        for v in (SKILL_DIR / "verifiers").glob("*.md"):
            self.assertIn(v.name, SKILL_MD, f"{v.name} がSKILL.mdに配線されていない")

    def test_unified_gateway_is_wired_and_complete(self):
        # 統一窓口check_all.pyがSKILL.mdに配線され、そのVERIFIER_MAPの参照先が実在すること
        # The unified gateway must be wired in SKILL.md and its verifier map must point to real files
        self.assertIn("check_all.py", SKILL_MD, "check_all.py がSKILL.mdに配線されていない")
        import sys
        sys.path.insert(0, str(SKILL_DIR / "scripts"))
        import check_all
        for kind, (path, model) in check_all.VERIFIER_MAP.items():
            self.assertTrue((SKILL_DIR / path).is_file(),
                            f"VERIFIER_MAP[{kind}] の {path} が実在しない")
            self.assertIn(f"candidates.{kind}", SKILL_MD,
                          f"candidates.{kind} がSKILL.mdに記載されていない")

    def test_every_gate_script_is_wired_in_some_skill(self):
        # scripts/の実行系（deterministic_checks・select_*・*_gate）が、どこかのスキルの
        # 実行経路（いずれかのSKILL.md。hooks経由含む）から呼ばれていること
        # Every executable gate/selector script must be invoked from some skill's SKILL.md
        for pattern in ("deterministic_checks.py", "select_lenses.py", "select_reviewers.py"):
            self.assertIn(pattern, SKILL_MD, f"{pattern} がSKILL.mdに配線されていない")
        all_skill_mds = "\n".join(
            p.read_text(encoding="utf-8")
            for p in (REPO_ROOT / ".claude/skills").glob("*/SKILL.md"))
        for g in (SKILL_DIR / "scripts").glob("*_gate.py"):
            self.assertIn(g.name, all_skill_mds,
                          f"{g.name} がどのスキルのSKILL.mdにも配線されていない")

    def test_external_tools_are_wired_in_skill_md(self):
        # リポジトリ側の外部ツールに依存する場合はSKILL.mdに記載があること
        # External repo tools the skill depends on must be documented in SKILL.md
        if (REPO_ROOT / "tools/DeadMemberAudit").is_dir():
            self.assertIn("tools/DeadMemberAudit", SKILL_MD,
                          "tools/DeadMemberAudit がSKILL.mdに配線されていない")

    def test_every_candidate_rule_has_verifier_or_consumer(self):
        # SKILL.mdに書かれた candidates.* 全種に、起動先（verifier/レンズ/後段Step）の記述があること
        # Each candidates.* kind mentioned in SKILL.md must state its consumer
        kinds = set(re.findall(r"candidates\.(\w+)", SKILL_MD))
        self.assertTrue(kinds, "candidates.* の記述が見つからない")
        for kind in kinds:
            # 各記述行の周辺に「verifier」「レンズ」「Step」のいずれかが居ることを緩く確認
            # Loosely assert a consumer (verifier / lens / Step) is named near each mention
            lines = [l for l in SKILL_MD.splitlines() if f"candidates.{kind}" in l]
            self.assertTrue(
                any(("verifier" in l or "レンズ" in l or "Step" in l) for l in lines),
                f"candidates.{kind} の消費先（verifier/レンズ/Step）がSKILL.mdに書かれていない")

    def test_every_script_has_regression_banner(self):
        # 全スクリプトが「変更時は回帰テスト必須」バナーを持つこと（新規追加時の掲示漏れ防止）
        # Every script must carry the regression-suite banner (so new scripts inherit the rule)
        for s in (SKILL_DIR / "scripts").glob("*.py"):
            head = s.read_text(encoding="utf-8")[:1200]
            self.assertIn("必ず回帰テストを実行", head,
                          f"scripts/{s.name} に回帰テスト必須バナーが無い")

    def test_every_reviewer_and_lens_has_frontmatter(self):
        # selector発見可能性: reviewers/lensesはfrontmatter（extensions等）を持つこと
        # Selector discoverability: reviewers/lenses must carry frontmatter
        for d in ("reviewers", "lenses"):
            for f in (SKILL_DIR / d).glob("*.md"):
                head = f.read_text(encoding="utf-8").lstrip()
                self.assertTrue(head.startswith("---"),
                                f"{d}/{f.name} にfrontmatterが無くselectorから発見できない")


if __name__ == "__main__":
    unittest.main()
