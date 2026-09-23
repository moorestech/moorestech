import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts" / "requirements"
sys.path.insert(0, str(SCRIPTS))
import main as runtime_main
from process_owner import ProcessOwner
try:
    from .paid_cli_guard import reject_paid_cli
    from .requirement_fixture import requirement_fixture
except ImportError:
    from paid_cli_guard import reject_paid_cli
    from requirement_fixture import requirement_fixture


COUNTEREXAMPLE = {"id": "R001", "verdict": "COUNTEREXAMPLE", "report": "反例の本文",
                  "reportPath": "/logs/R001/attempt-1/report.md", "sha256": "x"}


def failing_cleanup(owner):
    owner.stopping = True
    owner.failures.append("process group 4242 cleanup failure: denied")


@mock.patch("main.ProcessOwner.install_signals")
@mock.patch("main.ProcessOwner.restore_signals")
class RequirementRunOutcomeTests(unittest.TestCase):
    def setUp(self):
        self.paid_cli_guard = reject_paid_cli()
        self.paid_cli_guard.start()

    def tearDown(self):
        self.paid_cli_guard.stop()

    def run_execute(self, args, fake_main, launch_result, **patches):
        with mock.patch.object(runtime_main, "__file__", str(fake_main)), \
             mock.patch("main.launch", return_value=launch_result), \
             mock.patch.object(ProcessOwner, "stop_all", autospec=True,
                               side_effect=patches.get("stop_all", lambda owner: None)), \
             mock.patch("main.snapshot", **patches.get("snapshot", {"wraps": runtime_main.snapshot})), \
             mock.patch("sys.stderr"):
            return runtime_main.execute(args)

    def test_cleanup_failure_keeps_unit_verdict_and_fails_at_run_level(self, *_signals):
        with tempfile.TemporaryDirectory() as temp:
            args, fake_main = requirement_fixture(Path(temp))
            code = self.run_execute(args, fake_main, COUNTEREXAMPLE, stop_all=failing_cleanup)
            self.assertEqual(code, 2)
            results = json.loads(Path(args.run_dir, "results.json").read_text(encoding="utf-8"))
            self.assertEqual(results["results"], [COUNTEREXAMPLE])
            self.assertEqual(results["runnerFailures"], ["process group 4242 cleanup failure: denied"])
            summary = Path(args.run_dir, "summary.md").read_text(encoding="utf-8")
            self.assertIn("## R001: Critical", summary)
            self.assertIn("反例の本文", summary)
            self.assertIn("後始末失敗: ['process group 4242 cleanup failure: denied']", summary)

    def test_snapshot_failure_is_undetermined_and_invalidates_run_dir(self, *_signals):
        with tempfile.TemporaryDirectory() as temp:
            args, fake_main = requirement_fixture(Path(temp))
            # 開始時のsnapshotはbundle経由で成功し、終了時の比較だけが失敗する
            # The start snapshot runs inside bundle; only the end-of-run comparison fails
            failure = subprocess.CalledProcessError(128, "git")
            code = self.run_execute(args, fake_main, COUNTEREXAMPLE,
                                    snapshot={"side_effect": failure})
            self.assertEqual(code, 2)
            results = json.loads(Path(args.run_dir, "results.json").read_text(encoding="utf-8"))
            self.assertIsNone(results["codeChanged"])
            self.assertIn("128", results["codeChangeError"])
            summary = Path(args.run_dir, "summary.md").read_text(encoding="utf-8")
            self.assertIn("検査中のコード変化: 判定不能（snapshot failure:", summary)
            self.assertNotIn("検査中のコード変化: True", summary)
            self.assertTrue(Path(args.run_dir, "invalidated.json").is_file())
            with self.assertRaisesRegex(ValueError, "無効化済み"):
                self.run_execute(args, fake_main, COUNTEREXAMPLE)

    def test_changed_code_invalidates_run_dir_even_after_revert(self, *_signals):
        with tempfile.TemporaryDirectory() as temp:
            args, fake_main = requirement_fixture(Path(temp))
            tracked = Path(args.repo_root, "tracked")

            def edit_during_review(*_args):
                tracked.write_text("changed", encoding="utf-8")
                return COUNTEREXAMPLE

            with mock.patch.object(runtime_main, "__file__", str(fake_main)), \
                 mock.patch("main.launch", side_effect=edit_during_review), \
                 mock.patch.object(ProcessOwner, "stop_all", autospec=True), \
                 mock.patch("sys.stderr"):
                self.assertEqual(runtime_main.execute(args), 2)
            self.assertIs(json.loads(Path(args.run_dir, "results.json").read_text(
                encoding="utf-8"))["codeChanged"], True)
            tracked.write_text("base", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "無効化済み"):
                self.run_execute(args, fake_main, COUNTEREXAMPLE)

    def test_unchanged_run_leaves_run_dir_reusable(self, *_signals):
        with tempfile.TemporaryDirectory() as temp:
            args, fake_main = requirement_fixture(Path(temp))
            self.assertEqual(self.run_execute(args, fake_main, COUNTEREXAMPLE), 0)
            self.assertFalse(Path(args.run_dir, "invalidated.json").exists())
            results = json.loads(Path(args.run_dir, "results.json").read_text(encoding="utf-8"))
            self.assertIs(results["codeChanged"], False)
            self.assertIsNone(results["codeChangeError"])
            self.assertEqual(results["runnerFailures"], [])
            self.assertIs(results["stopRequested"], False)
            self.assertEqual(self.run_execute(args, fake_main, COUNTEREXAMPLE), 0)

    def test_missing_manifest_with_existing_attempt_is_rejected(self, *_signals):
        with tempfile.TemporaryDirectory() as temp:
            args, fake_main = requirement_fixture(Path(temp))
            Path(args.run_dir, "R001", "attempt-1").mkdir(parents=True)
            with self.assertRaisesRegex(ValueError, "manifestが無い既存attempt"):
                self.run_execute(args, fake_main, COUNTEREXAMPLE)
            self.assertFalse(Path(args.run_dir, "manifest.json").exists())


if __name__ == "__main__":
    unittest.main()
