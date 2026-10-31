#!/usr/bin/env python3
"""box_layout.py の振り分け規則（wants の許可リスト・finalize の 10MB 上限と remote.json）を検証する。
Checks box_layout.py's split rules: the wants allowlist and finalize's 10MB cap and remote.json.
"""
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

R2_STORE = Path(__file__).resolve().parents[2] / "r2_store"
sys.path.insert(0, str(R2_STORE))
import box_layout  # noqa: E402

LIMIT = box_layout.KEEP_MAX_BYTES


def make_file(path: Path, size: int) -> None:
    # 大きなファイルは疎ファイルで作り、テストを速く保つ
    # Large files are sparse so the test stays fast
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("wb") as handle:
        handle.truncate(size)


class WantsTest(unittest.TestCase):
    def test_allowlisted_paths_are_wanted(self):
        for path in ("manifest.json", "READY", "frames.tsv", "screenshot.png", "record.json", "logs/unity.log",
                     "logs/nested/player.log", "export-markers/UPLOAD_FAILED", "repo/changes.diff", "repo/status.txt",
                     "repo/commit-transfer.json"):
            self.assertTrue(box_layout.wants(path), path)

    def test_raw_data_is_not_wanted(self):
        for path in ("video.mp4", "world/save.json", "snapshots/0001.json", "frames/0001.png", "repo/commits.bundle",
                     "repo/untracked/a.txt", "__attachments/000000/chunk", "logsx/a.log", "logs", "note.txt",
                     "nested/manifest.json", "repo.diff"):
            self.assertFalse(box_layout.wants(path), path)

    def test_cli_exit_codes(self):
        script = str(R2_STORE / "box_layout.py")
        self.assertEqual(subprocess.run([sys.executable, "-I", script, "wants", "logs/a.log"]).returncode, 0)
        self.assertEqual(subprocess.run([sys.executable, "-I", script, "wants", "video.mp4"]).returncode, 1)
        usage = subprocess.run([sys.executable, "-I", script, "finalize", "box", "bogus", "s", "i", "list"],
                               capture_output=True, text=True)
        self.assertEqual(usage.returncode, 2)
        self.assertIn("usage", usage.stderr)


class FinalizeTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.box = Path(self.temp.name) / "box"

    def test_oversized_files_leave_and_remote_json_lists_r2_only_files(self):
        make_file(self.box / "manifest.json", 10)
        make_file(self.box / "logs/at-limit.log", LIMIT)
        make_file(self.box / "logs/over.log", LIMIT + 1)
        make_file(self.box / "screenshot.png", LIMIT + 1)
        make_file(self.box / "export-markers/huge/marker", LIMIT + 1)
        make_file(self.box / "record.json", LIMIT + 1)
        listed = ["manifest.json", "logs/at-limit.log", "logs/over.log", "screenshot.png", "export-markers/huge/marker",
                  "record.json", "video.mp4", "world/save.json"]
        count = box_layout.finalize(self.box, "progress", "7656001", "20261030_pg1", listed)

        # 上限ちょうどと索引（record.json）は残り、上限超えは消え、空になったディレクトリも消える
        # Files at the cap and the index (record.json) stay, oversized ones go, and emptied directories go too
        self.assertTrue((self.box / "logs/at-limit.log").is_file())
        self.assertTrue((self.box / "record.json").is_file())
        for gone in ("logs/over.log", "screenshot.png", "export-markers"):
            self.assertFalse((self.box / gone).exists(), gone)
        ref = json.loads((self.box / "remote.json").read_text(encoding="utf-8"))
        expected = ["export-markers/huge/marker", "logs/over.log", "screenshot.png", "video.mp4", "world/save.json"]
        self.assertEqual(ref["files"], expected)
        self.assertEqual(count, len(expected))
        self.assertEqual((ref["store"], ref["bucket"], ref["prefix"]), ("r2", "moorestech-playtest", "progress/7656001/20261030_pg1/"))
        self.assertEqual((ref["kind"], ref["steamId"], ref["id"]), ("progress", "7656001", "20261030_pg1"))
        self.assertEqual(box_layout.read_ref(self.box), (ref, ""))

    def test_cli_finalize_prints_remote_count(self):
        make_file(self.box / "manifest.json", 10)
        listing = Path(self.temp.name) / "files.txt"
        listing.write_text("manifest.json\nvideo.mp4\n\n", encoding="utf-8")
        result = subprocess.run([sys.executable, "-I", str(R2_STORE / "box_layout.py"), "finalize", str(self.box), "report",
                                 "7656001", "r1", str(listing)], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout.strip(), "1")
        self.assertEqual(json.loads((self.box / "remote.json").read_text())["prefix"], "reports/7656001/r1/")

    def test_read_ref_reports_missing_and_broken(self):
        self.box.mkdir()
        ref, reason = box_layout.read_ref(self.box)
        self.assertIsNone(ref)
        self.assertIn("remote.json が無い", reason)
        (self.box / "remote.json").write_text('{"files": "video.mp4"}', encoding="utf-8")
        ref, reason = box_layout.read_ref(self.box)
        self.assertIsNone(ref)
        self.assertIn("files が文字列の配列でない", reason)


if __name__ == "__main__":
    unittest.main()
