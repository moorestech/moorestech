#!/usr/bin/env python3
"""Run SteamCMD with a private persistent HOME and an exclusive state lock."""
import fcntl
import os
from pathlib import Path
import pwd
import shutil
import signal
import subprocess
import sys


def private_home():
    value = os.environ.get('MOORESTECH_STEAM_STATE_ROOT')
    if not value:
        raise ValueError('MOORESTECH_STEAM_STATE_ROOT is required')
    root = Path(os.path.abspath(Path(value).expanduser()))
    real_home = Path(pwd.getpwuid(os.getuid()).pw_dir).resolve()
    desktop = real_home / 'Library/Application Support/Steam'
    if root == real_home or root in real_home.parents or root == desktop or root in desktop.parents or desktop in root.parents:
        raise ValueError('state root must not reuse the real HOME or Desktop Steam directory')
    # 大小文字の別表記も、作成や権限変更の前に実体で拒否する。
    # Reject filesystem aliases before creating directories or changing permissions.
    forbidden = (real_home, *real_home.parents, desktop, *desktop.parents)
    if root.exists() and any(p.exists() and root.samefile(p) for p in forbidden):
        raise ValueError('state root aliases the real HOME or Desktop Steam boundary')
    if desktop.exists() and any(p.exists() and desktop.samefile(p) for p in root.parents):
        raise ValueError('state root is inside Desktop Steam')
    # 既存のリンク経由で共有状態へ戻る設定を拒否する。
    # Reject links that could redirect private state into shared storage.
    if any(p.is_symlink() for p in (root, *root.parents)):
        raise ValueError('state root must not contain symlink components')
    root.mkdir(mode=0o700, parents=True, exist_ok=True)
    if root.stat().st_uid != os.getuid():
        raise ValueError('state root must be owned by the current user')
    os.chmod(root, 0o700)
    return root


def prepare_home(root):
    for current, dirs, files in os.walk(root):
        for name in dirs + files:
            path = Path(current) / name
            if path.is_symlink() or path.stat().st_uid != os.getuid() or path.stat().st_nlink > 1 and path.is_file():
                raise ValueError('private state contains a symlink, foreign owner or hard-linked file')
            os.chmod(path, 0o700 if path.is_dir() else (path.stat().st_mode & 0o700) | 0o600)
    home = root / 'home'
    home.mkdir(mode=0o700, exist_ok=True)
    return home


def main():
    os.umask(0o077)
    # ディスクとプロセス境界の失敗は資格情報を出さず報告する。
    # Report disk/process boundary failures without exposing credentials.
    try:
        root = private_home()
        executable = shutil.which(os.environ.get('MOORESTECH_STEAMCMD_EXECUTABLE') or 'steamcmd')
        if not executable or Path(executable).resolve() == Path(__file__).resolve():
            print('STEAM_RUNNER_ERROR:executable_missing', file=sys.stderr)
            return 2
        lock_path = root / 'steamcmd.lock'
        if lock_path.is_symlink() or lock_path.exists() and lock_path.stat().st_nlink != 1:
            raise ValueError('state lock must not be linked')
        with lock_path.open('a') as lock:
            try:
                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                print('STEAM_RUNNER_ERROR:state_in_use; already in use; wait for the active process', file=sys.stderr)
                return 2
            home = prepare_home(root)
            environment = dict(os.environ, HOME=str(home))
            child = subprocess.Popen([executable, *sys.argv[1:]], env=environment, pass_fds=(lock.fileno(),))
            def forward(signum, frame):
                child.send_signal(signum)
            signal.signal(signal.SIGTERM, forward)
            signal.signal(signal.SIGINT, forward)
            result = child.wait()
            return result if result >= 0 else 128 - result
    except (OSError, ValueError):
        print('STEAM_RUNNER_ERROR:setup_failed', file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
