"""Bound the lifetime of an owned subprocess session, including its descendants."""
from contextlib import contextmanager
import os
import signal
import subprocess
import sys
import time


def signal_group(pid, signum):
    # プロセス境界では終了競合だけを許容する。
    # Only tolerate an already-gone process group at this OS boundary.
    try:
        os.killpg(pid, signum)
        return True
    except ProcessLookupError:
        return False  # silent-ok: 回収対象は既に終了済み / 呼び出し元が最終状態を報告


def reap_group(child):
    signal_group(child.pid, signal.SIGTERM)
    deadline = time.monotonic() + 2
    # リーダー終了後もグループを確認し、孫が保持するロックを解放する。
    # Reap the leader and check descendants that may still hold the lock.
    while time.monotonic() < deadline:
        child.poll()
        if not signal_group(child.pid, 0):
            break
        time.sleep(0.05)
    signal_group(child.pid, signal.SIGKILL)
    child.wait(timeout=10)


class OwnedProcesses:
    def __init__(self):
        self.children = []
        self.spawning = False
        self.pending_signal = None

    def interrupted(self, signum, frame):
        if self.spawning:
            # 所有登録まで中断を延期し、子にシグナルマスクを継承させない。
            # Defer interruption until registration without masking child signals.
            self.pending_signal = signum
            return
        if signum == signal.SIGINT:
            raise KeyboardInterrupt
        raise SystemExit(128 + signum)

    def spawn(self, command, **options):
        self.spawning = True
        try:
            child = subprocess.Popen(command, **options)
            self.children.append(child)
        finally:
            self.spawning = False
        if self.pending_signal is not None:
            self.interrupted(self.pending_signal, None)
        return child


@contextmanager
def owned_session():
    owner = OwnedProcesses()
    previous = signal.signal(signal.SIGTERM, owner.interrupted)
    previous_int = signal.signal(signal.SIGINT, owner.interrupted)
    try:
        yield owner
    finally:
        # 回収中の再中断で子を残さない。
        # Do not let a second interruption abandon cleanup.
        signal.signal(signal.SIGINT, signal.SIG_IGN)
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
        try:
            for child in owner.children:
                reap_group(child)
        finally:
            signal.signal(signal.SIGINT, previous_int)
            signal.signal(signal.SIGTERM, previous)


if __name__ == '__main__':
    # 通常アップロードも専用グループで所有し、終了時に子孫を回収する。
    # Own normal uploads in a separate group and reap descendants on exit.
    with owned_session() as owner:
        child = owner.spawn(sys.argv[1:], start_new_session=True)
        result = child.wait()
    sys.exit(result if result >= 0 else 128 - result)
