"""Compile code is ephemeral; only diagnostic reports belong in the logs repo."""
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

SKILL_DIR = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(SKILL_DIR / "scripts"))
import scenario_compile


class ScenarioStorageTest(unittest.TestCase):
    def test_report_never_stores_code_and_scratch_is_cleaned(self):
        for outcome in ("compiled", "diagnostics", "unavailable", "exception"):
            with self.subTest(outcome=outcome), tempfile.TemporaryDirectory() as td:
                root = Path(td)
                repo = root / "moorestech"
                scenarios = repo / ".agents/skills/unity-playmode-recorded-playtest/scenarios"
                scenarios.mkdir(parents=True)
                (scenarios / "first.cs").write_text("return Foo.Api(1);\n")
                (scenarios / "second.cs").write_text("return Foo.Api(2);\n")
                project = repo / "moorestech_client"
                project.mkdir()
                logs = root / "moorestech_logs"
                report = logs / "harness/run/refix/s0-scenarios.json"
                wrapper = root / "uloop"
                fake = SKILL_DIR / "tests/fake_uloop.py"
                wrapper.write_text(f'#!/bin/sh\nexec "{sys.executable}" "{fake}" "$@"\n')
                wrapper.chmod(0o755)
                if outcome != "unavailable":
                    api = {"Foo.Api": ["string" if outcome == "diagnostics" else "int"]}
                    (repo / "fake_api.json").write_text(json.dumps(api))
                seen = []
                original = scenario_compile.run_edc

                def observe(project_path, snippet):
                    seen.append(snippet)
                    self.assertTrue(snippet.is_file())
                    self.assertNotIn(logs, snippet.parents)
                    self.assertIn("object ScenarioCompileOnly()", snippet.read_text())
                    if outcome == "exception":
                        raise OSError("injected external boundary failure")
                    return original(project_path, snippet)

                with patch.dict(os.environ, MOORES_REVIEW_ULOOP=str(wrapper)), \
                        patch.object(scenario_compile, "run_edc", side_effect=observe):
                    if outcome == "exception":
                        with self.assertRaisesRegex(OSError, "injected"):
                            scenario_compile.record(repo, report)
                    else:
                        result = scenario_compile.record(repo, report)
                        self.assertEqual(json.loads(report.read_text()), result)
                        expected = "unavailable" if outcome == "unavailable" else "ok"
                        self.assertEqual(result["status"], expected)
                        if outcome == "diagnostics":
                            self.assertTrue(all(s["diagnostics"] for s in result["scenarios"].values()))
                self.assertTrue(seen)
                self.assertEqual(len(seen), 1 if outcome in ("unavailable", "exception") else 2)
                self.assertTrue(all(not p.exists() and not p.parent.exists() for p in seen))
                self.assertEqual(list(logs.rglob("*.cs")), [])
                self.assertFalse(report.with_suffix("").exists())
