#!/usr/bin/env python3
# =====================================================================
# ⚠ scripts変更後: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
# =====================================================================
"""Own worker process groups and stop only processes launched by this run."""

import os
import signal
import subprocess
import threading


class ProcessOwner:
    def __init__(self, lock_fd):
        self.lock_fd = lock_fd
        self.processes = set()
        self.lock = threading.Lock()
        self.stopping = False
        self.previous = {}

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
            process = subprocess.Popen(
                args, start_new_session=True, pass_fds=(self.lock_fd,), **options
            )
            self.processes.add(process)
            return process

    def finished(self, process):
        with self.lock:
            self.processes.discard(process)

    def stop_all(self):
        with self.lock:
            self.stopping = True
            processes = list(self.processes)
        for process in processes:
            if process.poll() is None:
                os.killpg(process.pid, signal.SIGTERM)
        for process in processes:
            if process.poll() is not None:
                continue
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
        with self.lock:
            for process in processes:
                self.processes.discard(process)
