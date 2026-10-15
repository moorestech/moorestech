#!/usr/bin/env python3
"""1Password の Steam 資格で steamcmd にログインし <run-dir>/steam/app_build_playtest.vdf を上げる。

Usage: steam_upload_with_vault.py <run-dir>
env.sh に MOORESTECH_STEAM_USER / MOORESTECH_STEAM_OP_HELPER（hermes_onepassword.py のパス）/
MOORESTECH_STEAM_OP_ITEM（1Password 項目ID）が要る。秘密値は PTY へ直接書き、argv・ログ・ファイルへ出さない。
進捗は JSON 1行ずつ stdout と <run-dir>/steam-upload-status.json へ。Steam Guard が出たら
スマホ承認（約1分で steamcmd が exit 5）か <run-dir>/steam-guard-code.txt へのコード投入で進む。
"""
import importlib.util, json, os, pty, re, select, subprocess, sys, termios, time
from pathlib import Path

if len(sys.argv) != 2 or sys.argv[1] in ("-h", "--help"):
    sys.exit(__doc__)
ROOT = Path(sys.argv[1]).expanduser()
STATUS = ROOT / "steam-upload-status.json"
GUARD_CODE = ROOT / "steam-guard-code.txt"


def report(state, **extra):
    result = {"state": state, "helperPid": os.getpid(), **extra}
    STATUS.write_text(json.dumps(result, ensure_ascii=False))
    print(json.dumps(result, ensure_ascii=False), flush=True)


def env(name):
    value = subprocess.check_output(
        ["bash", "-c", f'source "$HOME/hermes-agent/data/services/playtest/env.sh"; printf %s "${name}"'], text=True)
    if not value:
        report("env_missing", name=name)
        sys.exit(2)
    return value


def load_credential():
    spec = importlib.util.spec_from_file_location("hermes_onepassword", env("MOORESTECH_STEAM_OP_HELPER"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    item = module.get_item(env("MOORESTECH_STEAM_OP_ITEM"), module.DEFAULT_VAULT, reveal=True)
    return module.item_fields(item)


def run():
    GUARD_CODE.unlink(missing_ok=True)
    report("reading_credential")
    username, password = load_credential()
    if not username or not password or any(c in username + password for c in "\r\n\0"):
        report("credential_unusable")
        return 2
    if username != env("MOORESTECH_STEAM_USER"):
        report("account_mismatch")
        return 2
    parent_fd, child_fd = pty.openpty()
    attributes = termios.tcgetattr(child_fd)
    attributes[3] &= ~termios.ECHO
    termios.tcsetattr(child_fd, termios.TCSANOW, attributes)
    child = subprocess.Popen(
        ["steamcmd", "+login", username, "+run_app_build", str(ROOT / "steam/app_build_playtest.vdf"), "+quit"],
        stdin=child_fd, stdout=child_fd, stderr=child_fd, start_new_session=True)
    os.close(child_fd)
    report("steam_started", steamPid=child.pid)
    buffer, sent_password, authenticated, guard_reported = "", False, False, False
    deadline = time.monotonic() + 3600
    try:
        while time.monotonic() < deadline and child.poll() is None:
            ready, _, _ = select.select([parent_fd], [], [], 1)
            if ready:
                try:
                    chunk = os.read(parent_fd, 65536)
                except OSError:
                    break
                if not chunk:
                    break
                buffer = (buffer + chunk.decode("utf-8", errors="replace"))[-32000:]
                if not sent_password and re.search(r"password:\s*$", buffer, re.I):
                    os.write(parent_fd, password.encode() + b"\n")
                    sent_password, buffer = True, ""
                    report("password_submitted", steamPid=child.pid)
                if re.search(r"Invalid Password|Account Logon Denied", buffer, re.I):
                    report("steam_rejected_credential", steamPid=child.pid)
                    return 3
                if not guard_reported and re.search(r"Steam Guard|authenticator|two.factor|check your email", buffer, re.I):
                    guard_reported = True
                    report("steam_guard_required", steamPid=child.pid, prompt=buffer[-300:])
                if not authenticated and re.search(r"Waiting for user info\.\.\.OK|Logged in OK|Logging in user.*OK", buffer, re.I):
                    authenticated = True
                    report("authenticated_uploading", steamPid=child.pid)
            if guard_reported and GUARD_CODE.exists():
                os.write(parent_fd, GUARD_CODE.read_text().strip().encode() + b"\n")
                GUARD_CODE.unlink()
                report("guard_code_submitted", steamPid=child.pid)
        try:
            child.wait(timeout=10)
        except subprocess.TimeoutExpired:
            report("upload_deadline_exceeded" if authenticated else "authentication_deadline_exceeded")
            return 4
        ok = authenticated and child.returncode == 0
        report("uploaded" if ok else "steam_upload_failed", steamExit=child.returncode)
        return 0 if ok else 5
    finally:
        if child.poll() is None:
            child.kill()
        os.close(parent_fd)


if __name__ == "__main__":
    sys.exit(run())
