#!/usr/bin/env python3
# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須
#   （tests/test_skill_wiring.py が全スクリプトのバナー実在を機械検証する）。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""Own worker process groups and stop only processes launched by this run."""

import os
import signal
import subprocess
import sys
import threading
import time


class ProcessOwner:
    def __init__(self, lock_fd, termination_grace=10):
        self.lock_fd = lock_fd
        self.termination_grace = termination_grace
        self.processes = set()
        self.lock = threading.Lock()
        self.stopping = False
        self.stop_requested = False
        self.previous = {}
        self.failures = []

    def install_signals(self):
        for signum in (signal.SIGTERM, signal.SIGINT):
            self.previous[signum] = signal.getsignal(signum)
            signal.signal(signum, self._signal)

    def restore_signals(self):
        for signum, handler in self.previous.items():
            signal.signal(signum, handler)
        self.previous.clear()

    def _signal(self, _signum, _frame):
        # ハンドラは記録だけ。lock/wait/subprocessは再入で自己デッドロックする
        # Record only: lock, wait or subprocess here can deadlock on reentry
        self.stop_requested = True

    def spawn(self, args, **options):
        with self.lock:
            if self.stopping or self.stop_requested:
                raise OSError("runner is stopping; worker was not started")
            process = subprocess.Popen(args, start_new_session=True,
                                       pass_fds=(self.lock_fd,), **options)
            self.processes.add(process)
            return process

    def finished(self, process):
        with self.lock:
            self.processes.discard(process)

    def complete(self, process):
        if self._group_exists(process.pid):
            self.stop(process)
            return
        self.finished(process)

    def stop(self, process):
        self._signal_group(process.pid, signal.SIGTERM)
        deadline = time.monotonic() + self.termination_grace
        while self._group_exists(process.pid) and time.monotonic() < deadline:
            time.sleep(0.02)
        if self._group_exists(process.pid):
            self._signal_group(process.pid, signal.SIGKILL)
        try:
            process.wait()
        except ChildProcessError:
            pass
        self.finished(process)

    def stop_all(self):
        with self.lock:
            self.stopping = True
            processes = list(self.processes)
        for process in processes:
            try:
                self.stop(process)
            except (OSError, subprocess.SubprocessError) as error:
                message = f"process group {process.pid} cleanup failure: {error}"
                print(message, file=sys.stderr)
                with self.lock:
                    self.failures.append(message)

    @staticmethod
    def _signal_group(group, signum):
        try:
            os.killpg(group, signum)
        except ProcessLookupError:
            pass
        except PermissionError:
            if ProcessOwner._live_group_members(group):
                raise

    @staticmethod
    def _group_exists(group):
        try:
            os.killpg(group, 0)
            return True
        except ProcessLookupError:
            return False
        except PermissionError:
            return ProcessOwner._live_group_members(group)

    @staticmethod
    def _live_group_members(group):
        result = subprocess.run(["ps", "-axo", "pgid=,stat="], check=True,
                                capture_output=True, text=True)
        for line in result.stdout.splitlines():
            fields = line.split()
            if len(fields) == 2 and fields[0] == str(group) and not fields[1].startswith("Z"):
                return True
        return False
