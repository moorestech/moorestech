"""ダッシュボードで人が付ける状態（報告の既読・関連チケットのリンク）を1つの JSON に保存する。

報告箱の中にマーカーを置くと、テスターが同名のファイルを送って既読を偽装できるため、箱の外の1ファイルに持つ。
書き込みはこのサーバー1プロセスだけが行う前提で、プロセス内ロックと一時ファイルからの置き換えで壊れた中間状態を残さない。

Stores human-made dashboard state (report read marks and related ticket links) in one JSON file.
A marker inside a report box could be forged by a tester sending a same-named file, so the state lives outside the boxes.
Only this server process writes it; an in-process lock plus write-to-temp-then-replace never leaves a half-written file.
"""
from __future__ import annotations

import json
import os
import threading
from datetime import datetime, timezone
from pathlib import Path

import digest_schema as schema
from display_fields import warn

TITLE_LIMIT = 200
URL_LIMIT = 2000
_lock = threading.Lock()


def read_state(path: Path) -> tuple[dict | None, str]:
    """ファイルが無ければ空の状態、壊れていれば (None, 理由)
    Returns an empty state when the file is absent, or (None, reason) when it is broken"""
    if not path.is_file():
        return {"read": {}, "links": {}}, ""
    data, reason = schema.read_json(path)
    if data is None or not isinstance(data.get("read"), dict) or not isinstance(data.get("links"), dict):
        return None, reason or "read/links が辞書でない"
    return data, ""


def load_state_for_display(path: Path) -> dict:
    """表示用。壊れていれば理由をログに出し、未読・リンク無しとして出す（ファイルには触れない）
    For display; a broken file is logged and shown as all unread and unlinked, without touching the file"""
    state, reason = read_state(path)
    if state is None:
        warn(f"ダッシュボードの状態ファイルを読めないため未読・リンク無しとして表示する: {reason}", path)
        return {"read": {}, "links": {}}
    return state


def set_read(path: Path, key: str, read: bool) -> str | None:
    """壊れた状態ファイルは上書きせず理由を返す（既読やリンクの記録を空で潰さないため）。以下の更新関数も同じ
    A broken state file is never overwritten; the reason is returned so existing marks are not wiped. Same for the updaters below"""
    with _lock:
        state, reason = read_state(path)
        if state is None:
            return reason
        if read:
            state["read"][key] = now_iso()
        else:
            state["read"].pop(key, None)
        save_state(path, state)
        return None


def add_link(path: Path, key: str, url: str, title: str) -> str | None:
    """同じ URL は1つにまとめ、題名だけ新しい値で上書きする
    The same URL is kept once; only its title is refreshed"""
    with _lock:
        state, reason = read_state(path)
        if state is None:
            return reason
        links = [link for link in state["links"].get(key, []) if link.get("url") != url]
        links.append({"url": url, "title": title, "addedAt": now_iso()})
        state["links"][key] = links
        save_state(path, state)
        return None


def remove_link(path: Path, key: str, url: str) -> str | None:
    with _lock:
        state, reason = read_state(path)
        if state is None:
            return reason
        links = [link for link in state["links"].get(key, []) if link.get("url") != url]
        if links:
            state["links"][key] = links
        else:
            state["links"].pop(key, None)
        save_state(path, state)
        return None


def link_problem(url: object, title: object) -> str | None:
    """リンクとして受け付けない理由を返す。https 以外は画面で踏ませないため拒否する
    Returns why a link is rejected; non-https URLs are refused so the page never makes them clickable"""
    if not isinstance(url, str) or not url.startswith("https://") or len(url) > URL_LIMIT or any(c.isspace() for c in url):
        return "url は空白を含まない https:// の URL にする"
    if not isinstance(title, str) or len(title) > TITLE_LIMIT:
        return f"title は {TITLE_LIMIT} 文字以内の文字列にする"
    return None


def save_state(path: Path, state: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix(".tmp")
    temp.write_text(json.dumps(state, ensure_ascii=False, indent=1, sort_keys=True), encoding="utf-8")
    os.replace(temp, path)


def now_iso() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
