"""表示だけに使う項目を、判定用の型検証とは切り離して1項目ずつ緩く読む。

判定（投入候補・集計対象）は digest_schema の契約どおりに行い、表示用の項目が1つ型違いでも箱ごと落とさない。
型違いの項目は理由をログに出して None にする（無音で空表示にしない）。

Reads display-only fields one at a time, decoupled from the type validation used for decisions.
Decisions follow the digest_schema contract unchanged, so one mistyped display field never drops the whole box;
a mistyped field is logged with its reason and becomes None (never a silent blank).
"""
from __future__ import annotations

import math
import sys
from pathlib import Path


def warn(reason: str, path: Path) -> None:
    print(f"[dashboard] {reason}: {path}", file=sys.stderr)


def pick(data: dict, dotted: str, kinds: tuple, source: Path):
    """"a.b.c" 形式のパスで値を取る。欠落は None、途中の階層や値の型違い・非有限の数値はログを出して None
    Fetches a value by an "a.b.c" path; missing gives None, while a mistyped level/value or non-finite number is logged and gives None"""
    value = data
    for key in dotted.split("."):
        if not isinstance(value, dict):
            warn(f"表示用の項目 {dotted} の途中が辞書でない（{type(value).__name__}）ため空表示にする", source)
            return None
        if value.get(key) is None:
            return None
        value = value[key]
    # bool は int の派生だが数値として扱わない（digest_schema と同じ規則）
    # bool subclasses int but is not treated as a number (same rule as digest_schema)
    if not isinstance(value, kinds) or (isinstance(value, bool) and bool not in kinds):
        warn(f"表示用の項目 {dotted} の型が想定外（{type(value).__name__}）で空表示にする", source)
        return None
    # json は Infinity/NaN を受理するが、そのまま返すとブラウザの JSON.parse が全体を拒否する
    # json accepts Infinity/NaN, but passing them on makes the browser's JSON.parse reject the whole payload
    if isinstance(value, float) and not math.isfinite(value):
        warn(f"表示用の項目 {dotted} が有限の数でない（{value!r}）ため空表示にする", source)
        return None
    return value


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


# ADR 0057 の webUiHealth の綴り（C# の WebUiHostStartupState・CefProcessState の camelCase）。集合外は契約の破れとして読めない扱いにする
# Spellings of webUiHealth per ADR 0057 (camelCase of the C# enums); anything outside is a broken contract and reads as unreadable
HOST_STARTUP_VALUES = ("notAttempted", "started", "failed", "skippedByEditorSwitch", "unknown")
CEF_PROCESS_VALUES = ("notObserved", "observedAfterBrowserStart", "lostRestarting", "recoveryFailed", "unknown")


def web_ui_health(raw: dict, source: Path) -> dict:
    """manifest v6 の webUiHealth を表示用に読む。status 1 項目で欄なし・前回分の不明・読めない・記録ありを決め、表示はそれだけで分岐する
    Reads manifest v6 webUiHealth for display; one status decides absent/unknown/unreadable/recorded and the view branches on it alone"""
    # キーの無い旧箱だけが欄なし。v6 の箱に null や dict 以外が入っていたら契約の破れなので読めない扱いにする
    # Only an old box without the key is absent; a null or non-dict in a v6 box breaks the contract and reads as unreadable
    if "webUiHealth" not in raw:
        return {"status": "absent"}
    health = raw["webUiHealth"]
    if not isinstance(health, dict):
        warn(f"webUiHealth が dict でない（{health!r}）ため読めないと表示する", source)
        return {"status": "unreadable"}
    host = pick(raw, "webUiHealth.hostStartup", (str,), source)
    cef = pick(raw, "webUiHealth.cefProcess", (str,), source)
    if host not in HOST_STARTUP_VALUES or cef not in CEF_PROCESS_VALUES:
        warn(f"webUiHealth の綴りが ADR 0057 の集合外（hostStartup={host!r}, cefProcess={cef!r}）のため読めないと表示する", source)
        return {"status": "unreadable"}
    # crash の箱は落ちたセッションの値を持たない。秒の null を「未受信」と読ませないよう先に分ける
    # Crash boxes hold no values of the crashed session; split them first so a null second is never read as never-received
    if host == "unknown" or cef == "unknown":
        return {"status": "unknown"}
    seconds = pick(raw, "webUiHealth.secondsSinceLastWebMessage", (int, float), source)
    if "secondsSinceLastWebMessage" not in health or (health["secondsSinceLastWebMessage"] is not None and seconds is None):
        warn("webUiHealth.secondsSinceLastWebMessage が無いか数でないため読めないと表示する", source)
        return {"status": "unreadable"}
    return {"status": "recorded", "hostStartup": host, "cefProcess": cef, "secondsSinceLastWebMessage": seconds}


# manifest v7 の gameUiExceptions の見本の上限（C# の GameUiExceptionTally.SampleLimit と同じ値）
# Sample cap of manifest v7 gameUiExceptions (same value as C# GameUiExceptionTally.SampleLimit)
GAME_UI_EXCEPTION_SAMPLE_LIMIT = 5


def game_ui_exceptions(raw: dict, source: Path) -> dict:
    """manifest v7 の gameUiExceptions を表示用に読む。webUiHealth と同じく status 1 項目で欄なし・不明・読めない・記録ありを決める
    Reads manifest v7 gameUiExceptions for display; as with webUiHealth, one status decides absent/unknown/unreadable/recorded"""
    # キーの無い v6 以前の箱だけが欄なし。dict でない・集合外の status は契約の破れなので読めない扱いにする
    # Only a v6-or-older box without the key is absent; a non-dict or an off-contract status breaks the contract and reads as unreadable
    if "gameUiExceptions" not in raw:
        return {"status": "absent"}
    value = raw["gameUiExceptions"]
    if not isinstance(value, dict) or value.get("status") not in ("recorded", "unknown"):
        warn(f"gameUiExceptions が契約の形でない（{value!r}）ため読めないと表示する", source)
        return {"status": "unreadable"}
    # unknown は数えていない箱。件数を 0 件と読ませない
    # unknown is an uncounted box; never let it read as zero
    if value["status"] == "unknown":
        return {"status": "unknown"}
    count = value.get("count")
    samples = value.get("samples")
    if not isinstance(count, int) or isinstance(count, bool) or not isinstance(samples, list):
        warn(f"gameUiExceptions の count が整数でないか samples が配列でない（count={count!r}）ため読めないと表示する", source)
        return {"status": "unreadable"}
    return {"status": "recorded", "count": count, "samples": game_ui_exception_samples(samples, source)}


def game_ui_exception_samples(samples: list, source: Path) -> list[dict]:
    """見本を最大 5 件、tick・message・frame の 3 項目で返す。形の崩れた見本は件数をログに出して除く
    Returns up to five samples as tick/message/frame; malformed samples are dropped with a logged count"""
    picked = []
    for sample in samples[:GAME_UI_EXCEPTION_SAMPLE_LIMIT]:
        if not isinstance(sample, dict) or not isinstance(sample.get("message"), str) or not isinstance(sample.get("frame"), str):
            continue
        tick = sample.get("tick")
        picked.append({"tick": tick if isinstance(tick, int) and not isinstance(tick, bool) else None,
                       "message": sample["message"], "frame": sample["frame"]})
    dropped = min(len(samples), GAME_UI_EXCEPTION_SAMPLE_LIMIT) - len(picked)
    if dropped:
        warn(f"gameUiExceptions.samples の形の崩れた見本 {dropped}件を表示から除く", source)
    return picked
