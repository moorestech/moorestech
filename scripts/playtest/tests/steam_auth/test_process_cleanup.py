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
from owned_process import reap_group

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

    def test_spawn_interruption_reaps_runner_and_releases_lock(self):
        for caller in ('checker', 'helper'):
            for signum in (signal.SIGTERM, signal.SIGINT):
                with self.subTest(caller=caller, signum=signum), tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary).resolve()
                    marker = root / 'ready'
                    fake = root / 'steamcmd'
                    fake.write_text('#!/usr/bin/env python3\nimport signal, time\n'
                                    f'open({str(marker)!r}, "w").write("ready")\n'
                                    'while True: time.sleep(0.1)\n')
                    fake.chmod(0o700)
                    env = dict(MOORESTECH_STEAMCMD_EXECUTABLE=str(fake),
                               MOORESTECH_STEAM_STATE_ROOT=str(root / 'state'))
                    children = []
                    real_popen = subprocess.Popen
                    previous_term = signal.getsignal(signal.SIGTERM)
                    previous_int = signal.getsignal(signal.SIGINT)

                    def spawn_and_interrupt(*args, **kwargs):
                        child = real_popen(*args, **kwargs)
                        children.append(child)
                        deadline = time.monotonic() + 5
                        while not marker.exists() and child.poll() is None and time.monotonic() < deadline:
                            time.sleep(0.02)
                        self.assertTrue(marker.exists())
                        # Popenが戻る直前に実シグナルを送り、未登録区間を検証する。
                        # Inject a real signal before Popen returns to cover the unregistered window.
                        with (root / 'state/steamcmd.lock').open('a') as lock:
                            with self.assertRaises(BlockingIOError):
                                fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                        os.kill(os.getpid(), signum)
                        return child

                    function = self.helper()['run']
                    error = SystemExit if signum == signal.SIGTERM else KeyboardInterrupt
                    try:
                        with patch.dict(os.environ, env), \
                             patch('subprocess.Popen', side_effect=spawn_and_interrupt), \
                             patch.dict(function.__globals__, ROOT=root, configure=Mock(), report=Mock(),
                                        credential=Mock(return_value=('user', 'secret')),
                                        required=Mock(return_value='user')):
                            with self.assertRaises(error) as interrupted:
                                if caller == 'checker':
                                    check_login.check('user')
                                else:
                                    function()
                            if signum == signal.SIGTERM:
                                self.assertEqual(143, interrupted.exception.code)
                        self.assertEqual(1, len(children))
                        self.assertIsNotNone(children[0].poll())
                        with (root / 'state/steamcmd.lock').open('a') as lock:
                            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
                        self.assertEqual(previous_term, signal.getsignal(signal.SIGTERM))
                        self.assertEqual(previous_int, signal.getsignal(signal.SIGINT))
                    finally:
                        # 失敗した検証でも、記録済みの所有PIDだけを回収する。
                        # Reap only recorded owned process groups even when an assertion fails.
                        for child in children:
                            reap_group(child)
                            if child.stdout is not None:
                                child.stdout.close()
