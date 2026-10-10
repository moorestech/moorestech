# .claude/skills/moores-code-review/tests/test_codex_workflow_runner.py
# Codex ホスト用ランタイム（scripts/codex_workflow_runner/run.mjs）が、Claude の Workflow 実行と同じ系統を同じ数・同じモデル階層で
# 発火させることを、同じ結合済みスクリプトの模擬実行（workflow_sim.py）と突き合わせて確かめる。偽 codex（fake_codex.py）を使う。
# Verifies the Codex-host runtime fires the same systems, count and model tiers as the Workflow simulation of the same script.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import json
import os
import re
import subprocess
import sys
import tempfile
import unittest
from collections import Counter
from pathlib import Path

import workflow_sim
from test_bug_pass import CONTEXT, PATCH, SKILL_DIR, build

RUNNER = SKILL_DIR / "scripts/codex_workflow_runner/run.mjs"
FAKE_CODEX = Path(__file__).resolve().parent / "fake_codex.py"
REVIEWERS = ("core-any-callsite-tracer", "core-any-user-intent-fulfillment", "moores-any-precedent-alignment")
# Refix の直し直し経路と report 未書込の再起動経路を踏ませる上書き（workflow_sim は label、偽 codex は安全化 label で突き合わせる）
# Overrides that drive the re-fix path; workflow_sim matches raw labels, the fake codex matches sanitized labels
REFIX_PATH = {"refix:refix-correctness-r1": {"critical_count": 1}, "refix-apply-r1": {"applied": 1, "refix_scope": "source"}}


def safe(label: str) -> str:
    return re.sub(r"[^A-Za-z0-9._-]+", "_", label)[:80]


class CodexWorkflowRunnerTest(unittest.TestCase):
    def setUp(self):
        if not workflow_sim.node_path():
            self.skipTest("node が無い環境")

    def _built(self, root: Path, *extra: str) -> Path:
        run_dir = root / "run"
        run_dir.mkdir()
        (run_dir / "patch.diff").write_text(PATCH, encoding="utf-8")
        (run_dir / "context.md").write_text(CONTEXT, encoding="utf-8")
        (run_dir / "detchecks.json").write_text("{}", encoding="utf-8")
        for kind in ("audit", "bughunt", "design"):
            (run_dir / f"codex-{kind}.md").write_text("p", encoding="utf-8")
        (run_dir / "checks.json").write_text(json.dumps({
            "reviewers": [{"path": str(SKILL_DIR / f"reviewers/{r}.md"), "model": "opus"} for r in REVIEWERS],
            "verifiers_to_launch": [], "summary": {"errors": [], "reviewers": len(REVIEWERS)}}), encoding="utf-8")
        run = build(run_dir, *extra)
        self.assertEqual(run.returncode, 0, run.stderr)
        return run_dir / "review_workflow.js"

    def _run(self, script: Path, overrides: dict) -> tuple[subprocess.CompletedProcess, list]:
        log = script.parent / "fake-codex.jsonl"
        env = {**os.environ, "FAKE_CODEX_LOG": str(log), "FAKE_CODEX_OVERRIDES": json.dumps({safe(k): v for k, v in overrides.items()})}
        bin_path = script.parent / "codex"
        bin_path.write_text(f"#!/bin/sh\nexec {sys.executable} {FAKE_CODEX} \"$@\"\n", encoding="utf-8")
        bin_path.chmod(0o755)
        run = subprocess.run([workflow_sim.node_path(), str(RUNNER), str(script), "--codex-bin", str(bin_path), "--max-parallel", "4"],
                             capture_output=True, text=True, env=env, timeout=120)
        return run, json.loads((script.parent / "codex-runner/fires.json").read_text(encoding="utf-8"))

    def test_fires_match_workflow_simulation(self):
        for extra, overrides in (((), REFIX_PATH), (("--report-only",), {})):
            with self.subTest(extra=extra), tempfile.TemporaryDirectory() as td:
                script = self._built(Path(td), *extra)
                expected = workflow_sim.simulate(script, overrides)["calls"]
                run, fires = self._run(script, overrides)
                self.assertEqual(run.returncode, 0, run.stderr[-1500:])
                result = json.loads(run.stdout)
                # 発火した label とモデル階層の多重集合が Workflow 模擬と一致すること（欠けも水増しも無い）
                # The multiset of (label, model) must equal the Workflow simulation's — no missing or extra fires
                self.assertEqual(Counter((f["label"], f["model"]) for f in fires),
                                 Counter((c["label"], c["model"]) for c in expected))
                self.assertTrue(all(f["ok"] for f in fires), fires)
                self.assertEqual(result["fireCount"], len(expected))
                self.assertEqual(result["result"]["systems"]["responded"], result["result"]["systems"]["planned"])
                for name in REVIEWERS:
                    self.assertIn(f"rev-{name}", [r["name"] for r in result["result"]["systems"]["perSystem"]])

    def test_codex_audit_jobs_are_launched_once(self):
        with tempfile.TemporaryDirectory() as td:
            script = self._built(Path(td), "--report-only")
            run, _ = self._run(script, {})
            self.assertEqual(run.returncode, 0, run.stderr[-1500:])
            # 本体が未起動の監査3本を runner が起動し、起動済み（.out.md あり）は再起動しない
            # The runner launches the three unstarted audits and never relaunches started ones
            for kind in ("audit", "bughunt", "design"):
                self.assertTrue((script.parent / f"codex-{kind}.out.md").exists(), kind)
            self.assertIn("起動済みのため", subprocess.run(
                [workflow_sim.node_path(), str(RUNNER), str(script), "--codex-bin", str(script.parent / "codex")],
                capture_output=True, text=True, env={**os.environ, "FAKE_CODEX_LOG": str(script.parent / "x.jsonl")}, timeout=120).stderr)

    def test_unknown_model_stops_before_launch(self):
        with tempfile.TemporaryDirectory() as td:
            script = self._built(Path(td), "--report-only")
            text = script.read_text(encoding="utf-8").replace("model: 'opus', schema: INTEGRATOR_SCHEMA", "model: 'gpt-x', schema: INTEGRATOR_SCHEMA")
            script.write_text(text, encoding="utf-8")
            run, fires = self._run(script, {})
            self.assertEqual(run.returncode, 3)
            self.assertIn("codex_model_map.json に無い model: gpt-x", json.loads(run.stdout)["error"])
            self.assertNotIn("integrator", [f["label"] for f in fires])

    def test_skill_routes_codex_host_to_runner(self):
        # Codex ホストが系統を自前代行せず runner を使うよう SKILL.md に配線されていること
        # SKILL.md must route Codex hosts to the runner instead of self-substituting the systems
        skill = (SKILL_DIR / "SKILL.md").read_text(encoding="utf-8")
        self.assertIn("references/codex-host.md", skill)
        ref = (SKILL_DIR / "references/codex-host.md").read_text(encoding="utf-8")
        self.assertIn("scripts/codex_workflow_runner/run.mjs", ref)
        self.assertIn("領域分割に置き換えるのは禁止", skill)


if __name__ == "__main__":
    unittest.main()
