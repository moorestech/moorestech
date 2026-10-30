import re
import unittest
from pathlib import Path
from wiring.context import SKILL_DIR, SKILL_MD, REPO_ROOT, WORKFLOW_SRC

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
        for pattern in ("deterministic_checks.py", "select_reviewers.py",
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
        # SKILL.mdに書かれた candidates.* 全種に、起動先（verifier/reviewer/後段Step）の記述があること
        # Each candidates.* kind mentioned in SKILL.md must state its consumer
        kinds = set(re.findall(r"candidates\.(\w+)", SKILL_MD))
        self.assertTrue(kinds, "candidates.* の記述が見つからない")
        for kind in kinds:
            # 各記述行の周辺に「verifier」「reviewer」「Step」のいずれかが居ることを緩く確認
            # Loosely assert a consumer (verifier / reviewer / Step) is named near each mention
            lines = [l for l in SKILL_MD.splitlines() if f"candidates.{kind}" in l]
            self.assertTrue(
                any(("verifier" in l or "reviewer" in l or "Step" in l) for l in lines),
                f"candidates.{kind} の消費先（verifier/reviewer/Step）がSKILL.mdに書かれていない")

    def test_every_script_has_regression_banner(self):
        # 全スクリプトが「変更時は回帰テスト必須」バナーを持つこと（新規追加時の掲示漏れ防止）
        # Every script must carry the regression-suite banner (so new scripts inherit the rule)
        for s in [*(SKILL_DIR / "scripts").glob("*.py"), *(SKILL_DIR / "scripts/workflow_args").glob("*.py")]:
            head = s.read_text(encoding="utf-8")[:1200]
            self.assertIn("必ず回帰テストを実行", head,
                          f"scripts/{s.name} に回帰テスト必須バナーが無い")

    def test_patch_builder_uses_the_review_diff_exclusions(self):
        # 除外の正本は review_diff.py。Step 1 の patch 生成が素の git diff に戻ると、プレイテストシナリオや NuGet 本体が再びレビューに入る
        # Step 1 must build the patch via review_diff.py; plain git diff re-admits scenarios and vendored NuGet
        self.assertIn("scripts/review_diff.py <base>^..<last>", SKILL_MD,
                      "moores-code-review Step 1 のpatch生成が review_diff.py を経由していない")

    def test_every_reviewer_has_frontmatter(self):
        # selector発見可能性: reviewersはfrontmatter（extensions等）を持つこと
        # Selector discoverability: reviewers must carry frontmatter
        for f in (SKILL_DIR / "reviewers").glob("*.md"):
            head = f.read_text(encoding="utf-8").lstrip()
            self.assertTrue(head.startswith("---"),
                            f"reviewers/{f.name} にfrontmatterが無くselectorから発見できない")

    def test_reviewer_names_follow_origin_language_convention(self):
        # 旧lenses/統合後の命名規約: <moores|core>-<cs|ts_tsx|any>-<観点>
        # Naming after the lenses/ merge: <moores|core>-<cs|ts_tsx|any>-<topic>
        self.assertFalse((SKILL_DIR / "lenses").exists(), "lenses/ が復活している（reviewers/ へ統合済み）")
        for f in (SKILL_DIR / "reviewers").glob("*.md"):
            self.assertRegex(f.stem, r"^(moores|core)-(cs|ts_tsx|any)-[a-z0-9-]+$",
                             f"reviewers/{f.name} が <出自>-<言語>-<観点> 命名に従っていない")

    def test_reviewer_model_overrides_name_existing_reviewers(self):
        # model_map の sonnet/fable 列挙に実在しない stem があると無言で opus に戻る
        # A stale stem in model_map's sonnet/fable lists silently falls back to opus
        import json
        data = json.loads((SKILL_DIR / "scripts/model_map.json").read_text(encoding="utf-8"))
        stems = {f.stem for f in (SKILL_DIR / "reviewers").glob("*.md")}
        for model in ("sonnet", "fable"):
            for stem in data.get(model, []):
                self.assertIn(stem, stems, f"model_map.json の {model} に実在しない reviewer: {stem}")


