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
# 2026-08-18にSKILL.mdは委譲ディスパッチャへ分割され、実行手順はreferences/orchestrator-steps.mdへ移った。
# 配線検査の対象は「SKILL.md + references配下の手順書」の全体（片方だけ見ると検査が空振りする）。
# The skill body was split into references/ on 2026-08-18; wiring lives across both.
SKILL_MD = "\n".join(
    [(SKILL_DIR / "SKILL.md").read_text(encoding="utf-8")]
    + [f.read_text(encoding="utf-8") for f in sorted((SKILL_DIR / "references").glob("*.md"))]
)
REPO_ROOT = SKILL_DIR.parent.parent.parent


class SkillWiringTest(unittest.TestCase):
    def test_every_verifier_is_wired_in_skill_md(self):
        # verifiers/配下の全ファイルがSKILL.md本文から参照されていること
        # Every verifier file must be referenced from SKILL.md
        for v in (SKILL_DIR / "verifiers").glob("*.md"):
            self.assertIn(v.name, SKILL_MD, f"{v.name} がSKILL.mdに配線されていない")

    def test_every_post_check_is_wired_in_skill_md(self):
        # post-checks/配下の全ファイルがSKILL.md本文からフルネームで参照されていること
        # （all-code-review移植時にconvention-guardのパス省略を実検出した検査の逆輸入 2026-08-04）
        # Every post-check file must be referenced from SKILL.md by full name
        for p in (SKILL_DIR / "post-checks").glob("*.md"):
            self.assertIn(p.name, SKILL_MD, f"{p.name} がSKILL.mdに配線されていない")

    def test_every_integrator_is_wired_in_skill_md(self):
        # integrators/配下の全ファイルがSKILL.md本文からフルネームで参照されていること（Step 5統合委譲の配線）
        # Every integrator file must be referenced from SKILL.md by full name
        for i in (SKILL_DIR / "integrators").glob("*.md"):
            self.assertIn(i.name, SKILL_MD, f"{i.name} がSKILL.mdに配線されていない")

    def test_every_investigator_is_wired_in_skill_md(self):
        # investigators/配下の全ファイルがSKILL.md本文からフルネームで参照されていること（第6系統の配線）
        # Every investigator file must be referenced from SKILL.md by full name
        for i in (SKILL_DIR / "investigators").glob("*.md"):
            self.assertIn(i.name, SKILL_MD, f"{i.name} がSKILL.mdに配線されていない")

    def test_every_codex_template_is_wired_in_skill_md(self):
        # scripts/配下の全codexテンプレートがSKILL.md本文から参照されていること（3起動構成の配線）
        # Every codex template under scripts/ must be referenced from SKILL.md
        templates = list((SKILL_DIR / "scripts").glob("codex-*.md"))
        self.assertGreaterEqual(len(templates), 3, "codexテンプレートが3本未満")
        for t in templates:
            self.assertIn(t.name, SKILL_MD, f"{t.name} がSKILL.mdに配線されていない")

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
        for pattern in ("deterministic_checks.py", "select_lenses.py", "select_reviewers.py",
                        "select_post_checks.py"):
            self.assertIn(pattern, SKILL_MD, f"{pattern} がSKILL.mdに配線されていない")
        # 手順書がreferences/へ分割されたスキルがあるため、そちらも配線先として数える
        # Some skills moved their steps into references/, so scan those too
        all_skill_mds = "\n".join(
            p.read_text(encoding="utf-8")
            for pattern in ("*/SKILL.md", "*/references/*.md")
            for p in (REPO_ROOT / ".agents/skills").glob(pattern))
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

    def test_playtest_scenarios_are_excluded_from_patch(self):
        # patch生成のpathspecからプレイテストシナリオ除外が消えていないこと
        # （消えると使い捨ての操作台本が再びレビュー対象になる。ユーザー裁定 2026-08-16 / PR#1137-F12）
        # The playtest-scenario exclusion must stay in every patch-building pathspec
        pathspec = "':(exclude,glob)**/unity-playmode-recorded-playtest/**/*.cs'"
        self.assertIn(pathspec, SKILL_MD,
                      "moores-code-review Step 1 のpatch生成からプレイテストシナリオ除外が消えている")
        independent = (REPO_ROOT / ".agents/skills/pr-independent-review/SKILL.md").read_text(encoding="utf-8")
        self.assertIn(pathspec, independent,
                      "pr-independent-review Step 3 のpatch生成からプレイテストシナリオ除外が消えている")

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


class CodexOutputRecoveryTest(unittest.TestCase):
    """codexの結論回収経路の再発防止（2026-08-18 PR#1167: 完走したのに欠員と誤判定した）。
    Recurrence prevention for codex conclusion recovery."""

    def _docs(self):
        return (sorted((SKILL_DIR / "references").glob("*.md"))
                + [SKILL_DIR / "SKILL.md"]
                + sorted((SKILL_DIR / "integrators").glob("*.md")))

    def test_every_codex_exec_writes_final_message_to_file(self):
        # stdout(.out.md)は完走しても結論が入らないことがあるため -o が必須
        # stdout can be truncated even on completion, so -o is mandatory
        for doc in self._docs():
            for line in doc.read_text(encoding="utf-8").splitlines():
                if line.strip().startswith("codex exec"):
                    self.assertRegex(line, r"(?:^|\s)(?:-o|--output-last-message)\s",
                                     f"{doc.name}: codex exec に -o が無い -> {line}")

    def test_recovery_script_exists_and_is_wired(self):
        script = SKILL_DIR / "scripts" / "codex_recover.py"
        self.assertTrue(script.is_file(), "scripts/codex_recover.py が無い")
        wired = [d.name for d in self._docs() if "codex_recover.py" in d.read_text(encoding="utf-8")]
        self.assertIn("orchestrator-steps.md", wired)
        self.assertIn("finding-integrator.md", wired)


class WorkflowWiringTest(unittest.TestCase):
    """Workflow 既定（2026-08-20）の配線検査。sonnet オーケストレータの待機空転（$194〜240/本）を
    Workflow スクリプトへ置換した構成が、SKILL.md から到達可能で構文的にも壊れていないこと。
    Wiring checks for the Workflow default: reachable from SKILL.md and syntactically valid."""

    def test_workflow_script_and_args_builder_are_wired(self):
        skill = (SKILL_DIR / "SKILL.md").read_text(encoding="utf-8")
        self.assertTrue((SKILL_DIR / "scripts/review_workflow.js").is_file(), "scripts/review_workflow.js が無い")
        self.assertIn("scripts/review_workflow.js", skill, "SKILL.md が review_workflow.js を起動経路に載せていない")
        self.assertIn("build_workflow_args.py", skill, "SKILL.md が build_workflow_args.py を呼んでいない")
        self.assertIn("codex_preflight.py", SKILL_MD, "codex_preflight.py が手順に配線されていない")
        self.assertTrue((SKILL_DIR / "references/output-contract.md").is_file(), "references/output-contract.md が無い")
        builder = (SKILL_DIR / "scripts/build_workflow_args.py").read_text(encoding="utf-8")
        self.assertIn("output-contract.md", builder, "build_workflow_args.py が契約の正本を読んでいない")

    def test_workflow_script_has_meta_and_phases(self):
        src = (SKILL_DIR / "scripts/review_workflow.js").read_text(encoding="utf-8")
        self.assertIn("export const meta", src)
        for title in ("Review", "Integrate", "Apply", "PostCheck"):
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
        src = (SKILL_DIR / "scripts/review_workflow.js").read_text(encoding="utf-8")
        src = src.replace("export const meta", "const meta", 1)
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
            lens = SKILL_DIR / "lenses/precedent-alignment.md"
            rev = SKILL_DIR / "reviewers/core-cs-centralization-duplication.md"
            (run_dir / "checks.json").write_text(json.dumps({
                "deterministic": {"confirmed": [], "candidates": {}},
                "dead_member": {"status": "skipped", "candidates": []},
                "ts_dead_code": {"status": "skipped", "candidates": []},
                "lenses": [{"path": str(lens), "model": "fable"}],
                "reviewers": [{"path": str(rev), "model": "opus"}],
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
            self.assertEqual(kinds, ["fable", "investigator", "investigator", "investigator", "lens", "rev", "verifier"])
            self.assertTrue(all(s["model"] for s in args["systems"]), "model 空欄の系統がある")
            self.assertTrue((run_dir / "contract.md").is_file())
            self.assertEqual(args["baseRef"], "HEAD")
            self.assertFalse(args["reportOnly"])
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
                "lenses": [{"error": "select_lenses失敗: boom"}],
                "reviewers": [],
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


class CodexRecoverAuthOrderTest(unittest.TestCase):
    """認証失効(exit 5)は rollout に結論が無いときだけ・codex 自身の ERROR 行だけで判定する（2026-08-20 レビュー C1）。
    exit 5 only when the rollout has no answer, and only from codex's own ERROR lines."""

    def _run(self, out_text: str) -> int:
        import os, subprocess, sys, tempfile
        with tempfile.TemporaryDirectory() as td:
            prompt = Path(td) / "codex-x.md"
            out = Path(td) / "codex-x.out.md"
            prompt.write_text("unique-prompt-that-matches-no-session-9f3a", encoding="utf-8")
            out.write_text(out_text, encoding="utf-8")
            env = dict(os.environ, CODEX_HOME=td)  # sessions/ 不在 → rollout に結論なし
            run = subprocess.run([sys.executable, str(SKILL_DIR / "scripts/codex_recover.py"),
                                  "--prompt", str(prompt), "--out", str(out)],
                                 capture_output=True, text=True, env=env)
            return run.returncode

    def test_real_auth_error_line_is_exit_5(self):
        self.assertEqual(self._run(
            "2026-08-19T20:37:49Z ERROR codex_login::auth::manager: Failed to refresh token: 401 Unauthorized: {\n"), 5)

    def test_audited_text_mentioning_401_is_not_auth_failure(self):
        # 監査対象の本文（プロンプト echo）に 401 の文字があっても認証失効とは判定しない
        # The audited text echoed to stdout may mention 401; that must not read as an auth failure
        self.assertEqual(self._run(
            "user\n認証失効(401 Unauthorized / Please log in again) を exit 5 で区別する設計をレビューせよ\n"), 4)
