"""Exercise history and record boundaries through real Git and the gate CLI."""

import contextlib
import io
import unittest

from uitk_web_follow_testrepo import TestRepo, ledger_text
from uitk_web_follow.cli import LEDGER_PATH, main

HUD = "moorestech_web/webui/src/features/commonHud/Crosshair.tsx"
OTHER = "moorestech_web/webui/src/other.ts"


class GateBoundaryTest(unittest.TestCase):
    def setUp(self):
        self.repo = TestRepo()
        self.start = self.repo.commit("initial", {HUD: "v1", OTHER: "v1"})
        self.repo.commit("dark", {LEDGER_PATH: self.ledger("暗転", [])})

    def tearDown(self):
        self.repo.cleanup()

    def ledger(self, state, records):
        owner = "UITK" if state == "切替済み" else "Web"
        return ledger_text({2: (state, f"`{self.start[:10]}`")}, [
            ("HUD", "2", owner, "`src/features/commonHud/`"),
            ("shell", "5", "Web", "`src/`"),
        ], records)

    def switch(self, records):
        self.repo.git("checkout", "-q", "-b", "pr")
        self.repo.commit("switch", {LEDGER_PATH: self.ledger("切替済み", records)})

    def run_gate(self):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = main(["--root", str(self.repo.root), "--base", "master", "--head", "pr"])
        return code, out.getvalue(), err.getvalue()

    def assert_missing(self, sha):
        code, _, err = self.run_gate()
        self.assertEqual(code, 1, err)
        self.assertIn("G1-1", err)
        self.assertIn(sha[:10], err)

    def test_record_for_another_bundle_does_not_cover_same_sha(self):
        changed = self.repo.commit("HUD", {HUD: "v2"})
        self.switch([(5, changed[:10], "写した", "other bundle")])
        self.assert_missing(changed)

    def test_record_for_another_sha_does_not_cover_same_bundle(self):
        changed = self.repo.commit("HUD", {HUD: "v2"})
        self.switch([(2, self.start[:10], "写した", "old change")])
        self.assert_missing(changed)

    def test_commit_touching_multiple_bundles_needs_hud_record(self):
        changed = self.repo.commit("both", {HUD: "v2", OTHER: "v2"})
        self.switch([])
        self.assert_missing(changed)

    def test_change_owned_only_by_other_bundle_needs_no_hud_record(self):
        changed = self.repo.commit("other", {OTHER: "v2"})
        self.switch([])
        code, _, err = self.run_gate()
        self.assertEqual(code, 0, err)
        self.assertNotIn("G1-1", err)
        self.assertNotIn(changed[:10], err)

    def test_pr_can_change_web_and_switch_with_record(self):
        self.repo.git("checkout", "-q", "-b", "pr")
        changed = self.repo.commit("HUD", {HUD: "v2"})
        self.repo.commit("switch", {LEDGER_PATH: self.ledger("切替済み", [(2, changed[:10], "写した", "UITK")])})
        code, _, err = self.run_gate()
        self.assertEqual(code, 0, err)
        self.assertNotIn("G1-2", err)
        self.assertNotIn(changed[:10], err)

    def test_merged_fix_and_revert_both_need_records(self):
        self.repo.git("checkout", "-q", "-b", "fix")
        fix = self.repo.commit("fix", {HUD: "v2"})
        revert = self.repo.commit("revert", {HUD: "v1"})
        self.repo.git("checkout", "-q", "master")
        self.repo.git("merge", "--no-ff", "-m", "merge", "fix")
        self.switch([])
        self.assert_missing(fix)
        self.assert_missing(revert)
        records = [(2, sha[:10], "捨てた", "reverted") for sha in (fix, revert)]
        self.repo.commit("record both", {LEDGER_PATH: self.ledger("切替済み", records)})
        code, _, err = self.run_gate()
        self.assertEqual(code, 0, err)

    def test_unquoted_path_cannot_erase_retired_ownership(self):
        retired = self.ledger("切替済み", []).replace("`src/features/commonHud/`", "`e2e/hud.ts` `src/features/commonHud/`")
        self.repo.commit("already switched", {LEDGER_PATH: retired})
        self.repo.git("checkout", "-q", "-b", "pr")
        malformed = retired.replace("`src/features/commonHud/`", "src/features/commonHud/")
        self.repo.commit("malformed owner", {HUD: "v2", LEDGER_PATH: malformed})
        code, _, err = self.run_gate()
        self.assertEqual(code, 1, err)
        self.assertIn("引用されていない", err)
