"""
MapMakingのUnity .asset(YAML)読み込みと、guid→プレハブパス逆引き索引を提供する。
Loads MapMaking Unity .asset YAML documents and provides a guid to prefab path index.
"""

from __future__ import annotations

import importlib.util
import re
import sys
from pathlib import Path

# pyyaml が無い環境では黙って劣化させず、導入方法を示して即終了する
# Without pyyaml, fail immediately and show how to install it instead of degrading silently
if importlib.util.find_spec("yaml") is None:
    sys.exit(
        "pyyaml が見つかりません。導入してから再実行してください "
        "(例: python3 -m venv .venv && .venv/bin/pip install pyyaml)"
    )

import yaml  # noqa: E402

_DOCUMENT_HEADER = re.compile(r"^--- !u!\d+ &\d+.*$", re.MULTILINE)
_DIRECTIVE_LINE = re.compile(r"^%(YAML|TAG).*$", re.MULTILINE)
_META_GUID = re.compile(r"^guid: ([0-9a-fA-F]{32})$", re.MULTILINE)
_GUID_FORMAT = re.compile(r"^[0-9a-fA-F]{32}$")


def load_unity_asset(asset_path: Path) -> dict:
    """単一MonoBehaviourの .asset を読み、そのフィールド辞書を返す。
    Reads a single-MonoBehaviour .asset and returns its field mapping."""
    documents = _split_unity_documents(asset_path.read_text(encoding="utf-8"))
    if len(documents) != 1:
        raise ValueError(f"{asset_path}: MonoBehaviourドキュメントが1件ではない ({len(documents)}件)")

    parsed = yaml.safe_load(documents[0])
    if list(parsed.keys()) != ["MonoBehaviour"]:
        raise ValueError(f"{asset_path}: MonoBehaviour以外のルートキー {list(parsed.keys())}")
    return parsed["MonoBehaviour"]


def build_prefab_guid_index(private_assets_root: Path, assets_root: Path) -> dict:
    """非公開アセット配下の .prefab.meta を走査し guid→Assets相対パスの辞書を作る。
    Scans .prefab.meta under the private assets and maps guid to an Assets-relative path."""
    index: dict[str, str] = {}
    for meta_path in sorted(private_assets_root.rglob("*.prefab.meta")):
        match = _META_GUID.search(meta_path.read_text(encoding="utf-8"))
        if match is None:
            raise ValueError(f"{meta_path}: guid行が見つからない")

        guid = match.group(1)
        prefab_path = "Assets/" + str(meta_path.with_suffix("").relative_to(assets_root))
        if guid in index:
            raise ValueError(f"guid重複 {guid}: {index[guid]} と {prefab_path}")
        index[guid] = prefab_path
    return index


def reference_guid(reference: dict, location: str) -> str:
    """Unityのオブジェクト参照 {fileID, guid, type} からguid文字列を取り出す。
    Extracts the guid string from a Unity object reference {fileID, guid, type}."""
    guid = reference.get("guid")
    if not isinstance(guid, str) or not _GUID_FORMAT.match(guid):
        raise ValueError(f"{location}: guidが32桁hex文字列でない ({reference!r})")
    return guid


def is_null_reference(reference: dict, location: str) -> bool:
    """参照が未設定(fileID 0)かどうかを判定する。
    Determines whether the reference is unset (fileID 0)."""
    if "fileID" not in reference:
        raise ValueError(f"{location}: オブジェクト参照にfileIDが無い ({reference!r})")
    return reference["fileID"] == 0 and "guid" not in reference


def _split_unity_documents(text: str) -> list[str]:
    """`--- !u!` 区切りで手動分割し、Unity独自タグをパーサに渡さない。
    Splits on `--- !u!` manually so Unity-specific tags never reach the parser."""
    without_directives = _DIRECTIVE_LINE.sub("", text)
    chunks = _DOCUMENT_HEADER.split(without_directives)
    return [chunk for chunk in chunks[1:] if chunk.strip()]
