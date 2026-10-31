#!/usr/bin/env python3
"""logs repo に入っている生データの移行（migrate.sh / migrate.py）を、偽の logs repo で検証する（本物には触れない）。
Checks the migration of raw data already in the logs repo (migrate.sh / migrate.py) against a fake logs repo."""
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

MIGRATE = Path(__file__).resolve().parents[2] / "raw_store" / "migrate.sh"
REL = "harness/playtest/reports/765/r1"


def git(repo: Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(repo), *args], check=True, capture_output=True, text=True).stdout


class MigrateTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        base = Path(self.temp.name)
        self.logs, self.raw = base / "logs", base / "raw"
        self.raw.mkdir()
        self.box = self.logs / REL
        files = {"manifest.json": b"{}", "READY": b"{}", "screenshot.png": b"png", "logs/unity.log": b"log",
                 "snapshots/tick_1.json": b'{"tick":1}', "world/w.json": b"w", "video.mp4": b"mp4-ignored"}
        for name, body in files.items():
            (self.box / name).parent.mkdir(parents=True, exist_ok=True)
            (self.box / name).write_bytes(body)
        (self.box / "ingest.json").write_text(json.dumps({"ingestedAt": "2026-10-01T00:00:00Z"}))
        (self.logs / "harness/playtest/reports/2026-10-13-plan/notes").mkdir(parents=True)
        (self.logs / "harness/playtest/reports/2026-10-13-plan/notes/big.bin").write_bytes(b"agent output")
        (self.logs / ".gitignore").write_text("harness/playtest/reports/*/*/video.mp4\n")
        git(self.logs.parent, "init", "-q", str(self.logs))
        git(self.logs, "-c", "user.email=t@t", "-c", "user.name=t", "add", "-A")
        git(self.logs, "-c", "user.email=t@t", "-c", "user.name=t", "commit", "-qm", "init")

    def tearDown(self):
        self.temp.cleanup()

    def migrate(self, *args):
        env = dict(os.environ, MOORESTECH_LOGS=str(self.logs), MOORESTECH_PLAYTEST_RAW=str(self.raw),
                   GIT_AUTHOR_NAME="t", GIT_AUTHOR_EMAIL="t@t", GIT_COMMITTER_NAME="t", GIT_COMMITTER_EMAIL="t@t")
        return subprocess.run(["bash", str(MIGRATE), *args], env=env, capture_output=True, text=True)

    def test_dry_run_changes_nothing(self):
        head = git(self.logs, "rev-parse", "HEAD")
        done = self.migrate("-n")
        self.assertEqual(done.returncode, 0, done.stderr)
        self.assertIn("移す予定: reports/765/r1（3件", done.stdout)
        self.assertIn("箱でない（ingest.json が無い）ので触らない: harness/playtest/reports/2026-10-13-plan", done.stdout)
        self.assertEqual(git(self.logs, "rev-parse", "HEAD"), head)
        self.assertEqual(list(self.raw.iterdir()), [])
        self.assertTrue((self.box / "video.mp4").is_file())

    def test_run_moves_verified_copies_and_git_rms(self):
        done = self.migrate()
        self.assertEqual(done.returncode, 0, done.stdout + done.stderr)
        raw_box = self.raw / "reports/765/r1"
        self.assertEqual((raw_box / "snapshots/tick_1.json").read_bytes(), b'{"tick":1}')
        self.assertEqual((raw_box / "video.mp4").read_bytes(), b"mp4-ignored")
        tracked = git(self.logs, "ls-files", REL).split()
        self.assertEqual(sorted(tracked), sorted(f"{REL}/{n}" for n in
                                                ("manifest.json", "READY", "screenshot.png", "logs/unity.log", "ingest.json", "raw.json")))
        self.assertFalse((self.box / "video.mp4").exists())
        self.assertFalse((self.box / "snapshots").exists())
        ref = json.loads((self.box / "raw.json").read_text())
        self.assertEqual(ref["expiresAt"], "2026-11-30T00:00:00Z")
        self.assertEqual(ref, json.loads((raw_box / "raw.json").read_text()))
        self.assertIn("生データ 1 箱", git(self.logs, "log", "-1", "--format=%s"))
        self.assertTrue((self.logs / "harness/playtest/reports/2026-10-13-plan/notes/big.bin").is_file())
        # 再実行しても何も移さない（冪等）
        # A re-run moves nothing (idempotent)
        again = self.migrate()
        self.assertEqual(again.returncode, 0)
        self.assertNotIn("- 移す", again.stdout)

    def test_truncated_copy_from_an_interrupted_run_is_redone(self):
        partial = self.raw / "reports/765/r1/snapshots/tick_1.json"
        partial.parent.mkdir(parents=True)
        partial.write_bytes(b"{")
        done = self.migrate()
        self.assertEqual(done.returncode, 0, done.stdout + done.stderr)
        self.assertEqual(partial.read_bytes(), b'{"tick":1}')

    def test_box_without_ingested_at_is_reported_and_left_in_place(self):
        (self.box / "ingest.json").write_text("{}")
        done = self.migrate()
        self.assertEqual(done.returncode, 1)
        self.assertIn("移せなかった: KeyError", done.stdout)
        self.assertIn(f"{REL}/snapshots/tick_1.json", git(self.logs, "ls-files", REL))


if __name__ == "__main__":
    unittest.main()
