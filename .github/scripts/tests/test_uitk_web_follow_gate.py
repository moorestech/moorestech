import contextlib
import io
import unittest
from unittest.mock import patch

from uitk_web_follow_testrepo import TestRepo, ledger_text
from uitk_web_follow.cli import LEDGER_PATH, main
from uitk_web_follow.git_history import web_commits

HUD_FILE = "moorestech_web/webui/src/features/commonHud/Crosshair.tsx"
APP_FILE = "moorestech_web/webui/src/app/App.tsx"
NEW_FILE = "moorestech_web/webui/src/features/newScreen/index.tsx"
SHELL = ("アプリ殻", "5", "Web", "`src/`")


def rows(hud_owner="Web", app_owner="Web"):
    return [("commonHud", "2", hud_owner, "`src/features/commonHud/`"), SHELL,
            ("オーバーレイ: 背景スキット", "2", app_owner, "`src/app/App.tsx`"),
            ("オーバーレイ: 再接続", "5", "Web", "`src/app/App.tsx`")]


class FollowGateTest(unittest.TestCase):
    def setUp(self):
        self.repo = TestRepo()
        self.start = self.repo.commit("web", {HUD_FILE: "v1\n", APP_FILE: "v1\n", LEDGER_PATH: ledger_text({}, rows(), [])})

    def tearDown(self):
        self.repo.cleanup()

    def run_gate(self):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = main(["--root", str(self.repo.root), "--base", "master", "--head", "pr"])
        return code, out.getvalue(), err.getvalue()

    def dark_master_then_pr(self, master_files, pr_ledger):
        # master で束 2 を暗転にし、起点より後に Web を変え、PR で台帳を書き換える
        # Darken bundle 2 on master, change Web after the start, then rewrite the ledger on a PR branch
        self.repo.commit("dark", {LEDGER_PATH: ledger_text({2: ("暗転", f"`{self.start[:10]}`")}, rows(), [])})
        changed = self.repo.commit("web fix", master_files)
        self.repo.git("checkout", "-q", "-b", "pr")
        self.repo.commit("pr", {LEDGER_PATH: "<!-- PR ledger -->\n" + pr_ledger(changed)})
        return changed

    def switched(self, records):
        # 束 2 だけを持つ行（commonHud と背景スキット）の所有者を UITK にする
        # Rows owned only by bundle 2 (commonHud and the background skit) become UITK
        return ledger_text({2: ("切替済み", f"`{self.start[:10]}`")}, rows("UITK", "UITK"), records)

    def test_switch_with_unrecorded_web_change_is_red(self):
        changed = self.dark_master_then_pr({HUD_FILE: "v2\n"}, lambda sha: self.switched([]))
        code, _, err = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("G1-1", err)
        self.assertIn(changed[:10], err)

    def test_switch_with_recorded_web_change_is_green(self):
        self.dark_master_then_pr({HUD_FILE: "v2\n"}, lambda sha: self.switched([(2, sha[:10], "写した", "UITK の Crosshair")]))
        code, *_, err = self.run_gate()
        self.assertEqual(code, 0, err)

    def test_switch_with_discarded_web_change_is_green(self):
        self.dark_master_then_pr({HUD_FILE: "v2\n"}, lambda sha: self.switched([(2, sha[:10], "捨てた", "UITK では別の形で描く")]))
        code, *_, err = self.run_gate()
        self.assertEqual(code, 0, err)

    def test_shared_app_file_counts_for_switching_bundle(self):
        self.dark_master_then_pr({APP_FILE: "v2\n"}, lambda sha: self.switched([]))
        code, _, err = self.run_gate()
        self.assertEqual(code, 1, err)
        self.assertIn("G1-1", err)

    def test_change_before_dark_start_needs_no_record(self):
        self.repo.commit("old fix", {HUD_FILE: "v0\n"})
        start = self.repo.git("rev-parse", "HEAD")
        self.repo.commit("dark", {LEDGER_PATH: ledger_text({2: ("暗転", f"`{start[:10]}`")}, rows(), [])})
        self.repo.git("checkout", "-q", "-b", "pr")
        self.repo.commit("pr", {LEDGER_PATH: ledger_text({2: ("切替済み", f"`{start[:10]}`")}, rows("UITK", "UITK"), [])})
        code, *_, err = self.run_gate()
        self.assertEqual(code, 0, err)

    def test_dark_bundle_reports_unrecorded_count_without_failing(self):
        self.dark_master_then_pr({HUD_FILE: "v2\n"}, lambda sha: ledger_text({2: ("暗転", f"`{self.start[:10]}`")}, rows(), []))
        code, out, _ = self.run_gate()
        self.assertEqual(code, 0)
        self.assertIn("未記帳 1 件", out)

    def test_start_not_in_history_is_red(self):
        self.repo.git("checkout", "-q", "-b", "pr")
        self.repo.commit("pr", {LEDGER_PATH: ledger_text({2: ("暗転", "`0123456789`")}, rows(), [])})
        code, _, err = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("祖先に無い", err)


    def test_merge_commit_is_excluded_from_follow_history(self):
        self.repo.git("checkout", "-q", "-b", "fix")
        changed = self.repo.commit("fix", {HUD_FILE: "v2\n"})
        self.repo.git("checkout", "-q", "master")
        self.repo.git("merge", "--no-ff", "-m", "merge fix", "fix")
        merge = self.repo.git("rev-parse", "HEAD")
        commits = web_commits(self.repo.root, self.start, merge)
        self.assertEqual([item.sha for item in commits], [changed])

    def test_missing_head_ledger_is_red(self):
        self.repo.git("checkout", "-q", "-b", "pr")
        self.repo.commit("remove ledger", {LEDGER_PATH: None})
        code, _, err = self.run_gate()
        self.assertEqual(code, 1, err)
        self.assertIn("head に無い", err)

    def test_unreadable_head_ledger_is_red(self):
        self.repo.git("checkout", "-q", "-b", "pr")
        self.repo.commit("bad ledger", {LEDGER_PATH: "broken\n"})
        code, _, err = self.run_gate()
        self.assertEqual(code, 1, err)
        self.assertIn("台帳を読めない", err)

    def test_missing_base_ledger_and_summary_are_green(self):
        self.repo.commit("remove base ledger", {LEDGER_PATH: None})
        self.repo.git("checkout", "-q", "-b", "pr")
        self.repo.commit("introduce ledger", {LEDGER_PATH: ledger_text({}, rows(), [])})
        summary = self.repo.root / "summary.md"
        with patch.dict("os.environ", {"GITHUB_STEP_SUMMARY": str(summary)}):
            code, out, err = self.run_gate()
        self.assertEqual(code, 0, err)
        self.assertIn("全束を Web", out)
        self.assertIn("UITK Web follow gate", summary.read_text())

class RetiredBundleTest(unittest.TestCase):
    def setUp(self):
        self.repo = TestRepo()
        self.start = self.repo.commit("web", {HUD_FILE: "v1\n", APP_FILE: "v1\n"})
        self.switched_ledger = ledger_text({2: ("切替済み", f"`{self.start[:10]}`")}, rows("UITK", "UITK"), [])
        self.repo.commit("switch", {LEDGER_PATH: self.switched_ledger})
        self.repo.git("checkout", "-q", "-b", "pr")

    def tearDown(self):
        self.repo.cleanup()

    def run_gate(self):
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = main(["--root", str(self.repo.root), "--base", "master", "--head", "pr"])
        return code, err.getvalue()

    def test_change_to_switched_bundle_web_path_is_red(self):
        self.repo.commit("late web fix", {HUD_FILE: "v2\n"})
        code, err = self.run_gate()
        self.assertEqual(code, 1)
        self.assertIn("G1-2", err)

    def test_removing_row_cannot_unretire_web_path(self):
        no_hud = [row for row in rows("UITK", "UITK") if row[0] != "commonHud"]
        self.repo.commit("drop row and fix", {HUD_FILE: "v2\n", LEDGER_PATH: ledger_text({2: ("切替済み", f"`{self.start[:10]}`")}, no_hud, [])})
        code, err = self.run_gate()
        self.assertEqual(code, 1, err)
        self.assertIn("G1-2", err)

    def test_delete_of_switched_bundle_web_path_is_green(self):
        self.repo.commit("delete", {HUD_FILE: None})
        code, *_, err = self.run_gate()
        self.assertEqual(code, 0, err)

    def test_shared_path_with_unswitched_owner_is_green(self):
        self.repo.commit("app fix", {APP_FILE: "v2\n"})
        code, *_, err = self.run_gate()
        self.assertEqual(code, 0, err)

    def test_rollback_to_dark_allows_web_fix(self):
        start = self.repo.git("rev-parse", "master")
        self.repo.commit("rollback", {HUD_FILE: "v2\n", LEDGER_PATH: ledger_text({2: ("暗転", f"`{start[:10]}`")}, rows(), [])})
        code, *_, err = self.run_gate()
        self.assertEqual(code, 0, err)

    def test_rename_into_switched_bundle_is_red(self):
        moved = HUD_FILE.replace("Crosshair", "RenamedCrosshair")
        self.repo.git("mv", HUD_FILE, moved)
        self.repo.git("commit", "-q", "-m", "rename")
        code, err = self.run_gate()
        self.assertEqual(code, 1, err)
        self.assertIn("G1-2", err)
        self.assertIn(moved, err)

    def test_uncovered_new_screen_is_red(self):
        # アプリ殻（`src/` の受け皿）を外した台帳で、どの行にも入らない新しい画面を足す
        # Drop the shell row (the `src/` catch-all) and add a screen no row covers
        no_shell = [row for row in rows("UITK", "UITK") if row[0] != "アプリ殻"]
        self.repo.commit("new screen", {NEW_FILE: "x\n", LEDGER_PATH: ledger_text({2: ("切替済み", f"`{self.start[:10]}`")}, no_shell, [])})
        code, err = self.run_gate()
        self.assertEqual(code, 1, err)
        self.assertIn("G1-0", err)


if __name__ == "__main__":
    unittest.main()
