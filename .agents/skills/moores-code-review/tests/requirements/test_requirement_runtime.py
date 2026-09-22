import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts" / "requirements"
sys.path.insert(0, str(SCRIPTS))
from worker_runs import launch, read_report


REPORT = ("Requirement: R001\nVerdict: SUPPORTED\n## 原文と観測\nx\n"
          "## 経路と証拠\ny\n## 差と限界\nz\n")


class FakeProcess:
    returncode = 0
    pid = 999999

    def __init__(self, report=None):
        self.report = report
        self.calls = []

    def communicate(self, text=None, timeout=None):
        self.calls.append((text, timeout))
        if self.report:
            self.report.write_text(REPORT, encoding="utf-8")

    def poll(self):
        return self.returncode


class FakeOwner:
    def __init__(self, factory):
        self.factory = factory
        self.options = None

    def spawn(self, args, **options):
        self.options = options
        return self.factory(Path(args[args.index("--add-dir") + 1]) / "report.md")

    def finished(self, _process):
        pass


class RequirementReportTests(unittest.TestCase):
    def test_typed_verdict_and_identity(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "report.md"
            self.assertIsNone(read_report(path, "R001"))
            path.write_text(REPORT, encoding="utf-8")
            self.assertEqual(read_report(path, "R001")["verdict"], "SUPPORTED")
            self.assertIsNone(read_report(path, "R002"))
            path.write_text(REPORT + "Verdict: SUPPORTED\n", encoding="utf-8")
            self.assertIsNone(read_report(path, "R001"))

    def test_child_environment_only_removes_claudecode_and_success_resumes(self):
        data = {"model": "sonnet", "repo": "/tmp", "fingerprint": "fp",
                "procedure": "p", "context": "c", "patch": "d"}
        unit = {"id": "R001", "start": 1, "end": 1, "text": "c"}
        with tempfile.TemporaryDirectory() as temp, mock.patch.dict(os.environ, {"CLAUDECODE": "parent", "KEEP": "yes"}):
            owner = FakeOwner(FakeProcess)
            first = launch(data, unit, Path(temp) / "R001", owner)
            self.assertEqual(first["verdict"], "SUPPORTED")
            self.assertNotIn("CLAUDECODE", owner.options["env"])
            self.assertEqual(owner.options["env"]["KEEP"], "yes")
            self.assertEqual(os.environ["CLAUDECODE"], "parent")
            second_owner = FakeOwner(lambda _path: self.fail("successful attempt relaunched"))
            self.assertEqual(launch(data, unit, Path(temp) / "R001", second_owner)["verdict"], "SUPPORTED")

    def test_corrupt_status_is_preserved_and_retried(self):
        data = {"model": "sonnet", "repo": "/tmp", "fingerprint": "fp",
                "procedure": "p", "context": "c", "patch": "d"}
        unit = {"id": "R001", "start": 1, "end": 1, "text": "c"}
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "R001"
            old = root / "attempt-1"
            old.mkdir(parents=True)
            (old / "status.json").write_bytes(b"\xffbroken")
            with mock.patch("sys.stderr"):
                result = launch(data, unit, root, FakeOwner(FakeProcess))
            self.assertEqual(result["verdict"], "SUPPORTED")
            self.assertEqual((old / "status.json").read_bytes(), b"\xffbroken")
            self.assertTrue((root / "attempt-2" / "status.json").is_file())

    def test_non_utf8_report_is_missing(self):
        with tempfile.TemporaryDirectory() as temp, mock.patch("sys.stderr"):
            path = Path(temp) / "report.md"
            path.write_bytes(b"\xffbroken")
            self.assertIsNone(read_report(path, "R001"))

    def test_nonzero_missing_report_and_changed_report_are_not_success(self):
        data = {"model": "sonnet", "repo": "/tmp", "fingerprint": "fp",
                "procedure": "p", "context": "c", "patch": "d"}
        unit = {"id": "R001", "start": 1, "end": 1, "text": "c"}
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "R001"
            missing = launch(data, unit, root, FakeOwner(lambda _path: FakeProcess()))
            self.assertEqual(missing["verdict"], "MISSING")
            process = FakeProcess()
            process.returncode = 7
            failed = launch(data, unit, root, FakeOwner(lambda path: (setattr(process, "report", path) or process)))
            self.assertEqual(failed["verdict"], "MISSING")
            good = launch(data, unit, root, FakeOwner(FakeProcess))
            self.assertEqual(good["verdict"], "SUPPORTED")
            report = root / "attempt-3" / "report.md"
            report.write_text(REPORT + "changed", encoding="utf-8")
            launch(data, unit, root, FakeOwner(FakeProcess))
            self.assertTrue((root / "attempt-4").is_dir())
