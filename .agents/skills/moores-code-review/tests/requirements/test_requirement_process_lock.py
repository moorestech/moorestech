import fcntl
import os
import signal
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts" / "requirements"
sys.path.insert(0, str(SCRIPTS))
try:
    from .paid_cli_guard import reject_paid_cli
except ImportError:
    from paid_cli_guard import reject_paid_cli


class ProcessLockTests(unittest.TestCase):
    def setUp(self):
        self.paid_cli_guard = reject_paid_cli()
        self.paid_cli_guard.start()

    def tearDown(self):
        self.paid_cli_guard.stop()

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
