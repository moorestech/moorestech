#!/usr/bin/env python3
"""取り込み時点の Steam 表示名をダイジェストのラベルへ変換する。
Formats the Steam display name captured at ingest time for the digest.
"""


def tester_label(meta: dict) -> str:
    """同一人物の判定に使う SteamID と、その時点の表示名を並べる。
    Shows the SteamID used for identity beside the display name captured then."""
    steam_id = meta.get("steamId", "")
    name = meta.get("steamPersonaName", "")
    if not name:
        return f"名前未解決（SteamID {steam_id}）"
    return f"{name}（SteamID {steam_id}）"


def reporter_label(meta: dict) -> str:
    """報告行には取得済みのプロフィール URL も添える。
    Adds the captured profile URL to report rows when available."""
    label = tester_label(meta)
    url = meta.get("steamProfileUrl", "")
    if not meta.get("steamPersonaName", "") or not url:
        return label
    return f"{meta['steamPersonaName']}（SteamID {meta.get('steamId', '')}・{url}）"
