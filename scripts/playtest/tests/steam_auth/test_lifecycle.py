import importlib.util
from pathlib import Path
import runpy
import subprocess
import sys
import unittest
from unittest.mock import Mock, patch

BASE = Path(__file__).resolve().parents[2]
HELPER = BASE.parents[1] / '.agents/skills/moores-playtest-release/scripts/steam_upload_with_vault.py'


class LifecycleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        sys.path.insert(0, str(HELPER.parent))
        with patch.object(sys, 'argv', [str(HELPER), '/unused', '--login-only']):
            cls.helper = runpy.run_path(str(HELPER))
        spec = importlib.util.spec_from_file_location('check_login', BASE / 'steam_auth/check_login.py')
        cls.checker = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.checker)

    def conversation(self, chunks, child):
        function = self.helper['conversation']
        report = Mock()
        with patch.dict(function.__globals__, report=report), \
             patch('select.select', return_value=([1], [], [])), \
             patch('os.read', side_effect=chunks):
            result = function(child, 1, 'secret-never-print')
        return result, report

    def test_pty_disconnect_is_not_authentication_timeout(self):
        child = Mock(pid=4321)
        child.wait.side_effect = subprocess.TimeoutExpired('fake', 10)
        result, report = self.conversation([b''], child)
        self.assertEqual(4, result)
        report.assert_called_once_with('steam_pty_disconnected')

    def test_final_login_output_is_drained_before_success(self):
        child = Mock(pid=4321, returncode=0)
        result, report = self.conversation([b'Logged in OK\n', b''], child)
        self.assertEqual(0, result)
        report.assert_any_call('login_ok', steamExit=0)

    def test_guard_exit_five_has_distinct_status_without_raw_prompt(self):
        child = Mock(pid=4321, returncode=5)
        result, report = self.conversation([b'Steam Guard private-data\n', b''], child)
        self.assertEqual(5, result)
        report.assert_any_call('steam_guard_expired', steamExit=5)
        self.assertNotIn('private-data', str(report.call_args_list))

    def test_cached_timeout_kills_owned_process_group(self):
        child = Mock(pid=4321)
        child.communicate.side_effect = [subprocess.TimeoutExpired('fake', 120), ('', '')]
        with patch.object(self.checker.subprocess, 'Popen', return_value=child) as spawn, \
             patch.object(self.checker.os, 'killpg') as kill:
            self.assertEqual(4, self.checker.check('/fake', 'user'))
        self.assertTrue(spawn.call_args.kwargs['start_new_session'])
        kill.assert_called_once_with(4321, self.checker.signal.SIGKILL)
        self.assertEqual(2, child.communicate.call_count)


if __name__ == '__main__':
    unittest.main()
