# files先頭の拡張子からhljs言語名を決める写像を検証する
# Verify the mapping from the first file's extension to an hljs language name
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.code_card.lang import language_of


@pytest.mark.parametrize("path,want", [
    ("moorestech_server/Assets/Scripts/Game.Block/Foo.cs:40", "csharp"),
    ("moorestech_web/webui/src/features/detailLogic.ts:73", "typescript"),
    ("moorestech_web/webui/src/ui/MachineSection.tsx:18", "typescript"),
    ("moorestech_web/webui/src/ui/style.module.css:4", "css"),
    ("moorestech_master/server_v8/mods/blocks.json:1", "json"),
    ("VanillaSchema/blocks.yml:3", "yaml"),
    ("docs/adr/0018-foo.md:1", "markdown"),
    ("moorestech_client/Assets/Foo.asmdef:1", "json"),
    ("scripts/run.sh:2", "bash"),
])
def test_known_extensions(path, want):
    assert language_of([path]) == want


def test_unknown_extension_is_empty():
    assert language_of(["build/output.bin:1"]) == ""


def test_no_extension_is_empty():
    assert language_of(["Makefile:1"]) == ""


def test_first_file_decides():
    assert language_of(["a/b.cs:1", "c/d.ts:2"]) == "csharp"
