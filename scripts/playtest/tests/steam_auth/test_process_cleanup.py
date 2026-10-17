import fcntl
import os
from pathlib import Path
import runpy
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import Mock, patch

BASE = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(BASE / 'steam_auth'))
import check_login

HELPER = BASE.parents[1] / '.agents/skills/moores-playtest-release/scripts/steam_upload_with_vault.py'
sys.path.insert(0, str(HELPER.parent))


class ProcessCleanupTests(unittest.TestCase):
    def test_keyboard_interrupt_and_io_error_reap_owned_group(self):
        for error in (KeyboardInterrupt(), OSError('private-data')):
            child = Mock(pid=4321)
            child.communicate.side_effect = error
            with patch('check_login.subprocess.Popen', return_value=child), \
                 patch('owned_process.signal_group', return_value=False) as kill:
                if isinstance(error, KeyboardInterrupt):
                    with self.assertRaises(KeyboardInterrupt):
                        check_login.check('user')
                else:
                    self.assertEqual(8, check_login.check('user'))
            kill.assert_any_call(4321, signal.SIGTERM)
            kill.assert_any_call(4321, signal.SIGKILL)
            child.wait.assert_called_once_with(timeout=10)

    def test_sigterm_reaps_descendants_and_releases_lock(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            marker = root / 'ready'
            fake = root / 'steamcmd'
            fake.write_text('#!/usr/bin/env python3\nimport os, signal, time\n'
                            'signal.signal(signal.SIGTERM, signal.SIG_IGN)\n'
                            f'open({str(marker)!r}, "w").write(str(os.getpid()))\n'
                            'while True: time.sleep(0.1)\n')
            fake.chmod(0o700)
            env = dict(os.environ, MOORESTECH_STEAMCMD_EXECUTABLE=str(fake),
                       MOORESTECH_STEAM_STATE_ROOT=str(root / 'state'))
            checker = subprocess.Popen([sys.executable, str(BASE / 'steam_auth/check_login.py'), 'user'],
                                       env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            try:
                deadline = time.monotonic() + 5
                while not marker.exists() and checker.poll() is None and time.monotonic() < deadline:
                    time.sleep(0.02)
                self.assertTrue(marker.exists())
                # 使用中の診断を認証切れと混同しない。
                # Busy state must not be diagnosed as rejected credentials.
                busy = subprocess.run([sys.executable, str(BASE / 'steam_auth/check_login.py'), 'user'],
                                      env=env, capture_output=True, timeout=5)
                self.assertEqual(6, busy.returncode)
                self.assertNotIn(b'--login-only', busy.stderr)
                checker.send_signal(signal.SIGTERM)
                checker.communicate(timeout=8)
                self.assertEqual(143, checker.returncode)
                with (root / 'state/steamcmd.lock').open('a') as lock:
                    fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            finally:
                if checker.poll() is None:
                    checker.send_signal(signal.SIGTERM)
                checker.communicate(timeout=8)

    def helper(self):
        with patch.object(sys, 'argv', [str(HELPER), '/unused', '--login-only']):
            return runpy.run_path(str(HELPER))

    def test_exited_leader_with_open_pty_has_bounded_drain(self):
        function = self.helper()['conversation']
        child = Mock(pid=4321, returncode=2)
        child.poll.return_value = 2
        report = Mock()
        with patch.dict(function.__globals__, report=report), \
             patch('time.monotonic', side_effect=[0, 0, 0, 0, 1, 3]), \
             patch('select.select', return_value=([], [], [])):
            self.assertEqual(5, function(child, 1, 'secret'))
        child.poll.assert_called()
        report.assert_called_once_with('steam_process_failed', steamExit=2)

    def test_first_report_failure_after_spawn_still_reaps_child(self):
        function = self.helper()['run']
        child = Mock(pid=4321)
        report = Mock(side_effect=[None, OSError('status-write-failed')])
        with patch.dict(function.__globals__, ROOT=Mock(), configure=Mock(), report=report,
                        credential=Mock(return_value=('user', 'secret')), required=Mock(return_value='user')), \
             patch('subprocess.Popen', return_value=child), \
             patch('owned_process.signal_group', return_value=False) as kill:
            with self.assertRaises(OSError):
                function()
        kill.assert_any_call(4321, signal.SIGTERM)
        child.wait.assert_called_once_with(timeout=10)
