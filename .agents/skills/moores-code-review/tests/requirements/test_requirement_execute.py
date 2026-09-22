import argparse
import fcntl
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


class RequirementExecuteTests(unittest.TestCase):
    def fixture(self, root):
        source = root / "source"
        source.mkdir()
        subprocess.run(["git", "init", "-q", str(source)], check=True)
        subprocess.run(["git", "-C", str(source), "config", "user.email", "test@example.com"], check=True)
        subprocess.run(["git", "-C", str(source), "config", "user.name", "Test"], check=True)
        (source / "tracked").write_text("base", encoding="utf-8")
        subprocess.run(["git", "-C", str(source), "add", "tracked"], check=True)
        subprocess.run(["git", "-C", str(source), "commit", "-qm", "initial"], check=True)
        context, patch = root / "context.md", root / "patch.diff"
        context.write_text("request", encoding="utf-8")
        patch.write_text("patch", encoding="utf-8")
        tooling = root / "tool" / "scripts" / "requirements"
        tooling.mkdir(parents=True)
        reference = tooling.parents[1] / "references" / "requirement-proof.md"
        reference.parent.mkdir()
        reference.write_text("procedure", encoding="utf-8")
        args = argparse.Namespace(repo_root=str(source), context=str(context), patch=str(patch),
                                  run_dir=str(root / "run"), model="sonnet")
        return args, tooling / "main.py"

    @mock.patch("main.ProcessOwner.install_signals")
    @mock.patch("main.ProcessOwner.restore_signals")
    @mock.patch("main.ProcessOwner.stop_all")
    def test_execute_success_missing_and_changed_snapshot(self, _stop, _restore, _install):
        with tempfile.TemporaryDirectory() as temp:
            args, fake_main = self.fixture(Path(temp))
            report = {"id": "R001", "verdict": "SUPPORTED", "report": "ok", "sha256": "x"}
            with mock.patch.object(runtime_main, "__file__", str(fake_main)), \
                 mock.patch("main.launch", return_value=report):
                self.assertEqual(runtime_main.execute(args), 0)
            Path(args.run_dir, "results.json").unlink()
            Path(args.run_dir, "summary.md").unlink()
            with mock.patch.object(runtime_main, "__file__", str(fake_main)), \
                 mock.patch("main.launch", return_value={"id": "R001", "verdict": "MISSING", "reason": "failed"}):
                self.assertEqual(runtime_main.execute(args), 2)
            with mock.patch.object(runtime_main, "__file__", str(fake_main)), \
                 mock.patch("main.launch", return_value=report), \
                 mock.patch("main.snapshot", return_value="changed"):
                self.assertEqual(runtime_main.execute(args), 2)

    def test_corrupt_changed_and_concurrently_locked_manifest_fail(self):
        with tempfile.TemporaryDirectory() as temp:
            args, fake_main = self.fixture(Path(temp))
            run = Path(args.run_dir)
            run.mkdir()
            (run / "manifest.json").write_text("{broken", encoding="utf-8")
            with mock.patch.object(runtime_main, "__file__", str(fake_main)):
                with self.assertRaisesRegex(ValueError, "manifest"):
                    runtime_main.execute(args)
            (run / "manifest.json").write_text(json.dumps({"different": True}), encoding="utf-8")
            with mock.patch.object(runtime_main, "__file__", str(fake_main)):
                with self.assertRaisesRegex(ValueError, "model"):
                    runtime_main.execute(args)
            lock = (run / "run.lock").open("a")
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            with mock.patch.object(runtime_main, "__file__", str(fake_main)), self.assertRaises(BlockingIOError):
                runtime_main.execute(args)
            lock.close()

    def test_run_directory_cannot_share_procedure_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            args, fake_main = self.fixture(Path(temp))
            args.run_dir = str(fake_main.parents[2] / "references")
            with mock.patch.object(runtime_main, "__file__", str(fake_main)):
                with self.assertRaisesRegex(ValueError, "run-dir"):
                    runtime_main.execute(args)

    def test_external_git_failure_returns_cli_failure(self):
        with tempfile.TemporaryDirectory() as temp:
            args, fake_main = self.fixture(Path(temp))
            with mock.patch.object(runtime_main, "__file__", str(fake_main)), \
                 mock.patch("input_bundle.subprocess.check_output",
                            side_effect=subprocess.CalledProcessError(1, "git")):
                with mock.patch.object(sys, "argv", ["main.py", "--repo-root", args.repo_root,
                                      "--context", args.context, "--patch", args.patch,
                                      "--run-dir", args.run_dir, "--model", "sonnet"]):
                    self.assertEqual(runtime_main.main(), 2)


if __name__ == "__main__":
    unittest.main()
