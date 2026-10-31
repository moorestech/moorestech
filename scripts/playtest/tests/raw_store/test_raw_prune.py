#!/usr/bin/env python3
"""生データ置き場の期限削除（prune.py）と、ダッシュボードが生データ側を読むことを検証する。
Checks the raw store's expiry prune (prune.py) and that the dashboard reads from the raw side."""
import json
import os
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[2]
PRUNE = SCRIPTS / "raw_store" / "prune.py"
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(SCRIPTS / "dashboard"))
sys.path.insert(0, str(SCRIPTS / "raw_store"))
import collect_reports  # noqa: E402
import media  # noqa: E402


def write_box(root: Path, rel: str, expires_at: str, files: dict) -> Path:
    box = root / rel
    for name, body in files.items():
        (box / name).parent.mkdir(parents=True, exist_ok=True)
        (box / name).write_bytes(body)
    ref = {"schemaVersion": 1, "rawRelative": rel, "expiresAt": expires_at, "ingestedAt": "2026-01-01T00:00:00Z",
           "files": [{"path": name, "bytes": len(body)} for name, body in files.items()]}
    (box / "raw.json").write_text(json.dumps(ref), encoding="utf-8")
    return box


class PruneTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name) / "raw"
        self.root.mkdir()

    def tearDown(self):
        self.temp.cleanup()

    def prune(self, *args):
        env = dict(os.environ, MOORESTECH_PLAYTEST_RAW=str(self.root))
        return subprocess.run([sys.executable, str(PRUNE), *args], env=env, capture_output=True, text=True)

    def test_idle_run_prints_nothing(self):
        write_box(self.root, "reports/765/fresh", "2999-01-01T00:00:00Z", {"video.mp4": b"v"})
        done = self.prune()
        self.assertEqual((done.returncode, done.stdout), (0, ""))
        self.assertTrue((self.root / "reports/765/fresh/video.mp4").is_file())

    def test_dry_run_lists_without_deleting(self):
        write_box(self.root, "reports/765/old", "2026-01-01T00:00:00Z", {"video.mp4": b"v"})
        done = self.prune("-n")
        self.assertEqual(done.returncode, 0)
        self.assertIn("消す予定: reports/765/old（期限 2026-01-01T00:00:00Z", done.stdout)
        self.assertTrue((self.root / "reports/765/old/video.mp4").is_file())

    def test_deletes_expired_and_reports_unjudgeable(self):
        write_box(self.root, "reports/765/old", "2026-01-01T00:00:00Z", {"snapshots/tick_1.json": b"{}"})
        write_box(self.root, "reports/765/fresh", "2999-01-01T00:00:00Z", {"video.mp4": b"v"})
        broken = self.root / "reports/766/broken"
        broken.mkdir(parents=True)
        (broken / "raw.json").write_text("{not json", encoding="utf-8")
        stale = self.root / "reports/767/x.partial"
        stale.mkdir(parents=True)
        os.utime(stale, (time.time() - 2 * 86400,) * 2)
        done = self.prune()
        self.assertEqual(done.returncode, 1, done.stdout)
        self.assertIn("消した: reports/765/old", done.stdout)
        self.assertIn("消せなかった: reports/766/broken（期限を判定できない", done.stdout)
        self.assertIn("消した: reports/767/x.partial（取り込み途中で止まった下書きの残骸", done.stdout)
        self.assertFalse((self.root / "reports/765/old").exists())
        self.assertFalse((self.root / "reports/767").exists())
        self.assertTrue((self.root / "reports/765/fresh/video.mp4").is_file())
        self.assertTrue(broken.is_dir())

    def test_missing_root_is_reported_on_stdout(self):
        self.root.rmdir()
        done = self.prune()
        self.assertEqual(done.returncode, 1)
        self.assertIn("生データ置き場が無い", done.stdout)


class DashboardRawTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        base = Path(self.temp.name)
        self.reports, self.raw = base / "logs/harness/playtest/reports", base / "raw"
        self.box = self.reports / "765" / "r1"
        self.box.mkdir(parents=True)
        (self.box / "ingest.json").write_text(json.dumps(
            {"kind": "report", "steamId": "765", "id": "r1", "readyAt": "2026-10-20T01:00:00Z", "ingestedAt": "2026-10-20T01:01:00Z"}))
        (self.box / "screenshot.png").write_bytes(b"png")

    def tearDown(self):
        self.temp.cleanup()

    def row(self):
        return collect_reports.load_reports(self.reports, self.raw, {})[0]

    def test_video_is_served_from_raw_side(self):
        ref = write_box(self.raw, "reports/765/r1", "2999-01-01T00:00:00Z", {"video.mp4": b"mp4"}) / "raw.json"
        (self.box / "raw.json").write_text(ref.read_text())
        row = self.row()
        self.assertEqual(row["raw"]["state"], "ok")
        self.assertIn("video.mp4", row["media"])
        self.assertEqual(media.resolve_media(self.reports, self.raw, "765", "r1", "video.mp4"), self.raw / "reports/765/r1/video.mp4")

    def test_expired_raw_is_visible_in_the_row(self):
        ref = write_box(self.raw, "reports/765/r1", "2026-01-01T00:00:00Z", {"video.mp4": b"mp4"}) / "raw.json"
        (self.box / "raw.json").write_text(ref.read_text())
        for path in sorted(self.raw.rglob("*"), reverse=True):
            path.unlink() if path.is_file() else path.rmdir()
        row = self.row()
        self.assertEqual(row["raw"]["state"], "expired")
        self.assertIn("期限 2026-01-01T00:00:00Z を過ぎて削除済み", row["raw"]["reason"])
        self.assertEqual(row["media"], ["screenshot.png"])
        self.assertIsNone(media.resolve_media(self.reports, self.raw, "765", "r1", "video.mp4"))


if __name__ == "__main__":
    unittest.main()
