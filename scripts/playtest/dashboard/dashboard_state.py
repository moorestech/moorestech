"""ダッシュボードで人が付ける状態（報告の既読・関連チケットのリンク）を1つの JSON に保存する。

置き場は moorestech_logs の git 管理外（.state/）。報告箱の中に置くとテスターが同名ファイルを送って既読を偽装でき、
git 管理下に置くとログ同期の rebase 中に古い版へ戻ったファイルを書き換えて記録が消えるため。
書き手はこのサーバー1プロセスだけで、プロセス内ロックと一時ファイルからの置き換えで壊れた中間状態を残さない。
更新関数は (HTTP ステータス, 理由) を返し、壊れた状態ファイルは上書きしない（既読やリンクの記録を空で潰さないため）。

Stores human-made dashboard state (report read marks and related ticket links) in one JSON file.
It lives outside git in moorestech_logs/.state/: inside a report box a tester could forge it with a same-named file, and under git
a write during the log sync's rebase would hit a file temporarily rolled back to an old version and lose records.
Only this server process writes it; an in-process lock plus write-to-temp-then-replace never leaves a half-written file.
Updaters return (HTTP status, reason) and never overwrite a broken state file, so existing marks and links are not wiped.
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
    """ファイルが無ければ空の状態、形が契約と違えば (None, 理由)。値の型まで見る（手で直したファイルで落ちないため）
    Returns an empty state when absent, or (None, reason) when the shape breaks the contract, down to value types"""
    if not path.is_file():
        return {"read": {}, "links": {}}, ""
    data, reason = schema.read_json(path)
    if data is None:
        return None, reason
    read, links = data.get("read"), data.get("links")
    if not isinstance(read, dict) or not all(isinstance(v, str) for v in read.values()):
        return None, "read が「キー→日時文字列」の辞書でない"
    if not isinstance(links, dict) or not all(isinstance(v, list) and all(valid_link(l) for l in v) for v in links.values()):
        return None, "links が「キー→{url,title,addedAt} の配列」の辞書でない"
    return data, ""


def valid_link(link: object) -> bool:
    return isinstance(link, dict) and all(isinstance(link.get(k), str) for k in ("url", "title", "addedAt"))


def load_state_for_display(path: Path) -> dict:
    """表示用。壊れていれば理由をログに出し、未読・リンク無しとして出す（ファイルには触れない）
    For display; a broken file is logged and shown as all unread and unlinked, without touching the file"""
    state, reason = read_state(path)
    if state is None:
        warn(f"ダッシュボードの状態ファイルを読めないため未読・リンク無しとして表示する: {reason}", path)
        return {"read": {}, "links": {}}
    return state


def set_read(path: Path, keys: list[str], read: bool) -> tuple[int, str]:
    """複数の報告の既読をまとめて1回で書く（一括既読を途中失敗させず、読み直しも1回で済ませるため）
    Writes the read mark of several reports at once, so bulk marking never half-fails and needs a single reload"""
    with _lock:
        state, reason = read_state(path)
        if state is None:
            return 500, f"状態ファイルが壊れているため上書きしない: {reason}"
        for key in keys:
            if read:
                state["read"][key] = now_iso()
            else:
                state["read"].pop(key, None)
        save_state(path, state)
        return 200, ""


def add_link(path: Path, key: str, url: str, title: str) -> tuple[int, str]:
    """同じ URL は1つにまとめ、題名だけ新しい値で上書きする
    The same URL is kept once; only its title is refreshed"""
    with _lock:
        state, reason = read_state(path)
        if state is None:
            return 500, f"状態ファイルが壊れているため上書きしない: {reason}"
        links = [link for link in state["links"].get(key, []) if link["url"] != url]
        links.append({"url": url, "title": title, "addedAt": now_iso()})
        state["links"][key] = links
        save_state(path, state)
        return 200, ""


def remove_link(path: Path, key: str, url: str) -> tuple[int, str]:
    """一致する URL が無ければ 404（解除できたと誤解させない）
    No matching URL gives 404, so callers never believe an unlink happened"""
    with _lock:
        state, reason = read_state(path)
        if state is None:
            return 500, f"状態ファイルが壊れているため上書きしない: {reason}"
        current = state["links"].get(key, [])
        links = [link for link in current if link["url"] != url]
        if len(links) == len(current):
            return 404, "その URL はこの報告に紐付いていない"
        if links:
            state["links"][key] = links
        else:
            state["links"].pop(key, None)
        save_state(path, state)
        return 200, ""


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
