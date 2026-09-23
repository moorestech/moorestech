import argparse
import json
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "scripts" / "requirements"
sys.path.insert(0, str(SCRIPTS))

from input_bundle import prompt
from worker_runs import launch
import main as runtime_main
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

    def test_main_executes_bundle_launch_proof_report_and_summary(self):
        class FakeProcess:
            pid = 987654
            returncode = 0
            stdin = None

            def communicate(self, text, timeout):
                destination = Path(re.search(r"唯一の書込先: (.+)", text).group(1))
                destination.write_text(
                    "Requirement: R001\nVerdict: SUPPORTED\n\n## 原文と観測\nok\n"
                    "## 経路と証拠\nok\n## 差と限界\nok\n", encoding="utf-8")

            def poll(self):
                return 0

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            repo = root / "repo"
            subprocess.run(["git", "init", "-q", str(repo)], check=True)
            subprocess.run(["git", "-C", str(repo), "config", "user.email", "test@example.com"], check=True)
            subprocess.run(["git", "-C", str(repo), "config", "user.name", "Test"], check=True)
            (repo / "tracked").write_text("base", encoding="utf-8")
            subprocess.run(["git", "-C", str(repo), "add", "tracked"], check=True)
            subprocess.run(["git", "-C", str(repo), "commit", "-qm", "initial"], check=True)
            context, patch_path = root / "context.md", root / "patch.diff"
            context.write_text("要求全文", encoding="utf-8")
            patch_path.write_text("差分全文", encoding="utf-8")
            args = argparse.Namespace(repo_root=str(repo), context=str(context), patch=str(patch_path),
                                      run_dir=str(root / "run"), model="sonnet")
            with mock.patch("main.ProcessOwner.install_signals"), \
                 mock.patch("main.ProcessOwner.restore_signals"), \
                 mock.patch("main.ProcessOwner.stop_all"), \
                 mock.patch("main.ProcessOwner.complete"), \
                 mock.patch("main.ProcessOwner.spawn", return_value=FakeProcess()):
                self.assertEqual(runtime_main.execute(args), 0)
            results = json.loads((root / "run/results.json").read_text(encoding="utf-8"))
            row = results["results"][0]
            report_path = Path(row["reportPath"])
            summary = (root / "run/summary.md").read_text(encoding="utf-8")
            launch_prompt = (root / "run/R001/attempt-1/prompt.md").read_text(encoding="utf-8")
            self.assertTrue(report_path.is_file())
            self.assertIn("# 原文要求1件の独立検査", launch_prompt)
            self.assertIn("要求全文", launch_prompt)
            self.assertIn("差分全文", launch_prompt)
            self.assertIn(f"個別報告先: {report_path}", summary)


if __name__ == "__main__":
    unittest.main()
