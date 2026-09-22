import fcntl
import json
import os
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts" / "requirements"
sys.path.insert(0, str(SCRIPTS))
import main as runtime_main
from process_owner import ProcessOwner
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
            (old / "status.json").write_text("{broken", encoding="utf-8")
            with mock.patch("sys.stderr"):
                result = launch(data, unit, root, FakeOwner(FakeProcess))
            self.assertEqual(result["verdict"], "SUPPORTED")
            self.assertEqual((old / "status.json").read_text(), "{broken")
            self.assertTrue((root / "attempt-2" / "status.json").is_file())

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


class ProcessOwnershipTests(unittest.TestCase):
    def test_stop_kills_term_ignoring_group_and_prevents_new_spawn(self):
        with tempfile.TemporaryDirectory() as temp:
            lock = (Path(temp) / "lock").open("a")
            owner = ProcessOwner(lock.fileno())
            process = owner.spawn([sys.executable, "-c",
                                   "import signal,time; signal.signal(signal.SIGTERM, signal.SIG_IGN); time.sleep(60)"],
                                  stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                  stderr=subprocess.DEVNULL)
            time.sleep(0.1)
            with mock.patch("process_owner.subprocess.TimeoutExpired", subprocess.TimeoutExpired), \
                 mock.patch.object(process, "wait", side_effect=[subprocess.TimeoutExpired("x", 10), 0]):
                with mock.patch("process_owner.os.killpg", wraps=os.killpg) as killer:
                    owner.stop_all()
                    self.assertEqual([call.args[1] for call in killer.call_args_list],
                                     [signal.SIGTERM, signal.SIGKILL])
            with self.assertRaises(OSError):
                owner.spawn(["never"])
            lock.close()

    def test_inherited_lock_blocks_resume_after_parent_sigkill(self):
        with tempfile.TemporaryDirectory() as temp:
            lock_path = Path(temp) / "run.lock"
            script = ("import fcntl,os,subprocess,sys,time\n"
                      "f=open(sys.argv[1],'a'); fcntl.flock(f,fcntl.LOCK_EX|fcntl.LOCK_NB)\n"
                      "p=subprocess.Popen([sys.executable,'-c','import time;time.sleep(60)'],pass_fds=(f.fileno(),),start_new_session=True)\n"
                      "print(p.pid,flush=True); time.sleep(60)\n")
            parent = subprocess.Popen([sys.executable, "-c", script, str(lock_path)],
                                      stdout=subprocess.PIPE, text=True)
            child_pid = int(parent.stdout.readline())
            os.kill(parent.pid, signal.SIGKILL)
            parent.wait()
            contender = lock_path.open("a")
            with self.assertRaises(BlockingIOError):
                fcntl.flock(contender, fcntl.LOCK_EX | fcntl.LOCK_NB)
            os.killpg(child_pid, signal.SIGKILL)
            contender.close()
            parent.stdout.close()
