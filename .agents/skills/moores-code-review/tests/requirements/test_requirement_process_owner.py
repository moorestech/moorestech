import fcntl
import os
import signal
import subprocess
import sys
import tempfile
import threading
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from unittest import mock

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts" / "requirements"
sys.path.insert(0, str(SCRIPTS))
from process_owner import ProcessOwner
from worker_runs import launch


class ProcessOwnershipTests(unittest.TestCase):
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

    def test_signal_stops_running_worker_and_queued_work_never_spawns(self):
        with tempfile.TemporaryDirectory() as temp:
            lock = (Path(temp) / "lock").open("a")
            owner = ProcessOwner(lock.fileno(), termination_grace=0.05)
            started = threading.Event()
            count = []

            def work():
                process = owner.spawn([sys.executable, "-c", "import time;time.sleep(60)"],
                                      stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                      stderr=subprocess.DEVNULL)
                count.append(process.pid)
                started.set()
                process.wait()
                owner.finished(process)

            owner.install_signals()
            try:
                with ThreadPoolExecutor(max_workers=1) as pool:
                    futures = [pool.submit(work) for _ in range(4)]
                    self.assertTrue(started.wait(2))
                    os.kill(os.getpid(), signal.SIGTERM)
                    failures = [future.exception() for future in futures]
                self.assertEqual(len(count), 1)
                self.assertIsNone(failures[0])
                self.assertTrue(all(isinstance(error, OSError) for error in failures[1:]))
            finally:
                owner.stop_all()
                owner.restore_signals()
            lock.close()

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
            contender = lock_path.open("a")
            try:
                os.kill(parent.pid, signal.SIGKILL)
                parent.wait()
                with self.assertRaises(BlockingIOError):
                    fcntl.flock(contender, fcntl.LOCK_EX | fcntl.LOCK_NB)
            finally:
                try:
                    os.killpg(child_pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                if parent.poll() is None:
                    parent.kill()
                    parent.wait()
                contender.close()
                parent.stdout.close()
