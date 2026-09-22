import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts" / "requirements"
sys.path.insert(0, str(SCRIPTS))

from input_bundle import prompt
from worker_runs import launch
try:
    from .paid_cli_guard import reject_paid_cli
except ImportError:
    from paid_cli_guard import reject_paid_cli


class RequirementWiringTests(unittest.TestCase):
    def setUp(self):
        self.paid_cli_guard = reject_paid_cli()
        self.paid_cli_guard.start()

    def tearDown(self):
        self.paid_cli_guard.stop()

    def test_documents_connect_reviewer_to_final_consumer(self):
        skill = (ROOT / "SKILL.md").read_text(encoding="utf-8")
        reviewer = (ROOT / "reviewers/core-any-user-intent-fulfillment.md").read_text(encoding="utf-8")
        integration = (ROOT / "references/integration-rules.md").read_text(encoding="utf-8")
        output = (ROOT / "references/output-contract.md").read_text(encoding="utf-8")
        integrator = (ROOT / "integrators/finding-integrator.md").read_text(encoding="utf-8")
        proof = ROOT / "references/requirement-proof.md"
        self.assertTrue(proof.is_file())
        for document in (skill, reviewer):
            self.assertIn("scripts/requirements/main.py", document)
            self.assertIn("sonnet", document)
        self.assertNotIn("動詞 + 目的語", reviewer)
        self.assertIn("裁定引用と決定文の含意チェック", reviewer)
        self.assertIn("引用文単独", reviewer)
        self.assertIn("免責", integration)
        for verdict in ("UNCONFIRMED", "MISSING", "INTERPRETATION", "OUT_OF_SCOPE"):
            self.assertIn(verdict, integration)
            self.assertIn(verdict, output)
            self.assertIn(verdict, integrator)
        self.assertIn("要求別の未確認・欠員・解釈・対象外", skill)
        inline = (ROOT / "references/orchestrator-steps.md").read_text(encoding="utf-8")
        self.assertIn("Repo root", inline)
        self.assertIn("Skill root", inline)
        self.assertIn("cwdから推測しない", inline)

    def test_import_and_path_chain_reaches_proof_and_launch(self):
        procedure = (ROOT / "references/requirement-proof.md").read_text(encoding="utf-8")
        data = {"repo": "/checkout", "procedure": procedure, "context": "要求全文",
                "patch": "差分全文", "model": "sonnet", "fingerprint": "test"}
        unit = {"id": "R001", "start": 1, "end": 1, "text": "要求全文"}
        rendered = prompt(data, unit, Path("/logs/report.md"))
        self.assertIn("# 原文要求1件の独立検査", rendered)
        self.assertIn("要求全文", rendered)
        self.assertIn("差分全文", rendered)
        self.assertTrue(callable(launch))

    def test_every_requirement_script_has_regression_banner(self):
        for script in SCRIPTS.glob("*.py"):
            text = script.read_text(encoding="utf-8")
            self.assertIn("unittest discover", text, script.name)


if __name__ == "__main__":
    unittest.main()
