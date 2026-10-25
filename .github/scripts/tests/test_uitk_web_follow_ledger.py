import unittest

from uitk_web_follow_testrepo import ledger_text
from uitk_web_follow.ledger import STATE_SWITCHED, parse_ledger
from uitk_web_follow.ownership import is_retired, owning_bundles, owning_rows, uncovered_sources

HUD = ("commonHud", "2", "Web", "`src/features/commonHud/`")
SHELL = ("アプリ殻", "5", "Web", "`src/`")
APP_OVERLAY = ("オーバーレイ: 背景スキット", "2", "Web", "`src/app/App.tsx`")
APP_SHELL = ("オーバーレイ: 再接続", "5", "Web", "`src/app/App.tsx`")
SHA = "a" * 40


class LedgerParseTest(unittest.TestCase):
    def parse_ok(self, text):
        result = parse_ledger(text)
        self.assertEqual(result.errors, ())
        return result.ledger

    def parse_error(self, text, fragment):
        result = parse_ledger(text)
        self.assertIsNone(result.ledger)
        self.assertTrue(any(fragment in error for error in result.errors), result.errors)

    def test_reads_bundles_rows_and_records(self):
        ledger = self.parse_ok(ledger_text({2: ("暗転", f"`{SHA[:10]}`")}, [HUD], [(2, SHA[:10], "写した", "UITK の Crosshair")]))
        self.assertEqual(ledger.bundles[2].dark_start, SHA[:10])
        self.assertEqual(ledger.rows[0].web_paths, ("moorestech_web/webui/src/features/commonHud/",))
        self.assertEqual(ledger.records[0].verdict, "写した")

    def test_rejects_dark_bundle_without_start(self):
        self.parse_error(ledger_text({2: ("暗転", "—")}, [HUD], []), "暗転の起点")

    def test_rejects_web_bundle_with_start(self):
        self.parse_error(ledger_text({2: ("Web", f"`{SHA[:10]}`")}, [HUD], []), "Web なのに")

    def test_rejects_unknown_state(self):
        self.parse_error(ledger_text({2: ("凍結", "—")}, [HUD], []), "状態")

    def test_rejects_owner_that_disagrees_with_bundle_state(self):
        self.parse_error(ledger_text({2: (STATE_SWITCHED, f"`{SHA[:10]}`")}, [HUD], []), "所有者")

    def test_rejects_discard_without_reason(self):
        self.parse_error(ledger_text({2: ("暗転", f"`{SHA[:10]}`")}, [HUD], [(2, SHA[:10], "捨てた", "—")]), "理由")

    def test_rejects_unknown_verdict(self):
        self.parse_error(ledger_text({2: ("暗転", f"`{SHA[:10]}`")}, [HUD], [(2, SHA[:10], "保留", "x")]), "判定")

    def test_rejects_row_without_paths(self):
        self.parse_error(ledger_text({}, [("空", "2", "Web", "—")], []), "Web パスが無い")

    def test_rejects_renamed_header(self):
        self.parse_error(ledger_text({}, [HUD], []).replace("| 束 | 中身 |", "| 段階 | 中身 |"), "表の見出し")

    def test_rejects_missing_section(self):
        self.parse_error(ledger_text({}, [HUD], []).replace("## 追従記録", "## 記録"), "追従記録")


    def test_rejects_duplicate_bundle(self):
        text = ledger_text({}, [HUD], [])
        self.parse_error(text.replace("| 1 | 命綱 | Web | — | — |", "| 1 | 命綱 | Web | — | — |\n| 1 | 重複 | Web | — | — |"), "重複")

    def test_rejects_missing_bundle(self):
        self.parse_error(ledger_text({}, [HUD], []).replace("| 5 | 最後 | Web | — | — |\n", ""), "ちょうど")

    def test_rejects_duplicate_section(self):
        self.parse_error(ledger_text({}, [HUD], []) + "\n## 行\n", "重複")

    def test_rejects_missing_or_invalid_table_separator(self):
        text = ledger_text({}, [HUD], [(2, SHA, "写した", "destination")])
        for replacement in ("", "| wrong | separator | data | row |\n"):
            with self.subTest(replacement=replacement):
                self.parse_error(text.replace("|---|---|---|---|\n| 2 |", replacement + "| 2 |"), "区切り行")

    def test_rejects_column_count_and_missing_closing_pipe(self):
        text = ledger_text({}, [HUD], [])
        self.parse_error(text.replace("| 1 | 命綱 | Web | — | — |", "| 1 | 命綱 | Web | — |"), "列数")
        self.parse_error(text.replace("| 1 | 命綱 | Web | — | — |", "| 1 | 命綱 | Web | — | —"), "行末")

    def test_rejects_bad_bundle_ids_and_commits(self):
        for bundle_id in ("0", "6", "²", ""):
            with self.subTest(bundle_id=bundle_id):
                self.parse_error(ledger_text({}, [("bad", bundle_id, "Web", "`src/`")], []), "束")
        for commit in ("a" * 6, "a" * 41, "g" * 10, "—"):
            with self.subTest(commit=commit):
                self.parse_error(ledger_text({}, [HUD], [(2, commit, "写した", "x")]), "コミット")

    def test_rejects_unknown_record_bundle(self):
        self.parse_error(ledger_text({}, [HUD], [(6, SHA, "写した", "x")]), "束")

    def test_rejects_noncanonical_relative_paths(self):
        for path in ("/src/", "../src/", "src/../app/", "src//app/", "./src/", "src\\app/"):
            with self.subTest(path=path):
                self.parse_error(ledger_text({}, [("bad", "2", "Web", f"`{path}`")], []), "相対パス")

    def test_empty_ledger_and_multiple_bundle_row(self):
        from uitk_web_follow.ledger import empty_ledger
        self.assertEqual(tuple(empty_ledger().bundles), (1, 2, 3, 4, 5))
        ledger = self.parse_ok(ledger_text({}, [("共有", "2 5", "Web", "`src/a.ts` `e2e/a.ts`")], []))
        self.assertEqual(ledger.rows[0].bundle_ids, (2, 5))
        self.assertEqual(len(ledger.rows[0].web_paths), 2)


class OwnershipTest(unittest.TestCase):
    def setUp(self):
        self.ledger = parse_ledger(ledger_text({2: (STATE_SWITCHED, f"`{SHA[:10]}`")}, [
            ("commonHud", "2", "UITK", "`src/features/commonHud/`"), SHELL,
            ("オーバーレイ: 背景スキット", "2", "UITK", "`src/app/App.tsx`"), APP_SHELL,
        ], [])).ledger

    def test_longest_prefix_wins(self):
        rows = owning_rows("moorestech_web/webui/src/features/commonHud/Crosshair.tsx", self.ledger.rows)
        self.assertEqual([row.name for row in rows], ["commonHud"])

    def test_equal_paths_share_ownership(self):
        self.assertEqual(owning_bundles("moorestech_web/webui/src/app/App.tsx", self.ledger.rows), frozenset({2, 5}))

    def test_retired_only_when_every_owner_bundle_is_switched(self):
        self.assertTrue(is_retired("moorestech_web/webui/src/features/commonHud/Crosshair.tsx", self.ledger))
        self.assertFalse(is_retired("moorestech_web/webui/src/app/App.tsx", self.ledger))

    def test_exact_file_and_directory_boundary(self):
        self.assertEqual(owning_bundles("moorestech_web/webui/src/app/App.tsx.extra", self.ledger.rows), frozenset({5}))
        self.assertEqual(owning_bundles("moorestech_web/webui/src/features/commonHudExtra/a.tsx", self.ledger.rows), frozenset({5}))
        self.assertFalse(is_retired("outside.ts", self.ledger))

    def test_uncovered_sources_lists_only_src_paths_without_rows(self):
        ledger = parse_ledger(ledger_text({}, [HUD], [])).ledger
        paths = ("moorestech_web/webui/src/features/commonHud/a.tsx", "moorestech_web/webui/src/features/newScreen/a.tsx", "moorestech_web/webui/e2e/x.ts")
        self.assertEqual(uncovered_sources(paths, ledger.rows), ("moorestech_web/webui/src/features/newScreen/a.tsx",))


if __name__ == "__main__":
    unittest.main()
