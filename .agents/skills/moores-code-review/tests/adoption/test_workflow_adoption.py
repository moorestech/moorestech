import json
import tempfile
import unittest
from pathlib import Path

import workflow_sim
from test_bug_pass import AssembledWorkflowTest, build, main_run, CONTEXT, PATCH


class AdoptionTest(unittest.TestCase):
    def run_workflow(self, root, overrides=None, verify=False):
        script = AssembledWorkflowTest()._built(root, False)
        if verify:
            result = build(script.parent, "--verify-by-repro")
            self.assertEqual(result.returncode, 0, result.stderr)
        return workflow_sim.simulate(script, overrides)

    def finding(self, severity="Critical", recurs=None, fid="f1"):
        return {"id": fid, "severity": severity, "summary": "bad result", "recurs_from": recurs}

    def test_recurring_warning_promotes_and_stops_after_two_rounds(self):
        with tempfile.TemporaryDirectory() as td:
            out = self.run_workflow(Path(td), {
                "refix:refix-correctness-r1": {"critical_count": 1, "findings": [self.finding()]},
                "refix-apply-r1": {"applied": 1, "refix_scope": "source"},
                "refix:refix-correctness-r2": {"critical_count": 0, "findings": [self.finding("Warning", "r1")]},
            })
            refix = out["result"]["refix"]
            self.assertTrue(refix["loop_stuck"])
            self.assertTrue(refix["unresolved"])
            self.assertEqual(len(refix["rounds"]), 2)
            self.assertEqual(refix["rounds"][-1]["critical"], 1)
            labels = [c["label"] for c in out["calls"]]
            self.assertNotIn("refix-apply-r2", labels)
            second = next(c for c in out["calls"] if c["label"] == "refix:refix-correctness-r2")
            self.assertIn('"id":"f1"', second["prompt"])

    def test_different_critical_does_not_stop_early(self):
        with tempfile.TemporaryDirectory() as td:
            out = self.run_workflow(Path(td), {
                "refix:refix-correctness-r1": {"critical_count": 1, "findings": [self.finding()]},
                "refix:refix-correctness-r2": {"critical_count": 1, "findings": [self.finding(fid="f2")]},
                "refix-apply-r": {"applied": 1, "refix_scope": "source"},
            })
            self.assertFalse(out["result"]["refix"]["loop_stuck"])
            self.assertEqual(len(out["result"]["refix"]["rounds"]), 3)

    def test_invalid_history_and_count_fail_visibly(self):
        for finding, count in [(self.finding(recurs="r9"), 1), (self.finding(), 0)]:
            with self.subTest(finding=finding, count=count), tempfile.TemporaryDirectory() as td:
                with self.assertRaisesRegex(AssertionError, "Refix r1"):
                    self.run_workflow(Path(td), {"refix:refix-correctness-r1": {
                        "critical_count": count, "findings": [finding]}})

    def test_repro_off_adds_no_agent_or_prompt_or_schema_tokens(self):
        with tempfile.TemporaryDirectory() as td:
            out = self.run_workflow(Path(td))
            self.assertEqual(out["result"]["reproResults"], [])
            for call in out["calls"]:
                self.assertNotIn("logic_bugs", call["schema"].get("properties", {}))
                self.assertNotIn("実証", call["prompt"])
                self.assertFalse(call["label"].startswith("repro-"))

    def test_repro_on_reintegrates_all_three_outcomes(self):
        for status in ("実証済み", "反証済み", "再現できず"):
            with self.subTest(status=status), tempfile.TemporaryDirectory() as td:
                out = self.run_workflow(Path(td), {
                    "integrator": {"critical": 1, "logic_bugs": [{"id": "c1", "summary": "bad result", "fix": "correct result"}]},
                    "repro-1": {"status": status, "evidence": "observed value/log", "cleaned": True},
                }, verify=True)
                self.assertEqual(out["result"]["reproResults"][0]["status"], status)
                calls = {c["label"]: c for c in out["calls"]}
                self.assertIn("integrator-repro", calls)
                self.assertIn("棄却・降格の理由にせず", calls["integrator-repro"]["prompt"])
                self.assertIn("メイン repo 配下の現物を直接読まない", calls["repro-1"]["prompt"])

    def test_repro_cleanup_failure_stops_before_apply(self):
        with tempfile.TemporaryDirectory() as td:
            with self.assertRaisesRegex(AssertionError, "後片付けが未完了"):
                self.run_workflow(Path(td), {
                    "integrator": {"logic_bugs": [{"id": "c1", "summary": "bad", "fix": "fix"}]},
                    "repro-1": {"cleaned": False},
                }, verify=True)

    def test_bug_pass_preserves_declined_rulings(self):
        with tempfile.TemporaryDirectory() as td:
            root = Path(td)
            main = main_run(root)
            declined = {"declined": [{"id": "d1", "summary": "keep API", "reason": "explicit user choice"}]}
            (main / "step7-decisions.json").write_text(json.dumps(declined))
            run = root / "bp"
            run.mkdir()
            (run / "context.md").write_text(CONTEXT)
            (run / "patch.diff").write_text(PATCH)
            built = build(run, "--bug-pass", "--carry-from", str(main))
            self.assertEqual(built.returncode, 0, built.stderr)
            context = (run / "context-bug-pass.md").read_text()
            self.assertIn("explicit user choice", context)
            self.assertNotIn("## 許容するトレードオフ", context)
            (main / "step7-decisions.json").unlink()
            built = build(run, "--bug-pass", "--carry-from", str(main))
            self.assertNotEqual(built.returncode, 0)
            self.assertIn("裁定記録が無い", built.stderr)
