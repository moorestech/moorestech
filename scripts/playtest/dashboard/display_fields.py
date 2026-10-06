"""表示だけに使う項目を、判定用の型検証とは切り離して1項目ずつ緩く読む。

判定（投入候補・集計対象）は digest_schema の契約どおりに行い、表示用の項目が1つ型違いでも箱ごと落とさない。
型違いの項目は理由をログに出して None にする（無音で空表示にしない）。

Reads display-only fields one at a time, decoupled from the type validation used for decisions.
Decisions follow the digest_schema contract unchanged, so one mistyped display field never drops the whole box;
a mistyped field is logged with its reason and becomes None (never a silent blank).
"""
from __future__ import annotations

import sys
from pathlib import Path


def warn(reason: str, path: Path) -> None:
    print(f"[dashboard] {reason}: {path}", file=sys.stderr)


def pick(data: dict, dotted: str, kinds: tuple, source: Path):
    """"a.b.c" 形式のパスで値を取る。欠落は None、型違いはログを出して None
    Fetches a value by an "a.b.c" path; missing gives None, a type mismatch is logged and gives None"""
    value = data
    for key in dotted.split("."):
        if not isinstance(value, dict) or value.get(key) is None:
            return None
        value = value[key]
    # bool は int の派生だが数値として扱わない（digest_schema と同じ規則）
    # bool subclasses int but is not treated as a number (same rule as digest_schema)
    if isinstance(value, kinds) and not (isinstance(value, bool) and bool not in kinds):
        return value
    warn(f"表示用の項目 {dotted} の型が想定外（{type(value).__name__}）で空表示にする", source)
    return None


def pick_strings(data: dict, key: str, source: Path) -> list[str]:
    """文字列の配列だけを返す。文字列以外の要素は件数をログに出して除く
    Returns only the string elements; non-string elements are dropped with a logged count"""
    values = data.get(key)
    if not isinstance(values, list):
        return []
    strings = [value for value in values if isinstance(value, str)]
    if len(strings) != len(values):
        warn(f"{key} の文字列でない要素 {len(values) - len(strings)}件を表示から除く", source)
    return strings
