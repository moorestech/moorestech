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


def test_new_file_under_datastore_directory_is_flagged(repo: Path):
    # ファイル名にdatastoreを含まなくてもDataStoreディレクトリ配下の新規.csは新設扱い（PR実測の正しい挙動）
    # A new .cs under a DataStore directory counts even when its file name lacks "datastore"
    store_dir = repo / "Server.Core" / "DataStore"
    store_dir.mkdir(parents=True)
    (store_dir / "FooService.cs").write_text(
        "namespace Server.Core.DataStore { class FooService {} }\n"
    )
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "datastore dir file")
    result = _run(repo)
    stores = [g for g in result["grammar"] if g["kind"] == "new_datastore_file"]
    assert [g["file"] for g in stores] == ["Server.Core/DataStore/FooService.cs"]


def test_asmdef_reference_addition_detected(repo: Path):
    # asmdefはdiff行ではなくbase/HEADのJSONを読んで references の集合差を取る
    # asmdef refs come from the JSON at base vs HEAD, not from diff lines
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


def test_asmdef_guid_style_reference_detected(repo: Path):
    # GUID形式の参照(`"GUID:abc123def"`)も文字列のまま報告される / GUID-style refs are reported verbatim
    asmdef = repo / "Client.Game" / "Client.Game.asmdef"
    asmdef.write_text('{\n  "name": "Client.Game",\n  "references": [\n  ]\n}\n')
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "asmdef base")
    asmdef.write_text('{\n  "name": "Client.Game",\n  "references": [\n    "GUID:abc123def"\n  ]\n}\n')
    _git(repo, "commit", "-am", "asmdef guid ref")
    result = _run(repo)
    assert {"file": "Client.Game/Client.Game.asmdef", "ref": "GUID:abc123def"} in result["asmdef_refs"]


def test_asmdef_new_file_reports_all_references(repo: Path):
    # 新規asmdefはbase側を空集合として扱い、references全件が追加分になる（1行形式も同じ）
    # A brand-new asmdef has an empty base set, so all its references count as additions
    asmdef = repo / "Client.Game" / "Client.Game.OneLine.asmdef"
    asmdef.write_text('{"name": "Client.Game", "references": ["Game.ElectricWire"]}\n')
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "one-line asmdef")
    result = _run(repo)
    assert {"file": "Client.Game/Client.Game.OneLine.asmdef", "ref": "Game.ElectricWire"} in result["asmdef_refs"]
    # name等のkey値はJSONのreferences配列外なので原理的に混入しない / values outside references cannot leak in
    assert all(r["ref"] != "Client.Game" for r in result["asmdef_refs"])


def test_asmdef_other_keys_are_never_treated_as_references(repo: Path):
    # references以外のkey（name / includePlatforms）の値はrefにならない / other keys never become refs
    asmdef = repo / "Client.Game" / "Client.Game.Tail.asmdef"
    asmdef.write_text('{"references": ["Game.Foo"], "name": "A", "includePlatforms": ["Editor"]}\n')
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "one-line asmdef with trailing keys")
    result = _run(repo)
    refs = [r["ref"] for r in result["asmdef_refs"] if r["file"] == "Client.Game/Client.Game.Tail.asmdef"]
    assert refs == ["Game.Foo"]


def test_asmdef_include_platforms_only_change_yields_no_refs(repo: Path):
    # includePlatformsへ1行追加しただけのPRではasmdef_refsが空（行パース時代の誤検知の回帰テスト）
    # A PR that only appends to includePlatforms must yield zero asmdef refs
    asmdef = repo / "Client.Game" / "Client.Game.Platform.asmdef"
    asmdef.write_text(
        '{\n  "name": "Client.Game",\n  "references": [\n    "Game.Existing"\n  ],\n'
        '  "includePlatforms": [\n  ]\n}\n'
    )
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "asmdef base")
    _git(repo, "tag", "-f", "basetag")
    asmdef.write_text(
        '{\n  "name": "Client.Game",\n  "references": [\n    "Game.Existing"\n  ],\n'
        '  "includePlatforms": [\n    "Editor"\n  ]\n}\n'
    )
    _git(repo, "commit", "-am", "add platform only")
    result = _run(repo)
    assert result["asmdef_refs"] == []


def test_asmdef_multiline_other_array_elements_are_not_refs(repo: Path):
    # 複数行asmdefで includePlatforms の要素("Editor")がrefに混入しないこと
    # In a multi-line asmdef, includePlatforms elements must not leak into references
    asmdef = repo / "Client.Game" / "Client.Game.Multi.asmdef"
    asmdef.write_text('{\n  "name": "Client.Game",\n  "references": [],\n  "includePlatforms": []\n}\n')
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "asmdef base")
    asmdef.write_text(
        '{\n  "name": "Client.Game",\n  "references": [\n    "Game.ElectricWire"\n  ],\n'
        '  "includePlatforms": [\n    "Editor"\n  ]\n}\n'
    )
    _git(repo, "commit", "-am", "asmdef ref and platform")
    result = _run(repo)
    refs = [r["ref"] for r in result["asmdef_refs"] if r["file"] == "Client.Game/Client.Game.Multi.asmdef"]
    assert refs == ["Game.ElectricWire"]


def test_path_with_space_is_attributed_to_its_own_file(repo: Path):
    # スペース入りパスではgitがヘッダ末尾にTABを付ける。除去しないと拡張子判定が全て外れ偽クリーンになる
    # git appends a TAB to headers of paths with spaces; without stripping it every check silently misses
    protocol_dir = repo / "Third Party" / "Protocol"
    protocol_dir.mkdir(parents=True)
    packet = protocol_dir / "NewPacket.cs"
    packet.write_text(
        "using Game.NewPacketDomain;\nnamespace ThirdParty.Protocol { public interface INewPacket {} }\n"
    )
    # diffで先行するASCIIファイルも同時に変更し、誤帰属先になり得る状態を作る
    # Also touch a preceding ASCII file so misattribution would have somewhere to land
    wire = repo / "Client.Game" / "ElectricWire" / "WireView.cs"
    wire.write_text(
        "using Game.ElectricWire;\nusing UnityEngine;\nnamespace Client.Game.ElectricWire { class WireView {} }\n"
    )
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "spaced path protocol file")
    result = _run(repo)

    protocol = [g for g in result["grammar"] if g["kind"] == "new_protocol_file"]
    assert [g["file"] for g in protocol] == ["Third Party/Protocol/NewPacket.cs"]
    interfaces = [g for g in result["grammar"] if g["kind"] == "interface"]
    assert [g["file"] for g in interfaces] == ["Third Party/Protocol/NewPacket.cs"]
    edges = [e for e in result["new_edges"] if e["using"] == "Game.NewPacketDomain"]
    assert [e["file"] for e in edges] == ["Third Party/Protocol/NewPacket.cs"]
    assert all(g["file"] != "Client.Game/ElectricWire/WireView.cs" for g in result["grammar"])


def test_non_ascii_path_is_attributed_to_its_own_file(repo: Path):
    # core.quotepathがonだと`+++ "b/..."`となり前ファイルへ誤帰属する。設定を明示して再現条件を固定
    # With core.quotepath on, `+++ "b/..."` would misattribute lines to the previous file
    _git(repo, "config", "core.quotepath", "true")
    protocol_dir = repo / "Client.Game" / "Protocol"
    protocol_dir.mkdir(parents=True)
    packet = protocol_dir / "新規パケット.cs"
    packet.write_text(
        "using Game.NewPacketDomain;\nnamespace Client.Game.Protocol { class NewPacket {} }\n"
    )
    # diffで先行するASCIIファイルも同時に変更し、誤帰属先になり得る状態を作る
    # Also touch a preceding ASCII file so misattribution would have somewhere to land
    wire = repo / "Client.Game" / "ElectricWire" / "WireView.cs"
    wire.write_text(
        "using Game.ElectricWire;\nusing UnityEngine;\nnamespace Client.Game.ElectricWire { class WireView {} }\n"
    )
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "non-ascii protocol file")
    result = _run(repo)

    protocol = [g for g in result["grammar"] if g["kind"] == "new_protocol_file"]
    assert [g["file"] for g in protocol] == ["Client.Game/Protocol/新規パケット.cs"]
    edges = [e for e in result["new_edges"] if e["using"] == "Game.NewPacketDomain"]
    assert [e["file"] for e in edges] == ["Client.Game/Protocol/新規パケット.cs"]
    # 先行ASCIIファイルが新規ファイル扱いされていないこと / the preceding file must not be seen as new
    assert all(g["file"] != "Client.Game/ElectricWire/WireView.cs" for g in result["grammar"])


def test_forced_diff_color_does_not_silence_detection(repo: Path):
    # color.diff=always のANSIで全行が不一致になり「空JSON+exit 0」の偽クリーンになる回帰
    # Forced ANSI color would make every line miss, yielding a false-clean empty result
    _git(repo, "config", "color.diff", "always")
    _git(repo, "config", "color.grep", "always")
    f = repo / "Client.Game" / "BlockSystem" / "PlaceSystem" / "Common" / "CommonBlockPlaceSystem.cs"
    f.write_text(
        "using UnityEngine;\nusing Game.ElectricWire;\n"
        "namespace Client.Game.BlockSystem.PlaceSystem { class CommonBlockPlaceSystem {} }\n"
    )
    # base側インベントリ(git grep)も色で壊れないことを見るため、既存ペアの非検出も同時に確認する
    # Also assert an existing pair stays unflagged, which only holds if the git grep inventory survived
    sibling = repo / "Client.Game" / "ElectricWire" / "WireView2.cs"
    sibling.write_text("using Game.ElectricWire;\nnamespace Client.Game.ElectricWire { class WireView2 {} }\n")
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "add wire dep")
    result = _run(repo)
    edges = [e for e in result["new_edges"] if e["using"] == "Game.ElectricWire"]
    assert len(edges) == 1
    assert edges[0]["file"] == "Client.Game/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs"


def test_pure_move_into_generic_dir_is_flagged(repo: Path):
    # 純粋な移動はrename圧縮で内容行が消え偽クリーンになる。--no-renamesでusing行が追加行として見える
    # A pure move is compacted into a rename with no content lines; --no-renames exposes its usings
    _git(
        repo, "mv",
        "Client.Game/ElectricWire/WireView.cs",
        "Client.Game/BlockSystem/PlaceSystem/Common/WireView.cs",
    )
    _git(repo, "commit", "-am", "move wire view into common")
    result = _run(repo)
    edges = [e for e in result["new_edges"] if e["using"] == "Game.ElectricWire"]
    assert [e["file"] for e in edges] == ["Client.Game/BlockSystem/PlaceSystem/Common/WireView.cs"]
    assert edges[0]["generic_origin"] is True


def test_dir_is_new_distinguishes_brand_new_directories(repo: Path):
    # base側にusing記録が皆無のディレクトリはdir_is_new=true（全usingが機械的に新エッジ化するため参考扱い）
    # A directory with no using inventory at base is dir_is_new, since every using there is trivially novel
    fresh = repo / "Client.Game" / "NewFeature" / "Common"
    fresh.mkdir(parents=True)
    (fresh / "NewFeatureService.cs").write_text(
        "using Game.ElectricWire;\nnamespace Client.Game.NewFeature { class NewFeatureService {} }\n"
    )
    existing = repo / "Client.Game" / "BlockSystem" / "PlaceSystem" / "Common" / "CommonBlockPlaceSystem.cs"
    existing.write_text(
        "using UnityEngine;\nusing Game.ElectricWire;\n"
        "namespace Client.Game.BlockSystem.PlaceSystem { class CommonBlockPlaceSystem {} }\n"
    )
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "new dir and existing dir edges")
    result = _run(repo)
    by_file = {e["file"]: e for e in result["new_edges"] if e["using"] == "Game.ElectricWire"}
    assert by_file["Client.Game/NewFeature/Common/NewFeatureService.cs"]["dir_is_new"] is True
    assert by_file["Client.Game/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs"]["dir_is_new"] is False
