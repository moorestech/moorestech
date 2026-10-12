# .claude/skills/moores-code-review/tests/test_bug_pass.py
# 最終バグ確認（Step 7.5 bug-pass）と、分割した Workflow スクリプトの結合・args 埋め込みの回帰テスト。
# Regression tests for the Step 7.5 bug pass and the assembled, args-embedded Workflow script.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import workflow_sim

SKILL_DIR = Path(__file__).resolve().parent.parent
REPO_ROOT = SKILL_DIR.parent.parent.parent
BUILDER = SKILL_DIR / "scripts/build_workflow_args.py"
sys.path.insert(0, str(SKILL_DIR / "scripts"))
from workflow_args import bug_pass  # noqa: E402

PATCH = """diff --git a/moorestech_client/Assets/Scripts/Client.Game/Foo.cs b/moorestech_client/Assets/Scripts/Client.Game/Foo.cs
--- a/moorestech_client/Assets/Scripts/Client.Game/Foo.cs
+++ b/moorestech_client/Assets/Scripts/Client.Game/Foo.cs
@@ -1,3 +1,3 @@
-    public void Run() { }
+    public async UniTask Run(CancellationToken ct) { await UniTask.Yield(); }
"""
CONTEXT = "## 目指す（ゴール）\n- g\n## 目指さない（非目標）\n- n [agent前提]\n## 許容するトレードオフ\n- t [ユーザー裁定: \"x\" 2026-10-01]\n## 尊重すべき制約\n- c\n"


def build(run_dir: Path, *extra: str) -> subprocess.CompletedProcess:
    return subprocess.run([sys.executable, str(BUILDER), "--run-dir", str(run_dir), "--patch", str(run_dir / "patch.diff"),
                           "--context", str(run_dir / "context.md"), "--repo-root", str(REPO_ROOT), "--base-ref", "HEAD", *extra],
                          capture_output=True, text=True)


def main_run(root: Path) -> Path:
    main = root / "main"
    (main / "agents").mkdir(parents=True)
    (main / "integrated.md").write_text("## Warning\n- a.cs:1: w\n", encoding="utf-8")
    (main / "agents/refix-correctness-w7-r1.md").write_text("Warning:\n- b.cs:2: w\n", encoding="utf-8")
    (main / "agents/rev-core-any-efficiency.md").write_text("x", encoding="utf-8")
    return main


class BugPassSelectionTest(unittest.TestCase):
    def test_allow_lists_name_existing_files(self):
        for stem in bug_pass.BUG_PASS_REVIEWERS:
            self.assertTrue((SKILL_DIR / "reviewers" / f"{stem}.md").is_file(), f"許可集合に実在しない reviewer: {stem}")
        for stem in bug_pass.BUG_PASS_INVESTIGATORS:
            self.assertTrue((SKILL_DIR / "investigators" / f"{stem}.md").is_file(), f"許可集合に実在しない investigator: {stem}")
        self.assertEqual(bug_pass.BUG_PASS_CODEX_KINDS, ("bughunt",))

    def test_structure_and_convention_systems_are_excluded(self):
        # 構造・規約・前例一致・依頼解釈・効率は bug-pass に入れない（入れると往復と過剰設計の源に戻る）
        # Structure/convention/precedent/intent/cost reviewers must stay out of the bug pass
        for stem in ("moores-any-precedent-alignment", "core-cs-centralization-duplication", "core-cs-dead-code-and-scope",
                     "moores-cs-speculative-abstraction", "core-any-user-intent-fulfillment", "core-any-efficiency"):
            self.assertNotIn(stem, bug_pass.BUG_PASS_REVIEWERS)
        self.assertNotIn("chunk-context-consistency", bug_pass.BUG_PASS_INVESTIGATORS)

    def test_builder_emits_bug_pass_plan(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            run_dir = root / "bp"
            run_dir.mkdir()
            (run_dir / "patch.diff").write_text(PATCH, encoding="utf-8")
            (run_dir / "context.md").write_text(CONTEXT, encoding="utf-8")
            for kind in ("audit", "bughunt", "design"):
                (run_dir / f"codex-{kind}.md").write_text("p", encoding="utf-8")
            run = build(run_dir, "--bug-pass", "--carry-from", str(main_run(root)))
            self.assertEqual(run.returncode, 0, run.stderr)
            args = json.loads((run_dir / "workflow-args.json").read_text(encoding="utf-8"))
            self.assertEqual(args["mode"], "bug-pass")
            stems = {Path(s["path"]).stem for s in args["systems"]}
            self.assertLessEqual(stems, set(bug_pass.BUG_PASS_REVIEWERS) | set(bug_pass.BUG_PASS_INVESTIGATORS))
            for always in ("core-any-callsite-tracer", "core-any-removed-invariant", "core-cs-async-cancellation",
                           "chunk-deep-correctness", "chunk-seam-integration"):
                self.assertIn(always, stems)
            self.assertFalse({s["kind"] for s in args["systems"]} & {"fable", "verifier"})
            self.assertEqual([j["name"] for j in args["codexJobs"]], ["codex-bughunt"])
            self.assertEqual(args["expectedSystems"]["total"], len(args["systems"]))
            self.assertEqual(len(args["carriedWarningSources"]), 2, "integrated.md と refix-*.md だけを持ち込む")
            stripped = Path(args["userPromptPath"]).read_text(encoding="utf-8")
            self.assertIn("目指す（ゴール）", stripped)
            self.assertIn("尊重すべき制約", stripped)
            self.assertNotIn("## 許容するトレードオフ", stripped)
            self.assertNotIn("## 目指さない", stripped)
            self.assertIn("bug-pass", Path(args["contractPath"]).read_text(encoding="utf-8"))
            self.assertEqual(args["postChecks"], [])

    def test_builder_fails_closed(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            run_dir = root / "bp"
            run_dir.mkdir()
            (run_dir / "patch.diff").write_text(PATCH, encoding="utf-8")
            (run_dir / "context.md").write_text(CONTEXT, encoding="utf-8")
            self.assertEqual(build(run_dir, "--bug-pass").returncode, 2, "--carry-from 無しを通した")
            self.assertEqual(build(run_dir, "--bug-pass", "--carry-from", str(main_run(root)), "--report-only").returncode, 2)
            empty = root / "no-integrated"
            empty.mkdir()
            self.assertEqual(build(run_dir, "--bug-pass", "--carry-from", str(empty)).returncode, 5)
            self.assertFalse((run_dir / "workflow-args.json").exists())


class AssembledWorkflowTest(unittest.TestCase):
    """結合済みスクリプトが args 無し（埋め込み済み）で全フェーズを解決し return まで届くこと。
    The assembled script must resolve every phase with args embedded (no runtime args) and reach its return."""

    def setUp(self):
        if not workflow_sim.node_path():
            self.skipTest("node が無い環境")

    def _built(self, root: Path, bug: bool) -> Path:
        run_dir = root / ("bp" if bug else "rev")
        run_dir.mkdir()
        (run_dir / "patch.diff").write_text(PATCH, encoding="utf-8")
        (run_dir / "context.md").write_text(CONTEXT, encoding="utf-8")
        (run_dir / "codex-bughunt.md").write_text("p", encoding="utf-8")
        if bug:
            run = build(run_dir, "--bug-pass", "--carry-from", str(main_run(root)))
        else:
            (run_dir / "checks.json").write_text(json.dumps({
                "reviewers": [{"path": str(SKILL_DIR / "reviewers/core-any-callsite-tracer.md"), "model": "opus"}],
                "verifiers_to_launch": [], "summary": {"errors": [], "reviewers": 1}}), encoding="utf-8")
            run = build(run_dir)
        self.assertEqual(run.returncode, 0, run.stderr)
        return run_dir / "review_workflow.js"

    def test_review_and_bug_pass_scripts_resolve_with_embedded_args(self):
        refix_path = {"refix:refix-correctness-r1": {"critical_count": 1}, "refix-apply-r1": {"applied": 1, "refix_scope": "source"}}
        with tempfile.TemporaryDirectory() as td:
            for bug in (False, True):
                out = workflow_sim.simulate(self._built(Path(td), bug), refix_path)
                result, calls = out["result"], out["calls"]
                self.assertEqual(result["mode"], "bug-pass" if bug else "review")
                self.assertEqual([r["round"] for r in result["refix"]["rounds"]], [1, 2])
                by_label = {c["label"]: c for c in calls}
                self.assertIn("applied_diff_checks.py", by_label["apply"]["prompt"])
                self.assertIn("applied_diff_checks.py", by_label["refix-apply-r1"]["prompt"])
                # 録画シナリオは名前で絞らず、反映前の記録と反映後を比べる（基点→反映後の snapshot 名で突き合わせる）
                # Scenarios are compared before/after by snapshot names, never narrowed by changed names
                for label, frm, to in (("apply", "s0", "s1"), ("refix-apply-r1", "s1", "s2")):
                    prompt = by_label[label]["prompt"]
                    self.assertIn(f"--name {frm} --if-missing", prompt)
                    self.assertIn(f"--from {frm} --to {to}", prompt)
                    self.assertNotIn("changed_api", prompt)
                integrator = by_label["integrator"]
                self.assertEqual("Mode : bug-pass" in integrator["prompt"], bug)
                # bug-pass は checks.json を渡さない。integrator は渡された入力・起動計画の系統だけを検査する
                # bug-pass passes no checks.json; the integrator inspects only the inputs and planned systems it is given
                self.assertEqual("Checks : なし" in integrator["prompt"], bug)
                self.assertEqual(integrator["schema"]["properties"]["design_items"].get("maximum") == 0, bug,
                                 "bug-pass の integrator は design_items>0 をスキーマで拒否する")
                self.assertEqual("post-check を選択しない" in by_label["apply"]["prompt"], bug)
                self.assertEqual("症状を消す最小の変更" in by_label["refix-apply-r1"]["prompt"], bug)


class BugPassWiringTest(unittest.TestCase):
    def test_docs_route_bug_pass_and_new_rules(self):
        skill = (SKILL_DIR / "SKILL.md").read_text(encoding="utf-8")
        rules = (SKILL_DIR / "references/integration-rules.md").read_text(encoding="utf-8")
        integrator = (SKILL_DIR / "integrators/finding-integrator.md").read_text(encoding="utf-8")
        steps = (SKILL_DIR / "references/orchestrator-steps.md").read_text(encoding="utf-8")
        self.assertIn("## Step 7.5: 最終バグ確認（bug-pass）", skill)
        self.assertIn("--bug-pass --carry-from", skill)
        self.assertIn("[解釈]", skill)
        self.assertIn("自律モード", skill)
        self.assertIn("## 7. 最終バグ確認（bug-pass）の統合", rules)
        self.assertIn("推奨は報告された症状を消す最小の変更にする", rules)
        self.assertNotIn("推奨は最も本質的で長期運用に耐える案に固定する", rules + integrator)
        self.assertIn("症状を消す最小の変更", integrator)
        self.assertIn("Mode : bug-pass", integrator)
        self.assertIn("起動計画に `rev-core-any-user-intent-fulfillment` が含まれるときだけ", integrator)
        self.assertNotIn("Codex3本は1系統", integrator)
        self.assertIn("## 反映 diff の機械的動作確認", steps)
        self.assertIn("applied_diff_checks.py", steps)
        # 系統名の列挙は bug_pass.py だけが持つ（手順書・SKILL.md に二重定義しない）
        # Only bug_pass.py enumerates the bug-pass systems; docs must not redefine the list
        sections = (skill.split("## Step 7.5", 1)[1].split("\n## ", 1)[0]
                    + steps.split("## bug-pass モードの差分", 1)[1].split("\n## ", 1)[0]
                    + rules.split("## 7. 最終バグ確認", 1)[1])
        for stem in [*bug_pass.BUG_PASS_REVIEWERS, *bug_pass.BUG_PASS_INVESTIGATORS]:
            self.assertNotIn(stem, sections, f"{stem} が bug-pass の文書節にも列挙されている（二重定義）")


if __name__ == "__main__":
    unittest.main()
