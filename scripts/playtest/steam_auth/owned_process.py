"""Bound the lifetime of an owned subprocess session, including its descendants."""
from contextlib import contextmanager
import os
import signal
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


@contextmanager
def owned_session():
    def interrupted(signum, frame):
        raise SystemExit(128 + signum)

    previous = signal.signal(signal.SIGTERM, interrupted)
    children = []
    try:
        yield children
    finally:
        # 回収中の再中断で子を残さない。
        # Do not let a second interruption abandon cleanup.
        previous_int = signal.signal(signal.SIGINT, signal.SIG_IGN)
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
        try:
            for child in children:
                reap_group(child)
        finally:
            signal.signal(signal.SIGINT, previous_int)
            signal.signal(signal.SIGTERM, previous)
