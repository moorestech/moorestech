"""review_diff.py の除外が実際の git diff で効くことを一時リポジトリで確かめる。
Verify review_diff.py exclusions against a real git diff in a temporary repository."""
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "review_diff.py"

KEPT = [
    "moorestech_client/Assets/Scripts/Client.Game/Foo.cs",
    "moorestech_client/Assets/Dependencies/StarterAssets/Scripts/PlayerInput.cs",
    "moorestech_client/Assets/packages.config",
    "moorestech_web/webui/src/app/Panel.tsx",
    "docs/adr/0001-sample.md",
]
EXCLUDED = [
    "moorestech_client/Assets/Packages/Microsoft.CodeAnalysis.Common.4.14.0/lib/netstandard2.0/Microsoft.CodeAnalysis.xml",
    "moorestech_client/Assets/Packages/Lib.Harmony.2.4.2/lib/net48/0Harmony.dll",
    "moorestech_client/Packages/packages-lock.json",
    "moorestech_web/webui/pnpm-lock.yaml",
    "moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts",
    "moorestech_client/Assets/Scripts/Client.Tests/unity-playmode-recorded-playtest/Scenario.cs",
    "moorestech_client/Assets/Scripts/Client.Game/Foo.cs.meta",
    "moorestech_client/Assets/Asset/Block/Belt.prefab",
    "moorestech_client/Assets/Asset/Block/Belt.anim",
    "moorestech_client/Assets/Asset/Block/Belt.PNG",
    "tools/mac/mooreseditor.app/Contents/MacOS/mooreseditor",
    "tools/windows/mooreseditor.exe",
]


def _git(root: Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(root), *args], capture_output=True, text=True, check=True).stdout


class ReviewDiffTest(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = Path(self._tmp.name)
        _git(self.root, "init", "-q")
        _git(self.root, "config", "user.email", "t@example.com")
        _git(self.root, "config", "user.name", "t")
        (self.root / "README.md").write_text("base\n", encoding="utf-8")
        _git(self.root, "add", "-A")
        _git(self.root, "commit", "-q", "-m", "base")

    def tearDown(self):
        self._tmp.cleanup()

    def _write_all(self):
        for rel in KEPT + EXCLUDED:
            path = self.root / rel
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(f"content of {rel}\n", encoding="utf-8")

    def _review_diff(self, *args: str) -> str:
        return subprocess.run([sys.executable, str(SCRIPT), *args], cwd=self.root,
                              capture_output=True, text=True, check=True).stdout

    def test_only_reviewable_files_reach_the_patch(self):
        self._write_all()
        _git(self.root, "add", "-A")
        patch = self._review_diff("--cached")
        for rel in KEPT:
            self.assertIn(f"diff --git a/{rel} b/{rel}", patch, f"{rel} が patch から落ちている")
        for rel in EXCLUDED:
            self.assertNotIn(rel, patch, f"{rel} が patch に入っている")

    def test_arguments_pass_through_to_git_diff(self):
        # コミット範囲指定もそのまま git diff へ渡る
        # Commit-range arguments pass straight through to git diff
        self._write_all()
        _git(self.root, "add", "-A")
        _git(self.root, "commit", "-q", "-m", "change")
        patch = self._review_diff("HEAD^..HEAD")
        self.assertIn("Client.Game/Foo.cs b/", patch)
        self.assertNotIn("Assets/Packages/", patch)


if __name__ == "__main__":
    unittest.main()
