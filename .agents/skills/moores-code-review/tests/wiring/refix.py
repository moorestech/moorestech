import re
import unittest
from pathlib import Path
from wiring.context import SKILL_DIR, SKILL_MD, REPO_ROOT, WORKFLOW_SRC

class RefixWiringTest(unittest.TestCase):
    """反映 diff の再レビュー（2026-09-08 c9baa79 較正）が既定の Workflow 実行形と Step 7 裁定反映経路に配線されていること。
    手順書にだけ書かれて既定経路で 1 周しか走らない状態（2026-09-10 まで）への回帰を防ぐ。
    The applied-diff re-review must be wired into the default Workflow path and the post-Step-7 apply path,
    not only described in the orchestrator prose (the single-shot post-check state until 2026-09-10)."""

    def test_snapshot_script_exists_and_is_referenced_everywhere(self):
        self.assertTrue((SKILL_DIR / "scripts/refix_snapshot.py").is_file(), "scripts/refix_snapshot.py が無い")
        for doc in ("SKILL.md", "references/orchestrator-steps.md"):
            self.assertIn("refix_snapshot.py", (SKILL_DIR / doc).read_text(encoding="utf-8"),
                          f"{doc} が反映 diff の作り方として refix_snapshot.py を指していない")
        wf = WORKFLOW_SRC
        self.assertIn("refixSnapshotScript", wf, "review_workflow.js が refix_snapshot.py を呼んでいない")
        self.assertIn("refix-correctness-r", wf, "review_workflow.js に Refix の reviewer 起動が無い")
        self.assertIn("refix_scope", wf, "apply の返り値に反映 diff の scope が無い")
        self.assertNotIn("apply.diff", wf, "旧 apply.diff 経路（stash create 基点）が Workflow に残っている")

    def test_skill_md_covers_refix_postmortem_and_step7(self):
        self.assertIn("`refix`", SKILL_MD, "回収時の検死に refix が無い")
        self.assertIn("step7-decisions.json", SKILL_MD)
        self.assertIn("--verify-by-repro", SKILL_MD)
        self.assertIn("trackRefix", WORKFLOW_SRC)
        self.assertIn("logic-bug-repro.md", (SKILL_DIR / "scripts/build_workflow_args.py").read_text())
        self.assertIn("unresolved", SKILL_MD, "検死が未収束の扱いを書いていない")
        self.assertIn("w7-s0", SKILL_MD, "Step 7 の裁定反映後に snapshot を取る手順が無い（c9baa79 の経路）")

    def test_args_builder_emits_refix_wiring(self):
        import json, subprocess, sys, tempfile
        with tempfile.TemporaryDirectory() as td:
            run_dir = Path(td)
            moores_rev = SKILL_DIR / "reviewers/moores-any-precedent-alignment.md"
            (run_dir / "checks.json").write_text(json.dumps({
                "deterministic": {"confirmed": [], "candidates": {}},
                "dead_member": {"status": "skipped", "candidates": []},
                "ts_dead_code": {"status": "skipped", "candidates": []},
                "reviewers": [{"path": str(moores_rev), "model": "fable"}],
                "verifiers_to_launch": [],
                "summary": {"errors": []},
            }), encoding="utf-8")
            (run_dir / "patch.diff").write_text("", encoding="utf-8")
            (run_dir / "context.md").write_text("## 目指す\n", encoding="utf-8")
            run = subprocess.run([sys.executable, str(SKILL_DIR / "scripts/build_workflow_args.py"),
                                  "--run-dir", str(run_dir), "--patch", str(run_dir / "patch.diff"),
                                  "--context", str(run_dir / "context.md"), "--repo-root", str(REPO_ROOT)],
                                 capture_output=True, text=True)
            self.assertEqual(run.returncode, 0, run.stderr)
            args = json.loads((run_dir / "workflow-args.json").read_text(encoding="utf-8"))
            self.assertTrue(Path(args["refixReviewerPath"]).is_file(), "refixReviewerPath が実在しない")
            self.assertTrue(args["refixReviewerPath"].endswith("post-checks/applied-diff-correctness.md"))
            self.assertTrue(Path(args["refixSnapshotScript"]).is_file(), "refixSnapshotScript が実在しない")
            self.assertTrue(Path(args["integrationRulesPath"]).is_file(), "integrationRulesPath が実在しない")
            self.assertEqual(args["refixMaxRounds"], 3)


