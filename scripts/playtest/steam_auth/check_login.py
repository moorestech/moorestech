#!/usr/bin/env python3
"""Check cached authentication only; never fetch or submit a password."""
import argparse
import re
import subprocess
import sys
from pathlib import Path
from owned_process import owned_session

STATES = {0: 'cached_login_ok', 2: 'steam_setup_failed', 4: 'steam_check_timeout',
          5: 'cached_login_unavailable', 6: 'steam_state_in_use',
          7: 'steam_executable_missing', 8: 'steam_check_failed'}


def check(username):
    # 外部プロセス境界を囲み、中断・通信失敗でも所有グループを回収する。
    # Bound the process lifetime across interruptions and communication failures.
    try:
        with owned_session() as owner:
            child = owner.spawn([str(Path(__file__).with_name('run.py')), '+login', username, '+quit'],
                                     stdin=subprocess.DEVNULL, stdout=subprocess.PIPE,
                                     stderr=subprocess.STDOUT, text=True, start_new_session=True)
            output, _ = child.communicate(timeout=120)
    except subprocess.TimeoutExpired:
        print('ERROR: cached Steam login timed out; check connectivity and retry', file=sys.stderr)
        return 4
    except (OSError, subprocess.SubprocessError):
        print('ERROR: cached Steam login check failed; check local configuration', file=sys.stderr)
        return 8
    # ランナーの固定診断だけを伝え、生出力や推測した認証原因を公開しない。
    # Forward only fixed runner diagnostics, never raw output or inferred auth causes.
    reasons = {'state_in_use': 6, 'setup_failed': 2, 'executable_missing': 7}
    reason = re.search(r'^STEAM_RUNNER_ERROR:(state_in_use|setup_failed|executable_missing)\b', output, re.M)
    if child.returncode == 2 and reason:
        result = reasons[reason[1]]
        print('ERROR: ' + STATES[result], file=sys.stderr)
        return result
    authenticated = re.search(r'Waiting for user info\.\.\.OK|Logged in OK|Logging in user.*OK', output, re.I)
    if child.returncode == 0 and authenticated:
        print('cached_login_ok')
        return 0
    rejected = re.search(r'Invalid Password|Account Logon Denied|Cached credentials not found', output, re.I)
    if rejected:
        print('ERROR: cached Steam login unavailable; run --login-only before building', file=sys.stderr)
        return 5
    print('ERROR: steam_check_failed; check connectivity and retry', file=sys.stderr)
    return 8


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('username')
    sys.exit(check(parser.parse_args().username))
