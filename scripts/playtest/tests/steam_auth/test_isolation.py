import importlib.util
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

BASE = Path(__file__).resolve().parents[2]
RUNNER = BASE / 'steam_auth/run.py'
HELPER = BASE.parents[1] / '.agents/skills/moores-playtest-release/scripts/steam_upload_with_vault.py'


class IsolationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.fake = self.root / 'steamcmd'
        self.fake.write_text('''#!/usr/bin/env python3
import os, pathlib, sys
home = pathlib.Path(os.environ['HOME'])
(home / 'called').write_text(' '.join(sys.argv[1:]))
if os.environ.get('WAIT'):
    print('ready', flush=True)
    sys.stdin.readline()
if (home / 'cached').exists():
    print('Logged in OK', flush=True)
    sys.exit(0)
print('Cached credentials not found', flush=True)
sys.exit(5)
''')
        self.fake.chmod(0o700)
        self.env = dict(os.environ, MOORESTECH_STEAM_STATE_ROOT=str(self.root / 'state'),
                        MOORESTECH_STEAMCMD_EXECUTABLE=str(self.fake))

    def run_runner(self):
        return subprocess.run([str(RUNNER), '+quit'], env=self.env, capture_output=True, text=True)

    def test_private_home_and_parent_unchanged(self):
        desktop = self.root / 'desktop'
        desktop.mkdir()
        sentinel = desktop / 'config.vdf'
        sentinel.write_text('unchanged')
        self.env['HOME'] = str(desktop)
        result = self.run_runner()
        self.assertEqual(5, result.returncode)
        self.assertEqual('unchanged', sentinel.read_text())
        self.assertFalse((desktop / 'called').exists())
        self.assertTrue((self.root / 'state/home/called').is_file())
        self.assertEqual(0o700, (self.root / 'state').stat().st_mode & 0o777)

    def test_links_and_real_home_rejected(self):
        self.env['MOORESTECH_STEAM_STATE_ROOT'] = str(Path.home())
        self.assertEqual(2, self.run_runner().returncode)
        link = self.root / 'link'
        link.symlink_to(self.root, target_is_directory=True)
        self.env['MOORESTECH_STEAM_STATE_ROOT'] = str(link / 'state')
        self.assertEqual(2, self.run_runner().returncode)
        self.env['MOORESTECH_STEAM_STATE_ROOT'] = str(self.root / 'state')
        home = self.root / 'state/home'
        home.mkdir(parents=True)
        (home / 'Library').symlink_to(self.root, target_is_directory=True)
        self.assertEqual(2, self.run_runner().returncode)

    def test_exclusive_until_child_finishes(self):
        first = subprocess.Popen([str(RUNNER), '+quit'], env=dict(self.env, WAIT='1'),
                                 stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            self.assertEqual('ready\n', first.stdout.readline())
            second = self.run_runner()
            self.assertEqual(2, second.returncode)
            self.assertIn('already in use', second.stderr)
        finally:
            first.communicate('\n', timeout=5)
        self.assertEqual(5, self.run_runner().returncode)

    def test_cached_only_needs_no_vault_and_missing_cache_fails(self):
        env_file = self.root / 'env.sh'
        env_file.write_text('export MOORESTECH_STEAM_USER=testuser\n')
        env = dict(self.env, MOORESTECH_PLAYTEST_ENV_FILE=str(env_file))
        env.pop('MOORESTECH_STEAM_OP_HELPER', None)
        env.pop('MOORESTECH_STEAM_OP_ITEM', None)
        command = [sys.executable, str(HELPER), str(self.root / 'status'), '--check-login']
        first = subprocess.run(command, env=env, capture_output=True, text=True)
        self.assertEqual(5, first.returncode, first.stderr)
        (self.root / 'state/home/cached').touch()
        second = subprocess.run(command, env=env, capture_output=True, text=True)
        self.assertEqual(0, second.returncode, second.stderr)
        self.assertIn('cached_login_ok', second.stdout)
        self.assertNotIn('reading_credential', second.stdout)

    def test_owner_executable_survives_permissions_hardening(self):
        self.run_runner()
        binary = self.root / 'state/home/binary'
        binary.touch(mode=0o755)
        self.run_runner()
        self.assertEqual(0o700, binary.stat().st_mode & 0o777)


if __name__ == '__main__':
    unittest.main()
