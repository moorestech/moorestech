#!/usr/bin/env python3
"""外部 JSON（ingest.json / manifest.json / record.json / fix-result.json）の読み込み境界。

型の検証と既定値の補完をこの1箇所で行い、集計側は型が保証された dict だけを扱う。
値が null・欠落なら既定値、型が契約と食い違えば箱/記録ごと None＋理由文字列（呼び出し側が理由をログし件数に数えて除外する）。
テスター由来の文字列を Discord へ出す側の無害化（neutralize_discord_markup）も外部入力の境界としてここに置く。

Read boundary for external JSON (ingest.json / manifest.json / record.json / fix-result.json).
Type validation and defaulting happen only here, so aggregation code handles type-guaranteed dicts.
A null or missing value takes its default; a type mismatch yields (None, reason) for the whole file
(callers log the reason and count/exclude it). Neutralising tester-supplied text for Discord output
(neutralize_discord_markup) also lives here as part of the external-input boundary.
"""
from __future__ import annotations

import json
import math
import re
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
    "schemaVersion": (INT, None), "steamId": (STR, ""), "playSeconds": (NUMBER, None), "endReason": (STR, ""), "lastUiState": (STR, ""),
    "reachedChallenges": ([None], None), "completedResearch": ([None], None),
    "events": ([{"type": (STR, "")}], None),
}
FIX_RESULT_SCHEMA = {
    "status": (STR, ""), "pr_number": (INT, None), "base": (STR, ""), "summary": (STR, ""),
    "finishedAt": (STR, ""),
}


class Invalid:
    """型不一致の箇所を運ぶ番兵（入れ子は buildInfo.steamBuildLabel 形のパスになる）
    Sentinel carrying the mismatched field path (nested fields read as buildInfo.steamBuildLabel)"""

    def __init__(self, path: str) -> None:
        self.path = path


def read_json(path: Path) -> tuple[dict | None, str | None]:
    """壊れている・読めない・dict でない JSON は (None, 理由) にする。理由は呼び出し側がログする
    Broken, unreadable or non-dict JSON becomes (None, reason); callers log the reason"""
    # 外部（受け口・ゲーム本体・自動修正ラン）が書いたファイルの読み取りなので例外を隔離する
    # This reads files written by external producers, so the exception is isolated here
    try:
        raw = path.read_text(encoding="utf-8")
    except OSError as e:
        return None, f"読み込み失敗: {e}"
    try:
        data = json.loads(raw)
    except ValueError as e:
        return None, f"JSON解析失敗: {e}"
    if not isinstance(data, dict):
        return None, f"JSONがオブジェクトでない(got {type(data).__name__})"
    return data, None


def conform(data: dict, schema: dict) -> dict | Invalid:
    """スキーマどおりの dict を返す。型不一致が1つでもあれば不一致箇所を運ぶ Invalid
    Returns a dict shaped by the schema, or an Invalid carrying the mismatched field on any type mismatch"""
    out = {}
    for name, (kind, default) in schema.items():
        value = conform_value(data.get(name), kind, default, name)
        if isinstance(value, Invalid):
            return value
        out[name] = value
    return out


def conform_value(value, kind, default, path):
    # null・欠落は既定値。入れ子と list は空の形を既定にする
    # Null or missing takes the default; nested schemas and lists default to their empty shape
    if value is None:
        if isinstance(kind, dict):
            return conform({}, kind)
        return [] if isinstance(kind, list) else default
    if isinstance(kind, dict):
        if not isinstance(value, dict):
            return Invalid(path)
        nested = conform(value, kind)
        return Invalid(f"{path}.{nested.path}") if isinstance(nested, Invalid) else nested
    if isinstance(kind, list):
        return conform_list(value, kind[0], path)
    # bool は int の派生だが数値として受け入れない
    # bool subclasses int but is not accepted as a number
    if isinstance(value, bool) or not isinstance(value, kind):
        return Invalid(path)
    return value


def conform_list(value, element_kind, path):
    """要素型 None は中身を問わない（件数だけ使う list）
    An element kind of None accepts any element (lists used only for their length)"""
    if not isinstance(value, list):
        return Invalid(path)
    if element_kind is None:
        return value
    items = []
    for i, element in enumerate(value):
        if not isinstance(element, dict):
            return Invalid(f"{path}[{i}]")
        conformed = conform(element, element_kind)
        if isinstance(conformed, Invalid):
            return Invalid(f"{path}[{i}].{conformed.path}")
        items.append(conformed)
    return items


def read_conformed(path: Path, schema: dict) -> tuple[dict | None, str | None]:
    """読み込みと型の正規化を一度に行う（外部 JSON の唯一の入口）。None のときは理由も返す
    Reads and normalises types in one step, the single entry point for external JSON; returns a reason on None"""
    data, reason = read_json(path)
    if data is None:
        return None, reason
    conformed = conform(data, schema)
    if isinstance(conformed, Invalid):
        return None, f"型不一致: {conformed.path}"
    return conformed, None


def record_value_problem(record: dict) -> str | None:
    """型では弾けない record.json の値の契約違反を理由として返す（schemaVersion が 1 でない・
    playSeconds が NaN/Infinity/負数。json.loads は NaN/Infinity を受理するため平均が nan になる）
    Returns the reason for value-level contract violations types cannot catch (schemaVersion other than 1,
    or a NaN/Infinity/negative playSeconds, which json.loads accepts and which would turn the mean into nan)"""
    if record["schemaVersion"] != 1:
        return f"schemaVersion が 1 でない: {record['schemaVersion']!r}"
    seconds = record["playSeconds"]
    if seconds is not None and (not math.isfinite(seconds) or seconds < 0):
        return f"playSeconds が有限の非負数でない: {seconds!r}"
    return None


ZERO_WIDTH_SPACE = "\u200b"


def neutralize_discord_markup(text: str) -> str:
    """テスター由来の文字列を Discord へ出す前に無害化する。@ の直後にゼロ幅スペースを入れて
    @everyone/@here/<@id> を発火させず、連続するバッククォートの間にも入れてコードフェンスを開閉させない
    Neutralises tester-supplied text before it reaches Discord: a zero-width space after every @ stops
    @everyone/@here/<@id> pings, and one between consecutive backticks stops code fences opening or closing"""
    return re.sub(r"`(?=`)", "`" + ZERO_WIDTH_SPACE, text.replace("@", "@" + ZERO_WIDTH_SPACE))
