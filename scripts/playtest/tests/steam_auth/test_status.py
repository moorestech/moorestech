import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[2]
HELPER = BASE.parents[1] / '.agents/skills/moores-playtest-release/scripts/steam_upload_with_vault.py'
sys.path.insert(0, str(HELPER.parent))
from steam_vault_environment import NAMES


class StatusTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name).resolve()
        self.run = self.root / 'run'
        self.run.mkdir()
        self.upload = self.run / 'steam-upload-status.json'
        self.upload.write_text('{"state":"uploaded"}')
        self.fake = self.root / 'steamcmd'
        self.fake.write_text('#!/bin/sh\necho "Logged in OK"\n')
        self.fake.chmod(0o700)
        self.vault = self.root / 'vault.py'
        self.vault.write_text("DEFAULT_VAULT='test'\ndef get_item(*a, **k): return {}\n"
                              "def item_fields(item): return 'user', 'secret'\n")
        self.config = self.root / 'env.sh'
        self.config.write_text('export MOORESTECH_STEAM_USER=user\n')
        self.env = dict(os.environ, MOORESTECH_PLAYTEST_ENV_FILE=str(self.config),
                        MOORESTECH_STEAM_STATE_ROOT=str(self.root / 'state'),
                        MOORESTECH_STEAMCMD_EXECUTABLE=str(self.fake),
                        MOORESTECH_STEAM_OP_HELPER=str(self.vault), MOORESTECH_STEAM_OP_ITEM='test')

    def invoke(self, mode):
        return subprocess.run([sys.executable, str(HELPER), str(self.run), *mode],
                              env=self.env, capture_output=True, text=True, timeout=10)

    def status(self):
        return json.loads((self.run / 'steam-auth-status.json').read_text())

    def test_auth_modes_preserve_upload_evidence(self):
        for mode in ('--check-login', '--login-only'):
            with self.subTest(mode=mode):
                result = self.invoke([mode])
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertEqual('uploaded', json.loads(self.upload.read_text())['state'])
                self.assertEqual('cached_login_ok' if mode == '--check-login' else 'login_ok', self.status()['state'])

    def test_empty_required_settings_do_not_resurrect_parent_values(self):
        for name in (NAMES[0], NAMES[1], NAMES[3], NAMES[4]):
            with self.subTest(name=name):
                self.config.write_text(f'export {name}=""\n')
                self.env['MOORESTECH_STEAM_USER'] = 'user'
                result = self.invoke(['--login-only'])
                self.assertEqual(2, result.returncode)
                self.assertEqual('env_missing', self.status()['state'])
                self.assertEqual(name, self.status()['name'])

    def test_empty_optional_executable_uses_path(self):
        self.config.write_text('export MOORESTECH_STEAM_USER=user\nexport MOORESTECH_STEAMCMD_EXECUTABLE=""\n')
        self.env['MOORESTECH_STEAMCMD_EXECUTABLE'] = '/old/missing'
        self.env['PATH'] = str(self.root) + os.pathsep + os.environ['PATH']
        result = self.invoke(['--check-login'])
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_io_failure_replaces_stale_status_and_creates_missing_status(self):
        self.config.unlink()
        for mode, filename in (([], self.upload), (['--check-login'], self.run / 'steam-auth-status.json')):
            result = self.invoke(mode)
            self.assertEqual(2, result.returncode)
            self.assertEqual('steam_helper_io_failed', json.loads(filename.read_text())['state'])

    def test_persistence_failure_is_explicit_without_raw_error(self):
        self.config.unlink()
        status = self.run / 'steam-auth-status.json'
        status.mkdir()
        result = self.invoke(['--check-login'])
        self.assertEqual({'state': 'steam_helper_io_failed', 'statusSaved': False}, json.loads(result.stdout))
        self.assertNotIn(str(self.root), result.stdout + result.stderr)

    def test_unknown_failure_is_not_reauthentication(self):
        self.fake.write_text('#!/bin/sh\necho private-secret\nexit 2\n')
        result = self.invoke(['--check-login'])
        self.assertEqual(8, result.returncode)
        self.assertEqual('steam_check_failed', self.status()['state'])
        self.assertNotIn('private-secret', result.stdout + result.stderr)
        self.assertNotIn('--login-only', result.stderr)

    def test_missing_executable_and_bad_setup_have_distinct_status(self):
        self.env['MOORESTECH_STEAMCMD_EXECUTABLE'] = '/missing-steamcmd'
        self.assertEqual(7, self.invoke(['--check-login']).returncode)
        self.assertEqual('steam_executable_missing', self.status()['state'])
        self.env['MOORESTECH_STEAM_STATE_ROOT'] = str(Path.home())
        self.assertEqual(2, self.invoke(['--check-login']).returncode)
        self.assertEqual('steam_setup_failed', self.status()['state'])
