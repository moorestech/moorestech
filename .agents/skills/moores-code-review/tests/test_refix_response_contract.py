import tempfile
import unittest
from pathlib import Path

import workflow_sim
from test_bug_pass import AssembledWorkflowTest


class RefixResponseContractTest(unittest.TestCase):
    def findings(self):
        return [{"id": f"c{i}", "severity": "Critical", "summary": f"failure {i}",
                 "recurs_from": None} for i in range(3)]

    def test_incomplete_response_retries_without_changing_normal_fires(self):
        with tempfile.TemporaryDirectory() as td:
            script = AssembledWorkflowTest()._built(Path(td), False)
            baseline = workflow_sim.simulate(script)
            out = workflow_sim.simulate(script, {
                "refix:refix-correctness-r1": [
                    {"critical_count": 3, "findings": []},
                    {"critical_count": 3, "findings": self.findings()},
                ],
            })
            calls = [c for c in out["calls"] if c["label"] == "refix:refix-correctness-r1"]
            self.assertEqual(len(calls), 2)
            self.assertIn("差=3", calls[1]["prompt"])
            self.assertIn("findings の省略は禁止", calls[0]["prompt"])
            self.assertTrue(any("契約不整合" in line and "再起動" in line for line in out["logs"]))
            self.assertEqual(out["result"]["refix"]["rounds"][0]["findings"], self.findings())
            normal = lambda result: [(c["label"], c["model"]) for c in result["calls"]
                                     if not c["label"].startswith("refix")]
            self.assertEqual(normal(out), normal(baseline))

    def test_missing_fields_are_passed_to_retry(self):
        with tempfile.TemporaryDirectory() as td:
            script = AssembledWorkflowTest()._built(Path(td), False)
            out = workflow_sim.simulate(script, {
                "refix:refix-correctness-r1": [
                    {"critical_count": 1, "findings": [{"id": "x", "severity": "Critical"}]},
                    {"critical_count": 3, "findings": self.findings()},
                ],
            })
            retry = [c for c in out["calls"] if c["label"] == "refix:refix-correctness-r1"][1]
            self.assertIn("欠落: summary, recurs_from", retry["prompt"])

    def test_permanent_mismatch_fails_closed_after_one_retry(self):
        with tempfile.TemporaryDirectory() as td:
            script = AssembledWorkflowTest()._built(Path(td), False)
            with self.assertRaisesRegex(AssertionError, "Critical 件数と findings が不一致") as caught:
                workflow_sim.simulate(script, {
                    "refix:refix-correctness-r1": {"critical_count": 3, "findings": []},
                })
            failed = caught.exception.workflow
            self.assertEqual(sum(c["label"] == "refix:refix-correctness-r1" for c in failed["calls"]), 2)
            self.assertTrue(any("予算切れ、欠員" in line for line in failed["logs"]))
            self.assertNotIn("refix-apply-r1", [c["label"] for c in failed["calls"]])
