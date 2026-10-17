#!/usr/bin/env python3
"""Check cached authentication only; never fetch or submit a password."""
import os
import re
import signal
import subprocess
import sys
from pathlib import Path


def check(executable, username):
    try:
        child = subprocess.Popen([executable, '+login', username, '+quit'], stdin=subprocess.DEVNULL,
                                 stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                                 start_new_session=True)
    except OSError:
        print('ERROR: cached Steam login could not start', file=sys.stderr)
        return 2
    try:
        output, _ = child.communicate(timeout=120)
    except subprocess.TimeoutExpired:
        # wrapperだけでなく、その子SteamCMDも止めてロックを解放する。
        # Kill the owned process group, including SteamCMD, to release the lock.
        os.killpg(child.pid, signal.SIGKILL)
        child.communicate()
        print('ERROR: cached Steam login timed out; check connectivity and authenticate explicitly', file=sys.stderr)
        return 4
    authenticated = re.search(r'Waiting for user info\.\.\.OK|Logged in OK|Logging in user.*OK', output, re.I)
    if child.returncode or not authenticated:
        print('ERROR: cached Steam login unavailable; run --login-only before building', file=sys.stderr)
        return 5
    print('cached_login_ok')
    return 0


if __name__ == '__main__':
    sys.exit(check(sys.argv[2] if len(sys.argv) > 2 else str(Path(__file__).with_name('run.py')), sys.argv[1]))
