# .claude/skills/moores-code-review/tests/test_external_repo_reference.py
# 外部repo直接参照チェック（moorestech-sm831 再発防止・2026-09-25ユーザー裁定）の回帰テスト
# Regression test for the external-repo reference check (moorestech-sm831 prevention)
#
# 実行: python3 -m unittest discover -s .claude/skills/moores-code-review/tests
import sys
import unittest
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parent.parent
REPO_ROOT = SKILL_DIR.parent.parent.parent
sys.path.insert(0, str(SKILL_DIR / "scripts"))

import checks_external_repo  # noqa: E402
from patch_util import parse_patch  # noqa: E402

CLIENT_TEST = "moorestech_client/Assets/Scripts/Client.Tests/UnitTest/MapPreview/PreviewTest.cs"
WEBUI = "moorestech_web/webui/e2e/mock-host/assets/demoAssets.ts"


def _patch(body: str, path: str = CLIENT_TEST) -> str:
    return (
        f"diff --git a/{path} b/{path}\n"
        f"--- a/{path}\n"
        f"+++ b/{path}\n"
        "@@ -1,0 +1,20 @@\n"
    ) + "".join(f"+{line}\n" for line in body.strip("\n").splitlines())


def _lines(body: str, path: str = CLIENT_TEST) -> list[int]:
    return [f["line"] for f in checks_external_repo.run(parse_patch(_patch(body, path)))]


class ExternalRepoReferenceTest(unittest.TestCase):
    def test_personal_assets_path_in_test_is_confirmed(self):
        body = 'var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PersonalAssets/moorestech-client-private/BK/Tree.prefab");'
        self.assertEqual([1], _lines(body))

    def test_master_repo_path_is_confirmed(self):
        body = 'var root = Path.Combine(Environment.CurrentDirectory, "../../moorestech_master");'
        self.assertEqual([1], _lines(body))

    def test_ts_reference_is_confirmed(self):
        body = 'const dir = resolve(process.cwd(), "../../../moorestech_master/server_v8/mods");'
        self.assertEqual([1], _lines(body, WEBUI))

    def test_comments_are_clean(self):
        body = ("// PersonalAssetsはCIに無いので公開フィクスチャを使う\n"
                "/// moorestech_master を直接読まない\n"
                " * moorestech-client-private の説明\n"
                "var x = 1; // moorestech_master")
        self.assertEqual([], _lines(body))

    def test_hash_comment_is_clean_but_code_is_confirmed(self):
        body = "# moorestech_master の説明\nMASTER = '../moorestech_master'"
        self.assertEqual([2], _lines(body, "scripts/tools/foo.py"))

    def test_allowlisted_pipeline_is_clean(self):
        body = 'var d = Path.Combine(Environment.CurrentDirectory, "../../moorestech_master/server_v8/");'
        self.assertEqual([], _lines(body, "moorestech_server/Assets/Scripts/Server.Boot/ServerDirectory.cs"))
        self.assertEqual([], _lines("path: moorestech_master", ".github/workflows/run_test.yml"))

    def test_docs_skills_and_unity_yaml_are_clean(self):
        body = "moorestech_master と PersonalAssets"
        for path in ("docs/development/x.md", ".agents/skills/foo/scripts/bar.py",
                     "moorestech_client/Assets/Foo.prefab", "README.md"):
            self.assertEqual([], _lines(body, path), path)

    def test_removed_lines_are_ignored(self):
        path = CLIENT_TEST
        patch = (f"diff --git a/{path} b/{path}\n--- a/{path}\n+++ b/{path}\n"
                 "@@ -1,1 +1,0 @@\n"
                 '-var r = "../moorestech_master";\n')
        self.assertEqual([], checks_external_repo.run(parse_patch(patch)))


class ExternalRepoAllowlistWiringTest(unittest.TestCase):
    def test_every_allowlist_entry_has_reason_and_exists(self):
        # 理由の無い除外・実在しないパスの除外は、黙って検査を無効化する穴になる
        # Reasonless or dangling exemptions silently disable the check
        for entry in checks_external_repo.load_allowlist():
            self.assertTrue(entry.get("reason"), f"{entry} に reason が無い")
            self.assertTrue((REPO_ROOT / entry["path"]).exists(), f"{entry['path']} が実在しない")

    def test_wired_into_deterministic_checks_and_docs(self):
        source = (SKILL_DIR / "scripts/deterministic_checks.py").read_text(encoding="utf-8")
        self.assertIn("checks_external_repo.run(files)", source)
        steps = (SKILL_DIR / "references/orchestrator-steps.md").read_text(encoding="utf-8")
        self.assertIn("external_repo_reference", steps)
        self.assertIn("external_repo_reference_allowlist.json", steps)


if __name__ == "__main__":
    unittest.main()
