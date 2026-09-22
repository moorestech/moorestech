#!/usr/bin/env python3
# =====================================================================
# ⚠ scripts変更後: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
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
        self.stop_all()

    def spawn(self, args, **options):
        with self.lock:
            if self.stopping:
                raise OSError("runner is stopping; worker was not started")
            process = subprocess.Popen(args, start_new_session=True,
                                       pass_fds=(self.lock_fd,), **options)
            self.processes.add(process)
            return process

    def finished(self, process):
        with self.lock:
            self.processes.discard(process)

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
