#!/usr/bin/env python3
"""Upload a run, or use <run-dir> --login-only / --check-login for isolated authentication."""
import argparse
import errno
import importlib.util
import json
import os
import pty
import re
import select
import subprocess
import sys
import termios
import time
from pathlib import Path
from steam_vault_environment import configure, required

PARSER = argparse.ArgumentParser(description=__doc__)
PARSER.add_argument('run_dir', type=Path)
MODES = PARSER.add_mutually_exclusive_group()
MODES.add_argument('--login-only', action='store_true')
MODES.add_argument('--check-login', action='store_true')
ARGS = PARSER.parse_args()
ROOT = ARGS.run_dir.expanduser()
RUNNER = Path(__file__).resolve().parents[4] / 'scripts/playtest/steam_auth/run.py'
STATUS = ROOT / 'steam-upload-status.json'
GUARD_CODE = ROOT / 'steam-guard-code.txt'


def report(state, **extra):
    result = {'state': state, 'helperPid': os.getpid(), **extra}
    STATUS.write_text(json.dumps(result, ensure_ascii=False))
    print(json.dumps(result, ensure_ascii=False), flush=True)


def credential():
    spec = importlib.util.spec_from_file_location('hermes_onepassword', required('MOORESTECH_STEAM_OP_HELPER'))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    item = module.get_item(required('MOORESTECH_STEAM_OP_ITEM'), module.DEFAULT_VAULT, reveal=True)
    return module.item_fields(item)


def conversation(child, parent_fd, password):
    buffer, sent_password, authenticated, guard_reported = '', False, False, False
    deadline = time.monotonic() + 3600
    disconnected = False
    while time.monotonic() < deadline:
        ready, _, _ = select.select([parent_fd], [], [], 1)
        if ready:
            # PTY切断と実際の期限超過を分けるため、EOFまで残り出力を読む。
            # Drain final output and distinguish PTY disconnection from deadline expiry.
            try:
                chunk = os.read(parent_fd, 65536)
            except OSError as error:
                if error.errno != errno.EIO:
                    raise
                chunk = b''
            if not chunk:
                disconnected = True
                break
            buffer = (buffer + chunk.decode('utf-8', errors='replace'))[-32000:]
            if not sent_password and re.search(r'password:\s*$', buffer, re.I):
                os.write(parent_fd, password.encode() + b'\n')
                sent_password, buffer = True, ''
                report('password_submitted', steamPid=child.pid)
            if re.search(r'Invalid Password|Account Logon Denied', buffer, re.I):
                report('steam_rejected_credential', steamPid=child.pid)
                return 3
            if not guard_reported and re.search(r'Steam Guard|authenticator|two.factor|check your email', buffer, re.I):
                guard_reported = True
                report('steam_guard_required', steamPid=child.pid)
            if not authenticated and re.search(r'Waiting for user info\.\.\.OK|Logged in OK|Logging in user.*OK', buffer, re.I):
                authenticated = True
                report('authenticated' if ARGS.login_only else 'authenticated_uploading', steamPid=child.pid)
        if guard_reported and GUARD_CODE.exists():
            code = GUARD_CODE.read_text().strip()
            GUARD_CODE.unlink()
            if not re.fullmatch('[A-Za-z0-9]{5,8}', code):
                report('guard_code_invalid')
                return 2
            os.write(parent_fd, code.encode() + b'\n')
            report('guard_code_submitted', steamPid=child.pid)
    try:
        child.wait(timeout=10)
    except subprocess.TimeoutExpired:
        report('steam_pty_disconnected' if disconnected else
               ('upload_deadline_exceeded' if authenticated else 'authentication_deadline_exceeded'))
        return 4
    if authenticated and child.returncode == 0:
        report('login_ok' if ARGS.login_only else 'uploaded', steamExit=0)
        return 0
    state = 'steam_guard_expired' if guard_reported and child.returncode == 5 else 'steam_process_failed'
    report(state, steamExit=child.returncode)
    return 5


def run():
    os.umask(0o077)
    ROOT.mkdir(parents=True, exist_ok=True)
    configure()
    if ARGS.check_login:
        result = subprocess.run([sys.executable, str(RUNNER.with_name('check_login.py')),
                                 required('MOORESTECH_STEAM_USER')])
        report('cached_login_ok' if result.returncode == 0 else 'cached_login_unavailable')
        return result.returncode
    GUARD_CODE.unlink(missing_ok=True)
    report('reading_credential')
    username, password = credential()
    if not username or not password or any(c in username + password for c in '\r\n\0'):
        report('credential_unusable')
        return 2
    if username != required('MOORESTECH_STEAM_USER'):
        report('account_mismatch')
        return 2
    command = [str(RUNNER), '+login', username]
    if not ARGS.login_only:
        vdf = ROOT / 'steam/app_build_playtest.vdf'
        if not vdf.is_file():
            report('upload_vdf_missing')
            return 2
        command += ['+run_app_build', str(vdf)]
    parent_fd, child_fd = pty.openpty()
    attributes = termios.tcgetattr(child_fd)
    attributes[3] &= ~termios.ECHO
    termios.tcsetattr(child_fd, termios.TCSANOW, attributes)
    child = subprocess.Popen(command + ['+quit'], stdin=child_fd, stdout=child_fd,
                             stderr=child_fd, start_new_session=True)
    os.close(child_fd)
    report('steam_started', steamPid=child.pid)
    try:
        return conversation(child, parent_fd, password)
    finally:
        if child.poll() is None:
            # セッションごと止め、孫のSteamCMDを残さない。
            # Terminate the owned process group so SteamCMD cannot outlive this attempt.
            import signal
            os.killpg(child.pid, signal.SIGTERM)
            try:
                child.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(child.pid, signal.SIGKILL)
                child.wait()
        os.close(parent_fd)


if __name__ == '__main__':
    try:
        sys.exit(run())
    except (OSError, ValueError, subprocess.SubprocessError):
        # 外部IO境界の失敗は生の例外に含まれる秘密値を出さず明示する。
        # Surface external IO failures without leaking secrets embedded in exceptions.
        print('{"state":"steam_helper_io_failed"}', flush=True)
        sys.exit(2)
