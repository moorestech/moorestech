"""Validate the committed migration ledger against the real Web source inventory."""

from dataclasses import replace
import subprocess
import unittest
from pathlib import Path

import uitk_web_follow_testrepo  # noqa: F401  (sys.path に .github/scripts を足す)
from uitk_web_follow.cli import LEDGER_PATH
from uitk_web_follow.ledger import STATE_SWITCHED, STATE_WEB, parse_ledger
from uitk_web_follow.checks import check_retired_changes, unrecorded_commits
from uitk_web_follow.git_history import ChangedFile, CommitFiles
from uitk_web_follow.ownership import WEB_SOURCE_ROOT, owning_bundles, uncovered_sources

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
        # 現存する機能だけを照合し、切替後の正規撤去を許す
        # Check only existing features so legitimate post-switch removal stays allowed
        features = sorted({path[len(FEATURES):].split("/")[0] for path in self.sources if path.startswith(FEATURES)})
        listed = " ".join(path for row in self.ledger.rows for path in row.web_paths)
        self.assertEqual([feature for feature in features if f"{FEATURES}{feature}/" not in listed], [])

    def test_tutorial_shared_paths_keep_each_bundles_follow_obligation(self):
        expected = {
            "src/features/tutorial/overlay/style.module.css": {4, 5},
            "src/features/tutorial/overlay/TutorialOverlay.tsx": {4, 5},
            "src/features/tutorial/overlay/TutorialOverlay.test.ts": {4, 5},
            "src/features/tutorial/tutorialElement.ts": {2, 4, 5},
            "src/features/tutorial/tutorialAttentionDesign.test.ts": {2, 4, 5},
            "e2e/mock-host/topics/tutorialControls.ts": {2, 4, 5},
            "src/features/tutorial/index.ts": {2, 4, 5},
            "e2e/tests/system/tutorial.spec.ts": {2, 5},
        }
        for relative, owners in expected.items():
            path = "moorestech_web/webui/" + relative
            if not (REPO_ROOT / path).exists():
                continue  # silent-ok: 正規撤去を許す / 現存パスの所有だけをこのテストで検証する
            with self.subTest(path=relative):
                self.assertEqual(owning_bundles(path, self.ledger.rows), frozenset(owners))
                for bundle in owners:
                    commits = (CommitFiles("a" * 40, (path,)),)
                    self.assertEqual(unrecorded_commits(self.ledger, bundle, commits, {"a" * 40: owning_bundles(path, self.ledger.rows)}), ("a" * 40,))
                    # 実台帳の進捗から独立して共有所有を検証する
                    # Verify shared ownership independently of live migration progress
                    bundles = {key: replace(value, state=STATE_WEB) for key, value in self.ledger.bundles.items()}
                    bundles[bundle] = replace(bundles[bundle], state=STATE_SWITCHED)
                    shared = replace(self.ledger, bundles=bundles)
                    self.assertEqual(check_retired_changes(shared, shared, (ChangedFile("M", path),)), ())

    def test_tier9_special_family_rows_are_bundle_four(self):
        for name in ("details/rocket/", "details/spaceElevator/"):
            rows = [row for row in self.ledger.rows if any(path.endswith(name) for path in row.web_paths)]
            for row in rows:
                self.assertEqual(row.bundle_ids, (4,))


if __name__ == "__main__":
    unittest.main()
