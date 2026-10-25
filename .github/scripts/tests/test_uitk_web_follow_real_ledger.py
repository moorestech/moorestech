"""Validate the committed migration ledger against the real Web source inventory."""

import subprocess
import unittest
from pathlib import Path

import uitk_web_follow_testrepo  # noqa: F401  (sys.path に .github/scripts を足す)
from uitk_web_follow.cli import LEDGER_PATH
from uitk_web_follow.ledger import parse_ledger
from uitk_web_follow.ownership import WEB_SOURCE_ROOT, uncovered_sources

REPO_ROOT = Path(__file__).resolve().parents[3]
FEATURES = "moorestech_web/webui/src/features/"


class RealLedgerTest(unittest.TestCase):
    def setUp(self):
        result = parse_ledger((REPO_ROOT / LEDGER_PATH).read_text(encoding="utf-8"))
        self.assertEqual(result.errors, ())
        self.ledger = result.ledger
        listing = subprocess.run(["git", "ls-files", "--", WEB_SOURCE_ROOT], cwd=REPO_ROOT, check=True, capture_output=True, text=True)
        self.sources = tuple(line for line in listing.stdout.splitlines() if line)

    def test_every_web_source_is_covered(self):
        self.assertEqual(uncovered_sources(self.sources, self.ledger.rows), ())

    def test_every_feature_directory_has_its_own_row(self):
        # 20 機能それぞれのディレクトリが、どこかの行の Web パスに名前で現れる
        # Each of the 20 feature directories appears by name in some row's Web paths
        features = sorted({path[len(FEATURES):].split("/")[0] for path in self.sources if path.startswith(FEATURES)})
        listed = " ".join(path for row in self.ledger.rows for path in row.web_paths)
        self.assertEqual([feature for feature in features if f"{FEATURES}{feature}/" not in listed], [])
        self.assertGreaterEqual(len(features), 20)

    def test_app_overlays_remain_explicit_rows(self):
        # 状態機械だけでは拾えない表示面を台帳から消させない
        # Preserve overlay surfaces that cannot be found from the state machine alone
        overlays = ("背景スキット", "Ctrl+U の全体非表示", "カットシーン中の退避", "背面の通知層",
                    "カーソル追従", "常駐 HUD の可否", "画面のマウント点", "再接続・辞書エラー")
        app_rows = [row for row in self.ledger.rows if "moorestech_web/webui/src/app/App.tsx" in row.web_paths]
        for overlay in overlays:
            self.assertTrue(any(overlay in row.name for row in app_rows), overlay)

    def test_tier9_special_family_rows_are_bundle_four(self):
        for name in ("details/rocket/", "details/spaceElevator/"):
            rows = [row for row in self.ledger.rows if any(path.endswith(name) for path in row.web_paths)]
            self.assertEqual([row.bundle_ids for row in rows], [(4,)])


if __name__ == "__main__":
    unittest.main()
