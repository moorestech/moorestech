#!/usr/bin/env python3
"""操作 A の CLI を最小 mod に流し、itemGuid を持たないブロックも研究解放と nodeGraph から並ぶことを確かめる。
Run the operation A CLI on the minimal mod and check blocks without itemGuid are ordered by research unlocks and the nodeGraph."""
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sort_fixture import EXPECTED_BLOCKS, EXPECTED_ITEMS, build_mod, write_mod  # noqa: E402

SCRIPT = Path(__file__).resolve().parents[1] / "recalc_sort_priority.py"


def run_cli(mod_dir):
    return subprocess.run([sys.executable, "-B", str(SCRIPT), "--mod-dir", str(mod_dir)], capture_output=True, text=True)


def names_and_priorities(mod_dir, file_name):
    rows = json.loads((Path(mod_dir) / "master" / file_name).read_text(encoding="utf-8"))["data"]
    return [row["name"] for row in rows], [row["sortPriority"] for row in rows]


def expected_priorities(count):
    return [100 + 10 * index for index in range(count)]


class RecalcSortCliTest(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory(prefix="master-refine-sort-")
        self.mod_dir = Path(self._temp.name)
        write_mod(self.mod_dir, build_mod(), False)

    def tearDown(self):
        self._temp.cleanup()

    def test_blocks_without_item_guid_follow_research_and_graph(self):
        # 初期解放→研究順（深さ・行・列・境界100）→重複解放は先の研究だけ→孤立は末尾。配列も同じ順に並ぶ
        # Initial, then research order (depth, rows, columns, 100 boundary), a repeated unlock only once, orphans last; the array follows
        done = run_cli(self.mod_dir)
        self.assertEqual(0, done.returncode, done.stderr)
        names, priorities = names_and_priorities(self.mod_dir, "blocks.json")
        self.assertEqual(EXPECTED_BLOCKS, names)
        self.assertEqual(expected_priorities(len(EXPECTED_BLOCKS)), priorities)

    def test_items_keep_existing_rule(self):
        self.assertEqual(0, run_cli(self.mod_dir).returncode)
        names, priorities = names_and_priorities(self.mod_dir, "items.json")
        self.assertEqual(EXPECTED_ITEMS, names)
        self.assertEqual(expected_priorities(len(EXPECTED_ITEMS)), priorities)

    def test_empty_roles_finish_successfully_after_writing(self):
        # 片方だけ空・両方空でも保存後の集計が失敗しない
        # Empty roles must not fail reporting after files have been written
        for empty_roles in (("items",), ("blocks",), ("items", "blocks")):
            with self.subTest(empty_roles=empty_roles):
                mod = build_mod()
                mod["master/research.json"]["data"] = []
                mod[".mooreseditor/nodeGraph.v1.json"]["nodes"] = []
                for role in empty_roles:
                    mod[f"master/{role}.json"]["data"] = []
                write_mod(self.mod_dir, mod, False)
                done = subprocess.run([sys.executable, "-B", str(SCRIPT), "--mod-dir", str(self.mod_dir), "--quiet"], capture_output=True, text=True)
                self.assertEqual(0, done.returncode, done.stderr)
                for role in empty_roles:
                    self.assertIn(f"Updated 0 {role}", done.stdout)
                    self.assertEqual(([], []), names_and_priorities(self.mod_dir, f"{role}.json"))
                self.assertEqual(len(empty_roles), done.stdout.count("sortPriority range: none"))

    def test_rerun_is_byte_identical(self):
        self.assertEqual(0, run_cli(self.mod_dir).returncode)
        first = {p: p.read_bytes() for p in sorted(self.mod_dir.rglob("*.json"))}
        self.assertEqual(0, run_cli(self.mod_dir).returncode)
        self.assertEqual(first, {p: p.read_bytes() for p in sorted(self.mod_dir.rglob("*.json"))})

    def test_non_sort_values_and_missing_trailing_newline_are_kept(self):
        self.assertEqual(0, run_cli(self.mod_dir).returncode)
        text = (self.mod_dir / "master" / "blocks.json").read_text(encoding="utf-8")
        doc = json.loads(text)
        self.assertFalse(text.endswith("\n"))
        self.assertEqual([{"ratio": 1.5}], doc["connectorSettings"])
        self.assertTrue(all(row["blockParam"] == {"idlePowerRate": 0.2} for row in doc["data"]))

    def test_trailing_newline_is_kept_when_present(self):
        write_mod(self.mod_dir, build_mod(), True)
        self.assertEqual(0, run_cli(self.mod_dir).returncode)
        self.assertTrue((self.mod_dir / "master" / "blocks.json").read_text(encoding="utf-8").endswith("\n"))

    def test_duplicate_key_stops_before_writing(self):
        path = self.mod_dir / "master" / "blocks.json"
        original = path.read_text(encoding="utf-8").replace('"connectorSettings"', '"data": [],\n  "connectorSettings"', 1)
        path.write_text(original, encoding="utf-8")
        items_before = (self.mod_dir / "master" / "items.json").read_bytes()
        done = run_cli(self.mod_dir)
        self.assertNotEqual(0, done.returncode)
        self.assertIn("duplicate key", done.stderr)
        self.assertIn("blocks.json", done.stderr)
        self.assertEqual(original, path.read_text(encoding="utf-8"))
        self.assertEqual(items_before, (self.mod_dir / "master" / "items.json").read_bytes())


if __name__ == "__main__":
    unittest.main()
