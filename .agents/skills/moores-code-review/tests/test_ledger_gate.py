# .claude/skills/moores-code-review/tests/test_ledger_gate.py
# ledger_gateの台帳解決を固定する回帰テスト。正はplan自身の『## 判断記録（ADR）』、
# 旧plan互換としてfrontmatter spec:の台帳連結が生きていることも同時に見る。
# Regression tests: plan's own ledger section is canonical; legacy spec ledgers
# referenced via frontmatter must still be honored for old plans.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent / "scripts"
sys.path.insert(0, str(SCRIPTS))
import ledger_gate  # noqa: E402

RULES = [([r"Assets/Scripts"], [".cs"])]
TARGET = "moorestech_server/Assets/Scripts/Game.World/WorldSample.cs"


class LedgerGateTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.dir = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def write_plan(self, body: str) -> Path:
        plan = self.dir / "plan.md"
        plan.write_text(body, encoding="utf-8")
        return plan

    def test_plan_own_ledger_passes(self):
        plan = self.write_plan(
            f"# Plan\n\n- Modify: `{TARGET}`\n\n## 判断記録（ADR）\n\n- WorldSample.cs: 前例踏襲\n")
        self.assertEqual(ledger_gate.missing_entries(plan, RULES), [])

    def test_plan_ledger_missing_entry_reported(self):
        plan = self.write_plan(
            f"# Plan\n\n- Modify: `{TARGET}`\n\n## 判断記録（ADR）\n\n- 別件のみ\n")
        problems = ledger_gate.missing_entries(plan, RULES)
        self.assertEqual(len(problems), 1)
        self.assertIn("WorldSample.cs", problems[0])

    def test_legacy_spec_ledger_still_honored(self):
        spec = self.dir / "spec.md"
        spec.write_text("# Spec\n\n## 判断記録（ADR）\n\n- WorldSample.cs: 旧plan由来\n",
                        encoding="utf-8")
        plan = self.write_plan(
            f"---\nspec: {spec}\n---\n\n- Modify: `{TARGET}`\n")
        self.assertEqual(ledger_gate.missing_entries(plan, RULES), [])

    def test_no_ledger_anywhere_blocks(self):
        plan = self.write_plan(f"# Plan\n\n- Modify: `{TARGET}`\n")
        problems = ledger_gate.missing_entries(plan, RULES)
        self.assertEqual(len(problems), 1)
        self.assertIn("判断台帳セクション", problems[0])

    def test_no_moores_reviewer_target_never_blocks(self):
        plan = self.write_plan("# Plan\n\n- Modify: `docs/notes.md`\n")
        self.assertEqual(ledger_gate.missing_entries(plan, RULES), [])

    def test_design_checks_done_passes(self):
        plan = self.write_plan(
            "# Plan\n\n## 設計検査記録\n\n- 配置検査（Phase 1〜2.5）: 実施済み / 違反0件 / なし\n"
            "- Phase 2.6（型閉包）: 実施済み / 強0・弱0・第3バケツ0 / 発火なし\n\n## Task 1\n")
        self.assertEqual(ledger_gate.missing_design_checks(plan), [])

    def test_design_checks_not_done_blocks(self):
        plan = self.write_plan(
            "# Plan\n\n## 設計検査記録\n\n- 配置検査（Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / 層\n"
            "- Phase 2.6（型閉包）: 未実施\n")
        problems = ledger_gate.missing_design_checks(plan)
        self.assertEqual(len(problems), 1)
        self.assertIn("Phase 2.6", problems[0])

    def test_design_checks_section_missing_blocks_both(self):
        # 節の外にある「実施済み」は数えない
        # A 実施済み outside the section must not count
        plan = self.write_plan("# Plan\n\n- Phase 2.6 実施済みと本文に書いただけ\n\n## Task 1\n")
        self.assertEqual(len(ledger_gate.missing_design_checks(plan)), 2)


if __name__ == "__main__":
    unittest.main()
