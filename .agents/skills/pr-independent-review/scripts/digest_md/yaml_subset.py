# digestが使うYAMLサブセットのみを読み取る
# Read only the YAML subset that digest.md actually uses
from __future__ import annotations

from .errors import DigestError


def parse_yaml_block(text: str) -> dict:
    # digestが使うサブセットだけを読む。深い構造は推測せずエラーにする
    # Only the subset digest uses; deeper structures are rejected rather than guessed
    out: dict = {}
    key = None
    for raw in text.splitlines():
        if not raw.strip():
            continue
        stripped = raw.strip()
        # `- ` で始まる行は直前キーのリスト項目として積む
        # A line starting with `- ` is appended to the previous key's list
        if stripped.startswith("- "):
            if key is None:
                raise DigestError(f"リスト項目の親キーがありません: {raw!r}")
            if not isinstance(out.get(key), list):
                raise DigestError(f"キー {key} に値とリストが混在しています")
            out[key].append(stripped[2:].strip())
            continue
        if raw.startswith(" "):
            raise DigestError(f"未対応のインデント行です: {raw!r}")
        if ":" not in raw:
            raise DigestError(f"key: value 形式ではありません: {raw!r}")
        # `key:` 単独は後続のリスト項目待ち、`[a, b]` はインラインリストとして展開する
        # A bare `key:` awaits following list items; `[a, b]` expands as an inline list
        key, value = raw.split(":", 1)
        key, value = key.strip(), value.strip()
        if value == "":
            out[key] = []
        elif value.startswith("[") and value.endswith("]"):
            inner = value[1:-1].strip()
            out[key] = [v.strip() for v in inner.split(",")] if inner else []
        else:
            out[key] = value
    return out
