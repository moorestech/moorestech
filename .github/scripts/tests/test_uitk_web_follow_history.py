"""Regression tests for blackout origins and historical ownership."""

import contextlib
import io
import unittest

from uitk_web_follow_testrepo import TestRepo, ledger_text
from uitk_web_follow.cli import LEDGER_PATH, main

OLD = "moorestech_web/webui/src/old/view.ts"
NEW = "moorestech_web/webui/src/new/view.ts"


class HistoricalOwnershipTest(unittest.TestCase):
    def setUp(self):
        self.repo = TestRepo()
        self.start = self.repo.commit("initial", {OLD: "1"})
        self.repo.commit("ledger", {LEDGER_PATH: self.ledger("暗転", "old")})

    def tearDown(self):
        self.repo.cleanup()

    def ledger(self, state, path, start=None, records=()):
        return ledger_text({2: (state, f"`{start or self.start}`")}, [
            ("HUD", "2", "UITK" if state == "切替済み" else "Web", f"`src/{path}/`"),
            ("shell", "5", "Web", "`src/`"),
        ], list(records))

    def gate(self):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            result = main(["--root", str(self.repo.root), "--base", "master", "--head", "pr"])
        return result, out.getvalue(), err.getvalue()

    def test_origin_change_cannot_skip_unrecorded_change(self):
        changed = self.repo.commit("change", {OLD: "2"})
        self.repo.git("checkout", "-qb", "pr")
        for state in ("暗転", "切替済み"):
            self.repo.commit(state, {LEDGER_PATH: self.ledger(state, "old", changed)})
            result, _, err = self.gate()
            self.assertEqual(result, 1)
            self.assertIn("起点は変更できない", err)

    def test_same_origin_abbreviation_is_valid(self):
        self.repo.git("checkout", "-qb", "pr")
        self.repo.commit("switch", {LEDGER_PATH: self.ledger("切替済み", "old", self.start[:10])})
        self.assertEqual(self.gate()[0], 0)

    def test_old_path_changes_and_rename_retain_previous_owner(self):
        changed = self.repo.commit("change", {OLD: "2"})
        renamed = self.repo.commit("rename", {OLD: None, NEW: "2", LEDGER_PATH: self.ledger("暗転", "new")})
        self.repo.git("checkout", "-qb", "pr")
        self.repo.commit("switch", {LEDGER_PATH: self.ledger("切替済み", "new")})
        result, _, err = self.gate()
        self.assertEqual(result, 1)
        self.assertIn(changed[:10], err)
        self.assertIn(renamed[:10], err)
        records = [(2, sha, "写した", "UITK") for sha in (changed, renamed)]
        self.repo.commit("record", {LEDGER_PATH: self.ledger("切替済み", "new", records=records)})
        self.assertEqual(self.gate()[0], 0)

    def test_deleted_path_and_reassigned_row_keep_obligation(self):
        changed = self.repo.commit("change", {OLD: "2"})
        removed = self.repo.commit("delete", {OLD: None, LEDGER_PATH: self.ledger("暗転", "new")})
        self.repo.git("checkout", "-qb", "pr")
        self.repo.commit("switch", {LEDGER_PATH: self.ledger("切替済み", "new")})
        result, _, err = self.gate()
        self.assertEqual(result, 1)
        for sha in (changed, removed):
            self.assertIn(sha[:10], err)

    def test_invalid_historical_ledger_fails_even_if_repaired(self):
        bad = self.repo.commit("bad ledger", {OLD: "2", LEDGER_PATH: "broken"})
        self.repo.commit("repair", {LEDGER_PATH: self.ledger("暗転", "old")})
        self.repo.git("checkout", "-qb", "pr")
        self.repo.commit("switch", {LEDGER_PATH: self.ledger("切替済み", "old", records=[(2, bad, "写した", "UITK")])})
        result, _, err = self.gate()
        self.assertEqual(result, 1)
        self.assertIn(f"履歴台帳 {bad[:10]}", err)

    def test_missing_historical_ledger_fails_after_introduction(self):
        missing = self.repo.commit("remove ledger", {OLD: "2", LEDGER_PATH: None})
        self.repo.commit("repair", {LEDGER_PATH: self.ledger("暗転", "old")})
        self.repo.git("checkout", "-qb", "pr")
        self.repo.commit("switch", {LEDGER_PATH: self.ledger("切替済み", "old")})
        result, _, err = self.gate()
        self.assertEqual(result, 1)
        self.assertIn(f"履歴台帳 {missing[:10]}: 導入後", err)

    def test_pre_introduction_change_uses_initial_ledger_explicitly(self):
        self.repo.git("checkout", "-qb", "early", self.start)
        early = self.repo.commit("early Web", {OLD: "2"})
        self.repo.git("checkout", "master")
        self.repo.git("merge", "--no-ff", "-m", "merge early", "early")
        self.repo.git("checkout", "-qb", "pr")
        self.repo.commit("switch", {LEDGER_PATH: self.ledger("切替済み", "old")})
        result, out, err = self.gate()
        self.assertEqual(result, 1)
        self.assertIn(early[:10], err)
        self.assertIn("台帳導入前", out)
        self.repo.commit("record", {LEDGER_PATH: self.ledger("切替済み", "old", records=[(2, early, "写した", "UITK")])})
        self.assertEqual(self.gate()[0], 0)
