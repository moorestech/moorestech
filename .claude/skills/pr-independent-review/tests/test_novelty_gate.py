# .claude/skills/pr-independent-review/tests/test_novelty_gate.py
# 一時gitリポジトリを組み立ててnovelty_gate.pyの検出を検証する
# Build a throwaway git repo and verify novelty_gate.py detections
import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "novelty_gate.py"


def _git(repo: Path, *args: str) -> str:
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        check=True, capture_output=True, text=True,
    ).stdout


@pytest.fixture()
def repo(tmp_path: Path) -> Path:
    r = tmp_path / "repo"
    r.mkdir()
    _git(r, "init", "-b", "master")
    _git(r, "config", "user.email", "t@t")
    _git(r, "config", "user.name", "t")
    # base: 具体ドメイン側は既にElectricWireをusing済み / domain side already uses ElectricWire
    wire_dir = r / "Client.Game" / "ElectricWire"
    wire_dir.mkdir(parents=True)
    (wire_dir / "WireView.cs").write_text(
        "using Game.ElectricWire;\nnamespace Client.Game.ElectricWire { class WireView {} }\n"
    )
    common_dir = r / "Client.Game" / "BlockSystem" / "PlaceSystem" / "Common"
    common_dir.mkdir(parents=True)
    (common_dir / "CommonBlockPlaceSystem.cs").write_text(
        "using UnityEngine;\nnamespace Client.Game.BlockSystem.PlaceSystem { class CommonBlockPlaceSystem {} }\n"
    )
    _git(r, "add", "-A")
    _git(r, "commit", "-m", "base")
    # base_refはタグで固定する（masterのままだとHEADと同一でdiffが常に空になり空虚な合格を生む）
    # Pin base as a tag; using master would make base...HEAD always empty
    _git(r, "tag", "basetag")
    return r


def _run(repo: Path) -> dict:
    out = subprocess.run(
        [sys.executable, str(SCRIPT), str(repo), "basetag"],
        check=True, capture_output=True, text=True,
    ).stdout
    return json.loads(out)


def test_new_using_edge_from_generic_dir_is_flagged(repo: Path):
    # 汎用Common配下がドメインnamespaceを初めてusing → new_edge / generic Common dir gains first domain using
    f = repo / "Client.Game" / "BlockSystem" / "PlaceSystem" / "Common" / "CommonBlockPlaceSystem.cs"
    f.write_text(
        "using UnityEngine;\nusing Game.ElectricWire;\n"
        "namespace Client.Game.BlockSystem.PlaceSystem { class CommonBlockPlaceSystem {} }\n"
    )
    _git(repo, "commit", "-am", "add wire dep")
    result = _run(repo)
    edges = [e for e in result["new_edges"] if e["using"] == "Game.ElectricWire"]
    assert len(edges) == 1
    assert edges[0]["generic_origin"] is True


def test_existing_pair_is_not_flagged(repo: Path):
    # 既存ペア（ElectricWireディレクトリ内のGame.ElectricWire）は新エッジではない / pre-existing pair is not novel
    f = repo / "Client.Game" / "ElectricWire" / "WireView2.cs"
    f.write_text("using Game.ElectricWire;\nnamespace Client.Game.ElectricWire { class WireView2 {} }\n")
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "sibling file")
    result = _run(repo)
    assert result["new_edges"] == []


def test_grammar_elements_detected(repo: Path):
    # interface新設・Subject新設・スキーマyml変更を検出 / detect new interface, Subject, schema yml change
    f = repo / "Client.Game" / "ElectricWire" / "IWirePreview.cs"
    f.write_text(
        "using UniRx;\nnamespace Client.Game.ElectricWire {\n"
        "public interface IWirePreview {}\n"
        "class Impl { private readonly Subject<int> _onChanged = new(); }\n}\n"
    )
    schema = repo / "VanillaSchema" / "blocks.yml"
    schema.parent.mkdir()
    schema.write_text("key: value\n")
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "grammar")
    result = _run(repo)
    kinds = {g["kind"] for g in result["grammar"]}
    assert "interface" in kinds
    assert "subject" in kinds
    assert "schema_change" in kinds


def test_asmdef_reference_addition_detected(repo: Path):
    # 実際のasmdefは複数行配列。追加refは裸の文字列行として現れ、key行(`":`含む)は無視される
    # Real asmdefs use multi-line arrays; added refs appear as bare string lines
    asmdef = repo / "Client.Game" / "Client.Game.asmdef"
    asmdef.write_text('{\n  "name": "Client.Game",\n  "references": [\n  ]\n}\n')
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "asmdef base")
    asmdef.write_text('{\n  "name": "Client.Game",\n  "references": [\n    "Game.ElectricWire"\n  ]\n}\n')
    _git(repo, "commit", "-am", "asmdef ref")
    result = _run(repo)
    assert {"file": "Client.Game/Client.Game.asmdef", "ref": "Game.ElectricWire"} in result["asmdef_refs"]
    # key行の値("Client.Game"等)が誤検知されていないこと / key-line values must not be false positives
    assert all(r["ref"] != "Client.Game" for r in result["asmdef_refs"])
