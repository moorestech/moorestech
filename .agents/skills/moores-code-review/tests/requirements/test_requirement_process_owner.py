import fcntl
import os
import signal
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts" / "requirements"
sys.path.insert(0, str(SCRIPTS))
from process_owner import ProcessOwner
from worker_runs import launch
try:
    from .paid_cli_guard import reject_paid_cli
except ImportError:
    from paid_cli_guard import reject_paid_cli


class ProcessOwnershipTests(unittest.TestCase):
    def setUp(self):
        self.paid_cli_guard = reject_paid_cli()
        self.paid_cli_guard.start()

    def tearDown(self):
        self.paid_cli_guard.stop()

    def test_stop_kills_term_ignoring_descendant_reaps_leader_and_releases_lock(self):
        with tempfile.TemporaryDirectory() as temp:
            lock_path = Path(temp) / "lock"
            lock = lock_path.open("a")
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            owner = ProcessOwner(lock.fileno(), termination_grace=0.05)
            child = ("import os,signal,time;signal.signal(signal.SIGTERM,signal.SIG_IGN);"
                     "print(os.getpid(),flush=True);time.sleep(60)")
            script = ("import subprocess,sys,time\n"
                      f"subprocess.Popen([sys.executable,'-c',{child!r}],pass_fds=({lock.fileno()},))\n"
                      "time.sleep(60)\n")
            process = owner.spawn([sys.executable, "-c", script], stdin=subprocess.DEVNULL,
                                  stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
            child_pid = int(process.stdout.readline())
            try:
                owner.stop_all()
                self.assertIsNotNone(process.returncode)
                self.assertEqual(subprocess.run(["ps", "-p", str(child_pid), "-o", "pid="],
                                                capture_output=True, check=False).returncode, 1)
                with self.assertRaises(ChildProcessError):
                    os.waitpid(process.pid, os.WNOHANG)
                with self.assertRaises(OSError):
                    owner.spawn(["never"])
                lock.close()
                contender = lock_path.open("a")
                fcntl.flock(contender, fcntl.LOCK_EX | fcntl.LOCK_NB)
                contender.close()
            finally:
                owner.stop_all()
                lock.close()
                process.stdout.close()

    def test_communicate_failure_stops_and_reaps_worker(self):
        class FailingOwner(ProcessOwner):
            process = None

            def spawn(self, _args, **options):
                self.process = super().spawn([sys.executable, "-c", "import time;time.sleep(60)"],
                                             **options)
                self.process.communicate = mock.Mock(side_effect=OSError("stdin failed"))
                return self.process

        data = {"model": "sonnet", "repo": "/tmp", "fingerprint": "fp",
                "procedure": "p", "context": "c", "patch": "d"}
        unit = {"id": "R001", "start": 1, "end": 1, "text": "c"}
        with tempfile.TemporaryDirectory() as temp:
            lock = (Path(temp) / "lock").open("a")
            owner = FailingOwner(lock.fileno(), termination_grace=0.05)
            try:
                result = launch(data, unit, Path(temp) / "R001", owner)
                self.assertEqual(result["verdict"], "MISSING")
                self.assertIsNotNone(owner.process.returncode)
                with self.assertRaises(ChildProcessError):
                    os.waitpid(owner.process.pid, os.WNOHANG)
            finally:
                owner.stop_all()
                lock.close()

    def test_cleanup_failure_is_logged_and_kept_owned(self):
        with tempfile.TemporaryDirectory() as temp:
            lock = (Path(temp) / "lock").open("a")
            owner = ProcessOwner(lock.fileno(), termination_grace=0)
            process = mock.Mock(pid=12345)
            owner.processes.add(process)
            with mock.patch.object(owner, "stop", side_effect=PermissionError("denied")), \
                 mock.patch("sys.stderr") as stderr:
                owner.stop_all()
            self.assertIn(process, owner.processes)
            self.assertIn("denied", owner.failures[0])
            stderr.write.assert_called()
            owner.processes.clear()
            lock.close()

    def test_worker_final_cleanup_failure_keeps_group_owned(self):
        class BrokenOwner(ProcessOwner):
            process = mock.Mock(pid=12345, returncode=-15)
            process.poll.return_value = -15
            process.communicate.side_effect = OSError("stdin failed")
            process.stdin = mock.Mock()

            def spawn(self, _args, **_options):
                self.processes.add(self.process)
                return self.process

        data = {"model": "sonnet", "repo": "/tmp", "fingerprint": "fp",
                "procedure": "p", "context": "c", "patch": "d"}
        unit = {"id": "R001", "start": 1, "end": 1, "text": "c"}
        with tempfile.TemporaryDirectory() as temp:
            lock = (Path(temp) / "lock").open("a")
            owner = BrokenOwner(lock.fileno(), termination_grace=0)
            try:
                with mock.patch.object(owner, "stop", side_effect=PermissionError("denied")), \
                     mock.patch.object(owner, "_group_exists", return_value=True), \
                     mock.patch("sys.stderr"):
                    result = launch(data, unit, Path(temp) / "R001", owner)
                self.assertEqual(result["verdict"], "MISSING")
                self.assertIn(owner.process, owner.processes)
            finally:
                owner.processes.clear()
                lock.close()

    def test_paid_cli_is_rejected_before_process_start(self):
        with tempfile.TemporaryDirectory() as temp:
            lock = (Path(temp) / "lock").open("a")
            owner = ProcessOwner(lock.fileno())
            with self.assertRaisesRegex(AssertionError, "paid Claude"):
                owner.spawn(["claude", "-p"], stdin=subprocess.DEVNULL,
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            lock.close()

    def test_normal_leader_completion_still_cleans_owned_descendant(self):
        with tempfile.TemporaryDirectory() as temp:
            lock_path = Path(temp) / "lock"
            lock = lock_path.open("a")
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            owner = ProcessOwner(lock.fileno(), termination_grace=0.05)
            child = ("import os,signal,time;signal.signal(signal.SIGTERM,signal.SIG_IGN);"
                     "print(os.getpid(),flush=True);time.sleep(60)")
            script = ("import subprocess,sys\n"
                      f"subprocess.Popen([sys.executable,'-c',{child!r}],pass_fds=({lock.fileno()},))\n")
            process = owner.spawn([sys.executable, "-c", script], stdin=subprocess.DEVNULL,
                                  stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
            child_pid = int(process.stdout.readline())
            try:
                process.wait()
                owner.complete(process)
                self.assertNotIn(process, owner.processes)
                self.assertEqual(subprocess.run(["ps", "-p", str(child_pid), "-o", "pid="],
                                                capture_output=True, check=False).returncode, 1)
                lock.close()
                contender = lock_path.open("a")
                fcntl.flock(contender, fcntl.LOCK_EX | fcntl.LOCK_NB)
                contender.close()
            finally:
                owner.stop_all()
                lock.close()
                process.stdout.close()
