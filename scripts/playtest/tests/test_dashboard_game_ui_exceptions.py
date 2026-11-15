#!/usr/bin/env python3
"""ダッシュボードが manifest v7 の gameUiExceptions を欄なし・不明・読めない・記録ありの 4 状態に分けることを検証する。
Checks that the dashboard splits manifest v7 gameUiExceptions into absent, unknown, unreadable and recorded.
"""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from digest_fixture import DISABLED_MARK  # noqa: E402
import test_dashboard  # noqa: E402


class DashboardGameUiExceptionsTest(unittest.TestCase):
    # 土台は test_dashboard の箱づくりを借りる（継承やクラスの import をすると向こうのテストまで走るため借りるだけ）
    # Borrows test_dashboard's box fixture without inheriting or importing the class, so its tests do not run again here
    setUp = test_dashboard.DashboardCollectTest.setUp
    tearDown = test_dashboard.DashboardCollectTest.tearDown
    add_report = test_dashboard.DashboardCollectTest.add_report
    reports_by_id = test_dashboard.DashboardCollectTest.reports_by_id

    def game_ui_exceptions_of(self, report_id, manifest_extra):
        self.add_report("7656005", report_id, {"kind": "bug", "remoteExec": DISABLED_MARK, **manifest_extra})
        return self.reports_by_id()[report_id]["gameUiExceptions"]

    def test_box_without_the_field_is_absent(self):
        self.assertEqual(self.game_ui_exceptions_of("20260913_v6", {}), {"status": "absent"})

    def test_uncounted_box_is_unknown(self):
        self.assertEqual(self.game_ui_exceptions_of("20260913_crash", {"gameUiExceptions": {"status": "unknown"}}), {"status": "unknown"})

    def test_recorded_with_non_integer_count_is_unreadable(self):
        value = {"gameUiExceptions": {"status": "recorded", "count": "x"}}
        self.assertEqual(self.game_ui_exceptions_of("20260913_typo", value), {"status": "unreadable"})

    def test_recorded_keeps_count_and_samples(self):
        samples = [{"tick": 10, "message": "boom", "frame": "Client.Ui.Screens.Probe.OnClick ()"},
                   {"tick": 12, "message": "boom 2", "frame": "Client.Ui.State.Probe.Run ()"}]
        row = self.game_ui_exceptions_of("20260913_ui", {"gameUiExceptions": {"status": "recorded", "count": 2, "samples": samples}})
        self.assertEqual(row["status"], "recorded")
        self.assertEqual(row["count"], 2)
        self.assertEqual(row["samples"], samples)


if __name__ == "__main__":
    unittest.main()
