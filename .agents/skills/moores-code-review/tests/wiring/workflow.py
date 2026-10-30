import re
import unittest
from pathlib import Path
from wiring.context import SKILL_DIR, SKILL_MD, REPO_ROOT, WORKFLOW_SRC

class WorkflowWiringTest(unittest.TestCase):
    """Workflow 既定（2026-08-20）の配線検査。sonnet オーケストレータの待機空転（$194〜240/本）を
    Workflow スクリプトへ置換した構成が、SKILL.md から到達可能で構文的にも壊れていないこと。
    Wiring checks for the Workflow default: reachable from SKILL.md and syntactically valid."""

    def test_workflow_script_and_args_builder_are_wired(self):
        skill = (SKILL_DIR / "SKILL.md").read_text(encoding="utf-8")
        self.assertTrue((SKILL_DIR / "scripts/review_workflow/00_meta.js").is_file(), "scripts/review_workflow/00_meta.js が無い")
        self.assertFalse((SKILL_DIR / "scripts/review_workflow.js").exists(), "分割前の review_workflow.js が残っている（部品と二重定義になる）")
        self.assertIn("scripts/review_workflow/", skill, "SKILL.md が Workflow 部品の置き場を指していない")
        self.assertIn('Workflow({ scriptPath: "<$RUNDIRの実値>/review_workflow.js" })', skill,
                      "SKILL.md が結合済みスクリプト（$RUNDIR/review_workflow.js）を scriptPath にしていない")
        self.assertIn("build_workflow_args.py", skill, "SKILL.md が build_workflow_args.py を呼んでいない")
        self.assertIn("codex_preflight.py", SKILL_MD, "codex_preflight.py が手順に配線されていない")
        self.assertTrue((SKILL_DIR / "references/output-contract.md").is_file(), "references/output-contract.md が無い")
        # 契約の読み込みは build_workflow_args.py の部品（workflow_args/systems.py）が担う
        # The contract is read by the builder's part module (workflow_args/systems.py)
        builder = (SKILL_DIR / "scripts/workflow_args/systems.py").read_text(encoding="utf-8")
        self.assertIn("output-contract.md", builder, "build_workflow_args.py（workflow_args/systems.py）が契約の正本を読んでいない")

    def test_workflow_script_has_meta_and_phases(self):
        src = WORKFLOW_SRC
        self.assertTrue(sorted((SKILL_DIR / "scripts/review_workflow").glob("*.js"))[0].read_text(encoding="utf-8")
                        .count("export const meta") == 1, "先頭部品が export const meta を持たない")
        self.assertIn("export const meta", src)
        for title in ("Review", "Integrate", "Apply", "Refix", "PostCheck"):
            self.assertIn(f"title: '{title}'", src, f"phase {title} が meta に無い")
        # モデル継承事故の防止: 全 agent() 起動が model を明示している
        # Every agent() launch must pass an explicit model (inheritance accident prevention)
        import re as _re
        # 引数に括弧（reviewPrompt(s) 等）を含む起動も取りこぼさない。件数一致で網羅を機械保証する
        # Also match launches whose args contain parentheses; assert the count so none slips through
        calls = _re.findall(r"await agent\((?:[^(){}]|\([^()]*\))*\{(?:[^{}]|\$\{[^{}]*\})*\}", src, flags=_re.S)
        self.assertEqual(src.count("await agent("), len(calls), "agent() 起動の検査が取りこぼしている")
        for call in calls:
            self.assertIn("model", call, f"model 未指定の agent() 起動がある: {call[:80]}")

    def test_workflow_script_parses(self):
        import shutil, subprocess, tempfile
        node = shutil.which("node")
        if not node:
            self.skipTest("node が無い環境")
        src = WORKFLOW_SRC.replace("export const meta", "const meta", 1)
        wrapped = ("const args={};const agent=async()=>null;const parallel=async(t)=>Promise.all(t.map(f=>f()));"
                   "const log=()=>{};\n(async()=>{\n" + src + "\n})();")
        with tempfile.NamedTemporaryFile("w", suffix=".mjs", delete=False) as fh:
            fh.write(wrapped)
            path = fh.name
        run = subprocess.run([node, "--check", path], capture_output=True, text=True)
        self.assertEqual(run.returncode, 0, f"review_workflow.js の構文エラー: {run.stderr[:400]}")

    def test_args_builder_produces_systems_from_checks(self):
        # 実 run の checks.json 形を模した最小入力で systems が組み上がること
        # Minimal checks.json shaped like real output must yield launchable systems
        import json, subprocess, sys, tempfile
        with tempfile.TemporaryDirectory() as td:
            run_dir = Path(td)
            moores_rev = SKILL_DIR / "reviewers/moores-any-precedent-alignment.md"
            rev = SKILL_DIR / "reviewers/core-cs-centralization-duplication.md"
            (run_dir / "checks.json").write_text(json.dumps({
                "deterministic": {"confirmed": [], "candidates": {}},
                "dead_member": {"status": "skipped", "candidates": []},
                "ts_dead_code": {"status": "skipped", "candidates": []},
                "reviewers": [{"path": str(moores_rev), "model": "fable"},
                              {"path": str(rev), "model": "opus"}],
                "verifiers_to_launch": [{"verifier": "verifiers/comparison-operator-verifier.md",
                                         "model": "sonnet", "candidate_kind": "comparison_operator", "count": 2}],
                "summary": {"errors": []},
            }), encoding="utf-8")
            (run_dir / "patch.diff").write_text("", encoding="utf-8")
            (run_dir / "context.md").write_text("## 目指す\n", encoding="utf-8")
            (run_dir / "chunks.tsv").write_text("chunk-1\tlabel\ta.cs,b.cs\n", encoding="utf-8")
            run = subprocess.run([sys.executable, str(SKILL_DIR / "scripts/build_workflow_args.py"),
                                  "--run-dir", str(run_dir), "--patch", str(run_dir / "patch.diff"),
                                  "--context", str(run_dir / "context.md"), "--repo-root", str(REPO_ROOT),
                                  "--base-ref", "HEAD"], capture_output=True, text=True)
            self.assertEqual(run.returncode, 0, run.stderr)
            args = json.loads((run_dir / "workflow-args.json").read_text(encoding="utf-8"))
            kinds = sorted(s["kind"] for s in args["systems"])
            self.assertEqual(kinds, ["fable", "investigator", "investigator", "investigator", "rev", "rev", "verifier"])
            self.assertTrue(all(s["model"] for s in args["systems"]), "model 空欄の系統がある")
            self.assertTrue((run_dir / "contract.md").is_file())
            self.assertEqual(args["baseRef"], "HEAD")
            self.assertEqual(args["expectedSystems"]["verifiers"], 1)
            self.assertEqual(args["codexJobs"], [])

    def test_args_builder_fails_closed_on_selector_error(self):
        # セレクタの error 行は「レビュアー0体」を成功として通さない（2026-08-20 レビュー C3）
        # A selector error row must not pass as a zero-reviewer success
        import json, subprocess, sys, tempfile
        with tempfile.TemporaryDirectory() as td:
            run_dir = Path(td)
            (run_dir / "checks.json").write_text(json.dumps({
                "deterministic": {"confirmed": [], "candidates": {}},
                "dead_member": {"status": "skipped", "candidates": []},
                "ts_dead_code": {"status": "skipped", "candidates": []},
                "reviewers": [{"error": "select_reviewers失敗: boom"}],
                "verifiers_to_launch": [],
                "summary": {"errors": []},
            }), encoding="utf-8")
            (run_dir / "patch.diff").write_text("", encoding="utf-8")
            (run_dir / "context.md").write_text("## 目指す\n", encoding="utf-8")
            run = subprocess.run([sys.executable, str(SKILL_DIR / "scripts/build_workflow_args.py"),
                                  "--run-dir", str(run_dir), "--patch", str(run_dir / "patch.diff"),
                                  "--context", str(run_dir / "context.md"), "--repo-root", str(REPO_ROOT)],
                                 capture_output=True, text=True)
            self.assertEqual(run.returncode, 4, run.stderr)
            self.assertFalse((run_dir / "workflow-args.json").exists())


