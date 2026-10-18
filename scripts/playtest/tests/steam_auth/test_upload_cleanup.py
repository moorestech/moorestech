import fcntl
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest

BASE = Path(__file__).resolve().parents[2] / 'steam_auth'


class UploadCleanupTests(unittest.TestCase):
    def test_upload_exit_and_interrupt_release_descendant_lock(self):
        for leader_exits in (False, True):
            with self.subTest(leader_exits=leader_exits), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary).resolve()
                marker = root / 'ready'
                fake = root / 'steamcmd'
                fake.write_text('#!/usr/bin/env python3\nimport os, signal, time\n'
                                'signal.signal(signal.SIGTERM, signal.SIG_IGN)\n'
                                f'if {leader_exits!r} and os.fork(): os._exit(0)\n'
                                f'open({str(marker)!r}, "w").write(str(os.getpid()))\n'
                                'while True: time.sleep(0.1)\n')
                fake.chmod(0o700)
                env = dict(os.environ, MOORESTECH_STEAMCMD_EXECUTABLE=str(fake),
                           MOORESTECH_STEAM_STATE_ROOT=str(root / 'state'))
                upload = subprocess.Popen([sys.executable, str(BASE / 'owned_process.py'),
                                           str(BASE / 'run.py'), '+quit'], env=env,
                                          stdout=subprocess.PIPE, stderr=subprocess.PIPE)
                try:
                    deadline = time.monotonic() + 5
                    while not marker.exists() and upload.poll() is None and time.monotonic() < deadline:
                        time.sleep(0.02)
                    self.assertTrue(marker.exists())
                    if not leader_exits:
                        upload.send_signal(signal.SIGTERM)
                    upload.communicate(timeout=8)
                    self.assertEqual(0 if leader_exits else 143, upload.returncode)
                    with (root / 'state/steamcmd.lock').open('a') as lock:
                        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                finally:
                    # 検証失敗時にも記録した所有プロセスだけを止める。
                    # Stop only recorded owned processes when verification fails.
                    if upload.poll() is None:
                        upload.send_signal(signal.SIGTERM)
                    upload.communicate(timeout=8)
                    if marker.exists():
                        pid = int(marker.read_text())
                        try:
                            os.kill(pid, signal.SIGKILL)
                        except ProcessLookupError:
                            pass  # silent-ok: 所有プロセスは回収済み / ロック再取得で検証
