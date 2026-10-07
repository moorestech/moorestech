# .claude/skills/moores-code-review/tests/s5_shape/test_s5_shape_gate.py
# §5（裁定引用の含意チェック）の形式ゲートの回帰テスト。fixtures/ は PR #1457 の実記録
# （moorestech_logs harness/pr-independent-review/runs/pr-1457、logs-sync 542fb588b 時点）の写し。
# 10件中7件が「読み1／読み2／両立判定」を欠き、統合側は「回収（決定10件判定済み）」と要約していた。
# Regression tests for the §5 shape gate. Fixtures are copies of the real PR #1457 records where
# 7 of 10 decisions lacked reading 1/2 and the integrator still marked the system recovered.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parents[2]
REPO_ROOT = SKILL_DIR.parents[2]
SCRIPT = SKILL_DIR / "scripts" / "s5_shape_gate.py"
FIXTURES = Path(__file__).resolve().parent / "fixtures"
sys.path.insert(0, str(SCRIPT.parent))
import s5_shape_gate  # noqa: E402

PR1457_REPORT = FIXTURES / "pr-1457-rev-core-any-user-intent-fulfillment.md"
PR1457_RECOVERY = FIXTURES / "pr-1457-integrated-recovery.md"
GOOD = ("## §5 裁定引用の含意チェック\n対象: あり（決定 2 件）\n"
        "- a.md: 停止はESCだけ — 引用「ESCだけ止めたい」 / 読み1: ESCのみ停止 → 両立 / 読み2: 全メニュー停止 → 不両立 / 棄却案「全部止める」→ no\n"
        "- b.md: 距離で閉じる — 出所に逐語引用なし・含意検査不能\n")


class S5ShapeGateTest(unittest.TestCase):
    def test_pr1457_record_is_sent_back_for_seven_decisions(self):
        problems = s5_shape_gate.report_violations(PR1457_REPORT.read_text(encoding="utf-8"))
        self.assertEqual(len(problems), 7, problems)
        self.assertEqual([p.split("「")[0] for p in problems],
                         [f"決定{n}" for n in (3, 5, 6, 7, 8, 9, 10)])

    def test_pr1457_integrated_recovered_row_is_rejected(self):
        problems = s5_shape_gate.integrated_violations(PR1457_RECOVERY.read_text(encoding="utf-8"))
        self.assertEqual(len(problems), 1)
        self.assertIn("rev-core-any-user-intent-fulfillment", problems[0])

    def test_pr1457_run_dir_exits_1(self):
        with tempfile.TemporaryDirectory() as tmp:
            run = Path(tmp)
            (run / "agents").mkdir()
            shutil.copy(PR1457_REPORT, run / "agents" / s5_shape_gate.REPORT_NAME)
            shutil.copy(PR1457_RECOVERY, run / "integrated.md")
            proc = subprocess.run([sys.executable, str(SCRIPT), str(run)], capture_output=True, text=True)
        self.assertEqual(proc.returncode, 1, proc.stderr)
        self.assertIn("差し戻し", proc.stderr)

    def test_integrated_marking_send_back_is_accepted(self):
        table = "## 系統別回収状況\n| rev-core-any-user-intent-fulfillment | 差し戻し（§5形式欠落 7件） |\n"
        self.assertEqual(s5_shape_gate.integrated_violations(table), [])

    def test_integrated_saying_no_gap_is_still_rejected(self):
        table = "## 系統別回収状況\n| rev-core-any-user-intent-fulfillment | 回収（欠員なし） |\n"
        self.assertEqual(len(s5_shape_gate.integrated_violations(table)), 1)

    def test_complete_shape_and_unverifiable_line_pass(self):
        self.assertEqual(s5_shape_gate.report_violations(GOOD), [])

    def test_no_target_passes(self):
        text = "Critical: なし\n\n## §5 裁定引用の含意チェック\n対象: なし（patch に .decisions/ の追加変更が無い）\n"
        self.assertEqual(s5_shape_gate.report_violations(text), [])

    def test_nested_reading_lines_are_joined_to_their_decision(self):
        text = ("## §5 裁定引用の含意チェック\n対象: あり（決定 1 件）\n- a.md: 決定 — 引用「ok」\n"
                "  - 読み1: 承認 → 両立\n  - 読み2: 受領のみ → 不両立\n- 備考: 同じ引用を3決定へ割当\n")
        self.assertEqual(s5_shape_gate.report_violations(text), [])

    def test_missing_verdict_and_count_mismatch_are_reported(self):
        text = ("## §5 裁定引用の含意チェック\n対象: あり（決定 3 件。代表で記載）\n"
                "- a.md: 決定 — 引用「x」 / 読み1: y → 両立 / 読み2: z → 否定される\n")
        problems = s5_shape_gate.report_violations(text)
        self.assertEqual(len(problems), 2, problems)
        self.assertIn("決定行は 1 件", problems[0])
        self.assertIn("読み2に", problems[1])

    def test_missing_section_and_missing_report_are_reported(self):
        self.assertEqual(len(s5_shape_gate.report_violations("Critical: なし\n")), 1)
        with tempfile.TemporaryDirectory() as tmp:
            result = s5_shape_gate.check_run_dir(Path(tmp))
        self.assertEqual(len(result["report_violations"]), 1)

    def test_gate_is_wired_into_every_consumer(self):
        # 配線なき検出器は未実装と同じ（2026-08-03裁定）。reviewer・統合・本体・独立レビューの4か所
        # A detector without wiring is unimplemented; it must be named in all four consumers
        for doc in ("moores-code-review/reviewers/core-any-user-intent-fulfillment.md",
                    "moores-code-review/integrators/finding-integrator.md",
                    "moores-code-review/SKILL.md",
                    "moores-code-review/references/orchestrator-steps.md",
                    "pr-independent-review/SKILL.md"):
            text = (REPO_ROOT / ".agents/skills" / doc).read_text(encoding="utf-8")
            self.assertIn("s5_shape_gate.py", text, f"{doc} に s5_shape_gate.py の配線が無い")


if __name__ == "__main__":
    unittest.main()
