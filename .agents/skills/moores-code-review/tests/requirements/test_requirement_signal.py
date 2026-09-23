import json
import os
import signal
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from unittest import mock

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts" / "requirements"
sys.path.insert(0, str(SCRIPTS))
import main as runtime_main
from process_owner import ProcessOwner
try:
    from .paid_cli_guard import reject_paid_cli
    from .requirement_fixture import requirement_fixture
except ImportError:
    from paid_cli_guard import reject_paid_cli
    from requirement_fixture import requirement_fixture


# 旧実装はハンドラがowner.lockを再取得して永久に止まるため、別プロセスで締切付きに走らせる
# The old handler re-took owner.lock and hung forever, so run it in a child with a deadline
LOCK_HELD_SCRIPT = """
import os, signal, subprocess, sys, time
sys.path.insert(0, sys.argv[1])
from process_owner import ProcessOwner
lock = open(sys.argv[2], "a")
owner = ProcessOwner(lock.fileno(), termination_grace=0.05)
worker = owner.spawn([sys.executable, "-c", "import time;time.sleep(60)"])
owner.install_signals()
with owner.lock:
    os.kill(os.getpid(), signal.SIGTERM)
    time.sleep(0.2)
    alive_after_signal = worker.poll() is None
print("requested", owner.stop_requested, "alive", alive_after_signal, flush=True)
owner.stop_all()
owner.restore_signals()
print("stopped", worker.returncode is not None, flush=True)
"""


class SignalPathTests(unittest.TestCase):
    def setUp(self):
        self.paid_cli_guard = reject_paid_cli()
        self.paid_cli_guard.start()

    def tearDown(self):
        self.paid_cli_guard.stop()

    def test_signal_while_owner_lock_held_only_records_request(self):
        with tempfile.TemporaryDirectory() as temp:
            result = subprocess.run([sys.executable, "-c", LOCK_HELD_SCRIPT, str(SCRIPTS),
                                     str(Path(temp) / "lock")],
                                    capture_output=True, text=True, timeout=20, check=False)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.split(), ["requested", "True", "alive", "True", "stopped", "True"])

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
                    deadline = time.monotonic() + 2
                    while not owner.stop_requested and time.monotonic() < deadline:
                        time.sleep(0.01)
                    self.assertTrue(owner.stop_requested)
                    self.assertEqual(len(owner.processes), 1)
                    owner.stop_all()
                    failures = [future.exception() for future in futures]
                self.assertEqual(len(count), 1)
                self.assertIsNone(failures[0])
                self.assertTrue(all(isinstance(error, OSError) for error in failures[1:]))
            finally:
                owner.stop_all()
                owner.restore_signals()
            lock.close()

    def test_execute_stops_once_from_control_flow_after_signal(self):
        released = threading.Event()
        calls = []

        def stop_all(owner):
            calls.append(threading.current_thread() is threading.main_thread())
            owner.stopping = True
            released.set()

        def signalled_launch(_data, unit, _directory, _owner):
            os.kill(os.getpid(), signal.SIGTERM)
            self.assertTrue(released.wait(5))
            return {"id": unit["id"], "verdict": "MISSING", "reason": "stopped"}

        with tempfile.TemporaryDirectory() as temp:
            args, fake_main = requirement_fixture(Path(temp))
            previous = signal.getsignal(signal.SIGTERM)
            with mock.patch.object(runtime_main, "__file__", str(fake_main)), \
                 mock.patch("main.launch", side_effect=signalled_launch), \
                 mock.patch.object(ProcessOwner, "stop_all", autospec=True, side_effect=stop_all):
                self.assertEqual(runtime_main.execute(args), 2)
            self.assertEqual(calls, [True])
            self.assertIs(signal.getsignal(signal.SIGTERM), previous)

    def test_signal_during_supported_worker_is_cancelled_run_not_success(self):
        # 旧実装は全件SUPPORTEDなら停止要求を無視して0を返し、中断runを成功と報告した
        # The old code ignored the stop request when every unit was SUPPORTED and returned 0
        supported = {"id": "R001", "verdict": "SUPPORTED", "report": "根拠の本文",
                     "reportPath": "/logs/R001/attempt-1/report.md", "sha256": "x"}

        def signalled_launch(_data, unit, _directory, owner):
            os.kill(os.getpid(), signal.SIGTERM)
            deadline = time.monotonic() + 2
            while not owner.stop_requested and time.monotonic() < deadline:
                time.sleep(0.01)
            return dict(supported, id=unit["id"])

        with tempfile.TemporaryDirectory() as temp:
            args, fake_main = requirement_fixture(Path(temp))
            previous = signal.getsignal(signal.SIGTERM)
            with mock.patch.object(runtime_main, "__file__", str(fake_main)), \
                 mock.patch("main.launch", side_effect=signalled_launch), \
                 mock.patch("sys.stderr"):
                self.assertEqual(runtime_main.execute(args), 2)
            self.assertIs(signal.getsignal(signal.SIGTERM), previous)
            results = json.loads(Path(args.run_dir, "results.json").read_text(encoding="utf-8"))
            self.assertEqual(results["results"], [supported])
            self.assertIs(results["stopRequested"], True)
            self.assertEqual(results["missing"], [])
            self.assertEqual(results["runnerFailures"], [])
            self.assertIs(results["codeChanged"], False)
            summary = Path(args.run_dir, "summary.md").read_text(encoding="utf-8")
            self.assertIn("## R001: 静的根拠あり", summary)
            self.assertIn("停止要求（SIGTERM/SIGINT）: あり", summary)


if __name__ == "__main__":
    unittest.main()
