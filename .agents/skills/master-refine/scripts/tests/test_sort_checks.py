#!/usr/bin/env python3
"""操作 A の検証と根拠: 書き戻し前の検証が対象外の変化を止めること、重複解放・孤立の根拠が Placement に出ること。
Operation A checks and basis: pre-write checks stop unrelated changes, and Placements carry the unlocking research or orphan kind."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import master_json_io  # noqa: E402
import sort_order  # noqa: E402
from sort_fixture import build_mod, write_mod  # noqa: E402
from test_recalc_sort_cli import run_cli  # noqa: E402


class SortChecksTest(unittest.TestCase):
    def setUp(self):
        self._temp = tempfile.TemporaryDirectory(prefix="master-refine-checks-")
        self.mod_dir = Path(self._temp.name)
        write_mod(self.mod_dir, build_mod(), False)
        self.blocks_path = self.mod_dir / "master" / "blocks.json"

    def tearDown(self):
        self._temp.cleanup()

    def test_non_sort_change_is_rejected_with_decimal_precision(self):
        loaded = master_json_io.read_master_json(self.blocks_path, True)
        changed = loaded.text.replace('"idlePowerRate": 0.2', '"idlePowerRate": 0.20000000000000001', 1)
        with self.assertRaisesRegex(master_json_io.MasterJsonError, "beyond sortPriority"):
            master_json_io.verify_only_sort_changed(loaded, changed, "blockGuid")

    def test_top_level_change_is_rejected(self):
        loaded = master_json_io.read_master_json(self.blocks_path, True)
        with self.assertRaisesRegex(master_json_io.MasterJsonError, "top-level"):
            master_json_io.verify_only_sort_changed(loaded, loaded.text.replace('"ratio": 1.5', '"ratio": 2.5'), "blockGuid")

    def test_reorder_and_sort_change_pass(self):
        loaded = master_json_io.read_master_json(self.blocks_path, True)
        rows = list(reversed(loaded.doc["data"]))
        for index, row in enumerate(rows):
            row["sortPriority"] = index
        master_json_io.verify_only_sort_changed(loaded, master_json_io.render(dict(loaded.doc, data=rows), False), "blockGuid")

    def test_text_that_does_not_round_trip_is_refused(self):
        self.blocks_path.write_text(json.dumps(json.loads(self.blocks_path.read_text(encoding="utf-8")), indent=4), encoding="utf-8")
        before = self.blocks_path.read_bytes()
        done = run_cli(self.mod_dir)
        self.assertNotEqual(0, done.returncode)
        self.assertIn("does not round-trip", done.stderr)
        self.assertEqual(before, self.blocks_path.read_bytes())

    def test_row_without_sort_priority_stops(self):
        mod = build_mod()
        del mod["master/blocks.json"]["data"][0]["sortPriority"]
        write_mod(self.mod_dir, mod, False)
        done = run_cli(self.mod_dir)
        self.assertNotEqual(0, done.returncode)
        self.assertIn("rows without sortPriority ['B_orphan_late']", done.stderr)

    def test_unknown_unlock_target_is_reported(self):
        mod = build_mod()
        mod["master/research.json"]["data"][0]["clearedActions"][0]["gameActionParam"]["unlockBlockGuids"].append("B_missing")
        write_mod(self.mod_dir, mod, False)
        done = run_cli(self.mod_dir)
        self.assertEqual(0, done.returncode, done.stderr)
        self.assertIn("research 'R0' unlocks B_missing, which is not in blocks.json", done.stderr)

    def test_placements_name_the_first_unlocking_research_once(self):
        mod = build_mod()
        research = mod["master/research.json"]["data"]
        positions = sort_order.node_positions(mod[".mooreseditor/nodeGraph.v1.json"])
        order = sort_order.ordered_research(research, positions)
        self.assertEqual(["R0", "R1b", "R1a", "R1c", "R2"], [r["researchNodeName"] for r in order])
        placements, unknown = sort_order.plan_placements(mod["master/blocks.json"]["data"], sort_order.BLOCK_ROLE, order, positions)
        basis = {p.guid: (p.kind, p.research_name) for p in placements}
        self.assertEqual([], unknown)
        self.assertEqual(1, [p.guid for p in placements].count("B_multi"))
        self.assertEqual(("RES", "R1c"), basis["B_multi"])
        self.assertEqual(("INIT", None), basis["B_init_early"])
        self.assertEqual(("ORPHAN", None), basis["B_orphan_zero_a"])


if __name__ == "__main__":
    unittest.main()
