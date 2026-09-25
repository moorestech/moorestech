import runpy
import subprocess
import tempfile
import unittest
from pathlib import Path


CHECKER = runpy.run_path(str(Path(__file__).resolve().parents[1] / "check-test-external-dependencies.py"))


class ExternalDependencyCheckTest(unittest.TestCase):
    def setUp(self):
        self.workspace = tempfile.TemporaryDirectory()
        self.root = Path(self.workspace.name)
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)

    def tearDown(self):
        self.workspace.cleanup()

    def write(self, path, content):
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8")
        subprocess.run(["git", "add", path], cwd=self.root, check=True)

    def test_reports_master_checkout_and_missing_prefab_parent(self):
        self.write(
            "moorestech_client/Assets/Scripts/Client.Tests/ExampleTest.cs",
            'class ExampleTest {\n'
            '  // PinnedMasterRepository in a comment is harmless\n'
            '  void Run() { ServerDirectory.GetDirectory(); }\n'
            '  const string Address = "Tests/PrivatePrefab";\n'
            '  EditorTerrainAssetLoader loader;\n'
            '}\n',
        )
        self.write(
            "moorestech_client/Assets/AddressableAssetsData/AssetGroups/Test.asset",
            "  - m_GUID: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n    m_Address: Tests/PrivatePrefab\n",
        )
        self.write(
            "moorestech_client/Assets/AddressableResources/Private.prefab.meta",
            "fileFormatVersion: 2\nguid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n",
        )
        self.write(
            "moorestech_client/Assets/AddressableResources/Private.prefab",
            "  m_SourcePrefab: {fileID: 100100000, guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, type: 3}\n",
        )

        found = CHECKER["violations"](self.root)

        self.assertEqual([(3, "sibling master data directory"), (4, "Addressable prefab Tests/PrivatePrefab has an untracked source prefab")], [(line, reason) for _, line, reason in found])

    def test_owned_fake_pin_and_comments_do_not_fail(self):
        self.write(
            "moorestech_client/Assets/Scripts/Client.Tests/ExampleTest.cs",
            'class ExampleTest {\n'
            '  // moorestech-client-private is absent in CI\n'
            '  const string FakePin = "../fixture_master";\n'
            '}\n',
        )

        self.assertEqual([], CHECKER["violations"](self.root))

    def test_reports_explicit_private_and_sibling_paths(self):
        self.write(
            "moorestech_web/webui/e2e/example.test.ts",
            'const client = "Assets/PersonalAssets/moorestech-client-private";\n'
            'const master = "../moorestech_master/server_v8";\n',
        )

        self.assertEqual(
            [(1, "private client asset repository"), (2, "sibling master repository path")],
            [(line, reason) for _, line, reason in CHECKER["violations"](self.root)],
        )

    def test_reports_constructed_master_path_and_address_in_separate_loader(self):
        self.write(
            "moorestech_client/Assets/Scripts/Client.Tests/ExampleTest.cs",
            'var master = Path.Combine(root, "..", "moorestech_master");\n'
            'const string address = "Tests/PrivatePrefab";\n',
        )
        self.write(
            "moorestech_client/Assets/AddressableAssetsData/AssetGroups/Test.asset",
            "  - m_GUID: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n    m_Address: Tests/PrivatePrefab\n",
        )
        self.write(
            "moorestech_client/Assets/AddressableResources/Private.prefab.meta",
            "fileFormatVersion: 2\nguid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n",
        )
        self.write(
            "moorestech_client/Assets/AddressableResources/Private.prefab",
            "  m_SourcePrefab: {fileID: 100100000, guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, type: 3}\n",
        )

        self.assertEqual(
            [(1, "constructed sibling master repository path"), (2, "Addressable prefab Tests/PrivatePrefab has an untracked source prefab")],
            [(line, reason) for _, line, reason in CHECKER["violations"](self.root)],
        )


if __name__ == "__main__":
    unittest.main()
