"""Unit tests of the UITK no-Web gate (G4) on throwaway git repositories."""

import contextlib
import io
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from uitk_no_web.cli import main  # noqa: E402

ALLOWLIST = ".github/scripts/uitk_no_web/allowlist.txt"


class UitkNoWebGateTest(unittest.TestCase):
    def setUp(self):
        self._dir = tempfile.TemporaryDirectory()
        self.root = Path(self._dir.name)
        subprocess.run(["git", "init", "-q", str(self.root)], check=True)

    def tearDown(self):
        self._dir.cleanup()

    def write(self, path: str, text: str) -> None:
        target = self.root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8")
        subprocess.run(["git", "-C", str(self.root), "add", path], check=True)

    def run_gate(self, *extra: str) -> tuple:
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = main(["--root", str(self.root), *extra])
        return code, out.getvalue() + err.getvalue()

    def test_empty_repo_with_empty_allowlist_passes(self):
        self.write(ALLOWLIST, "# empty\n")
        self.assertEqual(0, self.run_gate()[0])

    def test_missing_allowlist_stops(self):
        self.write("a.cs", "class A {}\n")
        self.assertEqual(2, self.run_gate()[0])

    def test_token_outside_allowlist_fails_with_location(self):
        self.write(ALLOWLIST, "# empty\n")
        self.write("src/A.cs", "// WebUi の名残\n")
        code, output = self.run_gate()
        self.assertEqual(1, code)
        self.assertIn("src/A.cs:1: WebUi", output)

    def test_path_entry_allows_and_is_used(self):
        self.write(ALLOWLIST, "path docs/adr/* # 経緯\n")
        self.write("docs/adr/0001.md", "CefUnity を使う\n")
        self.assertEqual(0, self.run_gate()[0])

    def test_line_entry_allows_only_matching_line(self):
        self.write(ALLOWLIST, "line skill.md git\\ show # 撤去前の読み方\n")
        self.write("skill.md", "git show abc:moorestech_web/webui/x\nmoorestech_web の残り\n")
        code, output = self.run_gate()
        self.assertEqual(1, code)
        self.assertIn("skill.md:2:", output)

    def test_cef_inside_identifier_is_not_a_token(self):
        self.write(ALLOWLIST, "# empty\n")
        self.write("Mat.mat", "_Color: Vector1_5CEF3A\n")
        self.assertEqual(0, self.run_gate()[0])

    def test_cef_next_to_japanese_is_a_token(self):
        self.write(ALLOWLIST, "# empty\n")
        self.write("note.md", "CEF停止の記録\n")
        self.assertEqual(1, self.run_gate()[0])

    def test_file_name_with_token_fails(self):
        self.write(ALLOWLIST, "# empty\n")
        self.write("Client/WebUiThing.cs", "class Thing {}\n")
        code, output = self.run_gate()
        self.assertEqual(1, code)
        self.assertIn("Client/WebUiThing.cs:0:", output)

    def test_stale_entry_stops(self):
        self.write(ALLOWLIST, "path nowhere/* # 腐った行\n")
        self.write("a.cs", "class A {}\n")
        self.assertEqual(2, self.run_gate()[0])

    def test_entry_without_reason_stops(self):
        self.write(ALLOWLIST, "path docs/*\n")
        self.write("docs/a.md", "Kestrel\n")
        self.assertEqual(2, self.run_gate()[0])

    def test_spelled_variants_are_tokens(self):
        self.write(ALLOWLIST, "# empty\n")
        self.write("a.md", "Web UI の手順\n")
        self.write("b.md", "WebUI の手順\n")
        self.write("c.md", "scripts/cef-unity を取る\n")
        self.write("d.cs", "var webUiDeadline = 1;\n")
        self.write("e.sh", "export MOORESTECH_WEBUI_MODE=prod\n")
        self.write("f.md", "Webui root\n")
        code, output = self.run_gate()
        self.assertEqual(1, code)
        for expected in ("a.md:1: Web UI", "b.md:1: WebUI", "c.md:1: cef-unity", "d.cs:1: webUi", "e.sh:1: WEBUI", "f.md:1: Webui"):
            self.assertIn(expected, output)

    def test_hits_are_listed_even_with_stale_entry(self):
        self.write(ALLOWLIST, "path nowhere/* # 腐った行\n")
        self.write("src/A.cs", "// WebUi の名残\n")
        code, output = self.run_gate()
        self.assertEqual(2, code)
        self.assertIn("src/A.cs:1: WebUi", output)
        self.assertIn("何も許していない", output)

    def test_bad_regex_in_line_entry_stops_with_line_number(self):
        self.write(ALLOWLIST, "line a.md ( # 括弧が閉じていない\n")
        self.write("a.md", "WebUi\n")
        code, output = self.run_gate()
        self.assertEqual(2, code)
        self.assertIn("1: 正規表現が読めない", output)

    def test_git_failure_stops_even_in_report(self):
        outside = tempfile.TemporaryDirectory()
        self.addCleanup(outside.cleanup)
        allowlist = Path(outside.name) / ALLOWLIST
        allowlist.parent.mkdir(parents=True)
        allowlist.write_text("# empty\n", encoding="utf-8")
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            code = main(["--root", outside.name, "--report"])
        self.assertEqual(2, code)
        self.assertIn("G4: git", err.getvalue())


if __name__ == "__main__":
    unittest.main()
