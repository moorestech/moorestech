#!/usr/bin/env python3
"""取り込み時点の Steam 表示名をダイジェストのラベルへ変換する。
Formats the Steam display name captured at ingest time for the digest.
"""
import re

# 取り込み済みの古い箱への防御。steam_persona.py が正規化する前のデータにも同じ畳み込みを掛ける（C12）
# Defends already-ingested legacy boxes too, folding the same way steam_persona.py now does before storing (C12)
_CONTROL_CHARS_RE = re.compile(r"[\x00-\x1f\x7f]+")


def _normalize(value: str) -> str:
    return _CONTROL_CHARS_RE.sub(" ", value).strip()


def tester_label(meta: dict) -> str:
    """同一人物の判定に使う SteamID と、その時点の表示名を並べる。
    Shows the SteamID used for identity beside the display name captured then."""
    steam_id = meta.get("steamId", "")
    name = _normalize(meta.get("steamPersonaName", ""))
    if not name:
        return f"名前未解決（SteamID {steam_id}）"
    return f"{name}（SteamID {steam_id}）"


def reporter_label(meta: dict) -> str:
    """報告行には取得済みのプロフィール URL も添える。
    Adds the captured profile URL to report rows when available."""
    label = tester_label(meta)
    name = _normalize(meta.get("steamPersonaName", ""))
    url = _normalize(meta.get("steamProfileUrl", ""))
    if not name or not url:
        return label
    return f"{name}（SteamID {meta.get('steamId', '')}・{url}）"
