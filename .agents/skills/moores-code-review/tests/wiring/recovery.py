import re
import unittest
from pathlib import Path
from wiring.context import SKILL_DIR, SKILL_MD, REPO_ROOT, WORKFLOW_SRC

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


