# .claude/skills/moores-code-review/tests/test_ledger_gate.py
# ledger_gateの台帳解決を固定する回帰テスト。正はplan自身の『## 判断記録（ADR）』、
# 旧plan互換としてfrontmatter spec:の台帳連結が生きていることも同時に見る。
# 後半は stop が Write/Edit 追跡に頼らず Bash 作成の plan も拾い、checkout で mtime だけ新しい旧 plan は巻き込まないことを固定する。
# Regression tests: plan's own ledger section is canonical; legacy spec ledgers
# referenced via frontmatter must still be honored for old plans. The second half pins that
# stop gates Bash-written plans without tracking while leaving checkout-touched old plans alone.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import json
import os
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent / "scripts"
sys.path.insert(0, str(SCRIPTS))
import ledger_gate  # noqa: E402
import plan_discovery  # noqa: E402

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


DONE = ("## 設計検査記録\n\n- 配置検査（Phase 1〜2.5）: 実施済み / 違反0件 / なし\n"
        "- Phase 2.6（型閉包）: 実施済み / 強0・弱0・第3バケツ0 / 発火なし\n")
MISSING = "# Plan\n\n## Task 1\n"


class PlanDiscoveryTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name).resolve() / "repo"
        self.plans = self.root / "docs/superpowers/plans"
        self.plans.mkdir(parents=True)
        self.git("init", "-q")
        (self.plans / "2026-01-01-old.md").write_text(MISSING, encoding="utf-8")
        self.git("add", "-A")
        self.git("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-qm", "init")
        self.transcript = Path(self.tmp.name) / "session.jsonl"
        self.records = [{"type": "user", "timestamp": self.iso(time.time() - 5),
                         "message": {"content": "plan書いて"}}]

    def tearDown(self):
        self.tmp.cleanup()

    def git(self, *args):
        subprocess.run(["git", "-C", str(self.root), *args], check=True, capture_output=True)

    @staticmethod
    def iso(epoch: float) -> str:
        return time.strftime("%Y-%m-%dT%H:%M:%S.000Z", time.gmtime(epoch))

    def bash_tool_use(self, command: str):
        self.records.append({"type": "assistant", "timestamp": self.iso(time.time()), "message": {
            "content": [{"type": "tool_use", "name": "Bash", "input": {"command": command}}]}})

    def write_transcript(self):
        self.transcript.write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in self.records),
                                   encoding="utf-8")

    def run_stop(self, state_dir: Path) -> subprocess.CompletedProcess:
        payload = {"session_id": "sid-test", "transcript_path": str(self.transcript), "cwd": str(self.root)}
        return subprocess.run([sys.executable, str(SCRIPTS / "ledger_gate.py"), "stop"],
                              input=json.dumps(payload), capture_output=True, text=True,
                              env={**os.environ, "TMPDIR": str(state_dir)})

    def test_bash_written_plan_is_discovered_but_touched_old_plan_is_not(self):
        new_plan = self.plans / "2026-10-07-new.md"
        self.bash_tool_use(f"cat > {new_plan} <<'EOF'\n{MISSING}EOF")
        new_plan.write_text(MISSING, encoding="utf-8")
        # checkout 相当: 旧 plan の mtime だけが新しくなる / checkout-like touch of an old plan
        os.utime(self.plans / "2026-01-01-old.md")
        self.write_transcript()
        found, why = plan_discovery.session_plans(str(self.transcript), str(self.root))
        self.assertEqual(why, "")
        self.assertEqual(found, [str(new_plan)])

    def test_relative_path_in_other_cwd_resolves_against_session_cwd(self):
        self.bash_tool_use("python3 gen.py > docs/superpowers/plans/2026-10-07-rel.md")
        self.write_transcript()
        mentioned = plan_discovery.mentioned_plan_paths(self.records, self.root)
        self.assertIn(self.plans / "2026-10-07-rel.md", mentioned)

    def test_relative_path_after_cd_resolves_against_cd_target(self):
        # セッションcwdはメインrepo、planはworktreeへ `cd <wt> && cat >` で書く運用
        # Session cwd is the main repo while the plan is written into a worktree via cd
        other_cwd = Path(self.tmp.name) / "main-repo"
        self.bash_tool_use(f"cd {self.root} && cat > docs/superpowers/plans/2026-10-07-wt.md <<'EOF'\nx\nEOF")
        mentioned = plan_discovery.mentioned_plan_paths(self.records, other_cwd)
        self.assertIn(self.plans / "2026-10-07-wt.md", mentioned)

    def test_plan_older_than_session_is_ignored(self):
        stale = self.plans / "2026-10-06-stale.md"
        stale.write_text(MISSING, encoding="utf-8")
        os.utime(stale, (time.time() - 3600, time.time() - 3600))
        self.write_transcript()
        found, _ = plan_discovery.session_plans(str(self.transcript), str(self.root))
        self.assertEqual(found, [])

    def test_missing_transcript_reports_reason(self):
        found, why = plan_discovery.session_plans(str(self.root / "nope.jsonl"), str(self.root))
        self.assertEqual(found, [])
        self.assertIn("セッション開始時刻", why)

    def test_stop_blocks_untracked_bash_plan_and_passes_filled_one(self):
        plan = self.plans / "2026-10-07-bash.md"
        self.bash_tool_use(f"cat > {plan} <<'EOF'\n...\nEOF")
        plan.write_text(MISSING, encoding="utf-8")
        self.write_transcript()
        blocked = self.run_stop(Path(self.tmp.name) / "s1")
        self.assertEqual(blocked.returncode, 2, blocked.stderr)
        self.assertIn("2026-10-07-bash.md: 設計検査記録の『配置検査』", blocked.stderr)
        plan.write_text("# Plan\n\n" + DONE, encoding="utf-8")
        passed = self.run_stop(Path(self.tmp.name) / "s2")
        self.assertEqual(passed.returncode, 0, passed.stderr)


if __name__ == "__main__":
    unittest.main()
