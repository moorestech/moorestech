# .claude/skills/moores-code-review/tests/test_refix_snapshot.py
# refix_snapshot.py の実測テスト: 作業ツリーに触れず反映 diff と scope を作れること。
# Tests for refix_snapshot.py: builds the interdiff and scope without touching the worktree.
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parents[1]
SCRIPT = SKILL_DIR / "scripts" / "refix_snapshot.py"


def sh(cwd, *args):
    return subprocess.run(args, cwd=cwd, capture_output=True, text=True, check=True).stdout.strip()


def run_script(*args):
    return subprocess.run([sys.executable, str(SCRIPT), *args], capture_output=True, text=True)


class RefixSnapshotTest(unittest.TestCase):
    def setUp(self):
        self.td = tempfile.TemporaryDirectory()
        self.repo = Path(self.td.name) / "repo"
        self.run_dir = Path(self.td.name) / "run"
        self.repo.mkdir()
        sh(self.repo, "git", "init", "-q")
        sh(self.repo, "git", "config", "user.email", "t@example.com")
        sh(self.repo, "git", "config", "user.name", "t")
        (self.repo / "src").mkdir()
        (self.repo / "src" / "Foo.cs").write_text("public class Foo { int a = 1; }\n// note\n", encoding="utf-8")
        (self.repo / "Tests").mkdir()
        (self.repo / "Tests" / "FooTest.cs").write_text("[Test] public void A() {}\n", encoding="utf-8")
        (self.repo / ".gitignore").write_text("ignored.log\n", encoding="utf-8")
        sh(self.repo, "git", "add", "-A")
        sh(self.repo, "git", "commit", "-q", "-m", "init")
        # レビュー対象の未コミット変更（未追跡ファイル込み）を作る / uncommitted work under review, untracked included
        (self.repo / "src" / "Foo.cs").write_text("public class Foo { int a = 2; }\n// note\n", encoding="utf-8")
        (self.repo / "src" / "New.cs").write_text("public class New {}\n", encoding="utf-8")
        (self.repo / "ignored.log").write_text("x\n", encoding="utf-8")

    def tearDown(self):
        self.td.cleanup()

    def _snap(self, name):
        run = run_script("snapshot", "--repo-root", str(self.repo), "--run-dir", str(self.run_dir), "--name", name)
        self.assertEqual(run.returncode, 0, run.stderr)
        return json.loads(run.stdout)["sha"]

    def _diff(self, src, dst, out="round.diff"):
        run = run_script("diff", "--repo-root", str(self.repo), "--run-dir", str(self.run_dir),
                         "--from", src, "--to", dst, "--out", str(self.run_dir / out))
        self.assertEqual(run.returncode, 0, run.stderr)
        return json.loads(run.stdout)

    def test_snapshot_leaves_head_index_and_worktree_untouched(self):
        head = sh(self.repo, "git", "rev-parse", "HEAD")
        status = sh(self.repo, "git", "status", "--porcelain")
        sha = self._snap("s0")
        self.assertNotEqual(sha, head)
        self.assertEqual(sh(self.repo, "git", "rev-parse", "HEAD"), head, "HEAD が動いた")
        self.assertEqual(sh(self.repo, "git", "status", "--porcelain"), status, "index/作業ツリーが変わった")
        self.assertFalse((self.run_dir / "refix" / "s0.index").exists(), "一時 index が残っている")
        # snapshot には未追跡の New.cs が入り、ignored.log は入らない / untracked in, ignored out
        listed = sh(self.repo, "git", "ls-tree", "-r", "--name-only", sha).splitlines()
        self.assertIn("src/New.cs", listed)
        self.assertNotIn("ignored.log", listed)

    def test_source_change_is_scope_source(self):
        self._snap("s0")
        (self.repo / "src" / "Foo.cs").write_text("public class Foo { int a = 3; }\n// note\n", encoding="utf-8")
        self._snap("s1")
        result = self._diff("s0", "s1")
        self.assertEqual(result["scope"], "source")
        self.assertEqual(result["source_files"], ["src/Foo.cs"])
        self.assertIn("-public class Foo { int a = 2; }", (self.run_dir / "round.diff").read_text(encoding="utf-8"))

    def test_untracked_file_created_by_fix_is_included(self):
        self._snap("s0")
        (self.repo / "src" / "Bar.cs").write_text("public class Bar {}\n", encoding="utf-8")
        self._snap("s1")
        result = self._diff("s0", "s1")
        self.assertEqual(result["scope"], "source")
        self.assertEqual(result["files"], ["src/Bar.cs"])

    def test_comment_only_and_test_only_changes_are_non_source(self):
        # moorestech の大文字 `Tests/` ディレクトリと `*Test.cs` 名をテストとして扱う
        # Capitalized `Tests/` directories and `*Test.cs` names count as tests in moorestech
        self._snap("s0")
        (self.repo / "src" / "Foo.cs").write_text("public class Foo { int a = 2; }\n// note (reworded)\n", encoding="utf-8")
        (self.repo / "Tests" / "FooTest.cs").write_text("[Test] public void A() { Assert.Pass(); }\n", encoding="utf-8")
        (self.repo / "README.md").write_text("# doc\n", encoding="utf-8")
        self._snap("s1")
        result = self._diff("s0", "s1")
        self.assertEqual(result["scope"], "non-source", result)
        self.assertEqual(sorted(result["files"]), ["README.md", "Tests/FooTest.cs", "src/Foo.cs"])
        self.assertEqual(result["source_files"], [])

    def test_no_change_is_scope_none(self):
        self._snap("s0")
        self._snap("s1")
        result = self._diff("s0", "s1")
        self.assertEqual(result["scope"], "none")
        self.assertEqual((self.run_dir / "round.diff").read_text(encoding="utf-8"), "")

    def test_diff_without_snapshot_fails_closed(self):
        run = run_script("diff", "--repo-root", str(self.repo), "--run-dir", str(self.run_dir),
                         "--from", "s0", "--to", "s1", "--out", str(self.run_dir / "r.diff"))
        self.assertEqual(run.returncode, 3)
        self.assertIn("snapshot が無い", run.stderr)


if __name__ == "__main__":
    unittest.main()
