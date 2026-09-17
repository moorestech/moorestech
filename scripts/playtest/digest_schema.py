#!/usr/bin/env python3
"""外部 JSON（ingest.json / manifest.json / record.json / fix-result.json）の読み込み境界。

型の検証と既定値の補完をこの1箇所で行い、集計側は型が保証された dict だけを扱う。
値が null・欠落なら既定値、型が契約と食い違えば箱/記録ごと None（呼び出し側が件数に数えて除外する）。

Read boundary for external JSON (ingest.json / manifest.json / record.json / fix-result.json).
Type validation and defaulting happen only here, so aggregation code handles type-guaranteed dicts.
A null or missing value takes its default; a type mismatch yields None for the whole file (callers count and exclude it).
"""
from __future__ import annotations

import json
from pathlib import Path

STR = (str,)
NUMBER = (int, float)
INT = (int,)

# スキーマ: フィールド名 → (型, 既定値)。型が dict なら入れ子スキーマ、[要素型] ならその要素の list
# Schema: field name -> (kind, default). A dict kind is a nested schema; [kind] is a list of that kind
INGEST_SCHEMA = {
    "id": (STR, ""), "steamId": (STR, ""), "readyAt": (STR, ""), "ingestedAt": (STR, ""),
}
MANIFEST_SCHEMA = {
    "kind": (STR, ""), "description": (STR, ""),
    "buildInfo": ({"steamBuildLabel": (STR, "")}, None),
}
RECORD_SCHEMA = {
    # playSeconds は既定値を None にする（0.0 だと「計測0秒」と「未計測」が区別できず平均へ無言混入する）
    # playSeconds defaults to None: 0.0 would conflate "measured zero" with "unmeasured" and silently skew the mean
    "steamId": (STR, ""), "playSeconds": (NUMBER, None), "endReason": (STR, ""), "lastUiState": (STR, ""),
    "reachedChallenges": ([None], None), "completedResearch": ([None], None),
    "events": ([{"type": (STR, "")}], None),
}
FIX_RESULT_SCHEMA = {
    "status": (STR, ""), "pr_number": (INT, None), "base": (STR, ""), "summary": (STR, ""),
    "finishedAt": (STR, ""),
}


class _Invalid:
    """型不一致を表す番兵 / Sentinel for a type mismatch"""


INVALID = _Invalid()


def read_json(path: Path) -> dict:
    """壊れている・読めない・dict でない JSON はすべて空 dict にする。呼び出し側が件数として報告する
    Broken, unreadable or non-dict JSON all become an empty dict; callers report the count"""
    # 外部（受け口・ゲーム本体・自動修正ラン）が書いたファイルの読み取りなので例外を隔離する
    # This reads files written by external producers, so the exception is isolated here
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}
    return data if isinstance(data, dict) else {}


def conform(data: dict, schema: dict) -> dict | None:
    """スキーマどおりの dict を返す。型不一致が1つでもあれば None
    Returns a dict shaped by the schema, or None on any type mismatch"""
    out = {}
    for name, (kind, default) in schema.items():
        value = conform_value(data.get(name), kind, default)
        if value is INVALID:
            return None
        out[name] = value
    return out


def conform_value(value, kind, default):
    # null・欠落は既定値。入れ子と list は空の形を既定にする
    # Null or missing takes the default; nested schemas and lists default to their empty shape
    if value is None:
        if isinstance(kind, dict):
            return conform({}, kind)
        return [] if isinstance(kind, list) else default
    if isinstance(kind, dict):
        return conform(value, kind) if isinstance(value, dict) else INVALID
    if isinstance(kind, list):
        return conform_list(value, kind[0])
    # bool は int の派生だが数値として受け入れない
    # bool subclasses int but is not accepted as a number
    if isinstance(value, bool) or not isinstance(value, kind):
        return INVALID
    return value


def conform_list(value, element_kind):
    """要素型 None は中身を問わない（件数だけ使う list）
    An element kind of None accepts any element (lists used only for their length)"""
    if not isinstance(value, list):
        return INVALID
    if element_kind is None:
        return value
    items = []
    for element in value:
        conformed = conform(element, element_kind) if isinstance(element, dict) else None
        if conformed is None:
            return INVALID
        items.append(conformed)
    return items


def read_conformed(path: Path, schema: dict) -> dict | None:
    """読み込みと型の正規化を一度に行う（外部 JSON の唯一の入口）
    Reads and normalises types in one step; the single entry point for external JSON"""
    return conform(read_json(path), schema)
