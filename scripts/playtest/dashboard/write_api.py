"""人が付ける状態（既読・チケットのリンク）を書き換える POST の受け口。

認証は前段の Cloudflare Access に任せ、ここでは別サイトからの送信（CSRF）だけを弾く: JSON 本文と専用ヘッダを必須にし、
Origin があれば許可ホストと一致させる（フォーム送信では専用ヘッダを付けられず、fetch は事前確認で止まる）。
報告キー（steamId/id）は取り込みと同じ安全セグメント規則で検証し、実在する報告箱だけを受け付ける。

The POST entry point that changes human-made state (read marks and ticket links).
Authentication stays with Cloudflare Access in front; this only blocks cross-site submissions (CSRF) by requiring a JSON body
and a dedicated header, and by matching any Origin against the allowed hosts (forms cannot set the header and fetch stops at preflight).
Report keys (steamId/id) pass the same safe-segment rule as ingest, and only existing report boxes are accepted.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path
from urllib.parse import urlsplit

import dashboard_state

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "lib"))
from safe_segment import is_safe_segment  # noqa: E402

CSRF_HEADER = "X-Playtest-Dashboard"
BODY_LIMIT = 16 * 1024


def handle_post(handler, route: str, reports_root: Path, state_path: Path, allowed_hosts: frozenset) -> tuple[int, dict]:
    """(HTTP ステータス, 応答 JSON) を返す。拒否理由は必ずログと応答の両方に出す
    Returns (HTTP status, response JSON); every rejection reason goes to both the log and the response"""
    reason = request_problem(handler, allowed_hosts)
    if reason is not None:
        return reject(403, reason, route)
    body, reason = read_body(handler)
    if body is None:
        return reject(400, reason, route)
    key, reason = report_key(body, reports_root)
    if key is None:
        return reject(400, reason, route)
    if route == "/api/read" and not isinstance(body.get("read"), bool):
        return reject(400, "read は true/false にする", route)
    if route == "/api/links/add":
        problem = dashboard_state.link_problem(body.get("url"), body.get("title"))
        if problem is not None:
            return reject(400, problem, route)
    if route == "/api/links/remove" and not isinstance(body.get("url"), str):
        return reject(400, "url を指定する", route)
    # 状態ファイルの書き込みはディスク IO（容量不足・権限）という外部境界なので失敗を隔離して 500 にする
    # Writing the state file is disk IO, an external boundary (space, permissions), so failures are isolated as 500
    try:
        if route == "/api/read":
            broken = dashboard_state.set_read(state_path, key, body["read"])
        elif route == "/api/links/add":
            broken = dashboard_state.add_link(state_path, key, body["url"], body["title"].strip())
        elif route == "/api/links/remove":
            broken = dashboard_state.remove_link(state_path, key, body["url"])
        else:
            return reject(404, "未知の書き込みルート", route)
    except OSError as error:
        return reject(500, f"状態ファイルへ書けない: {error}", route)
    if broken is not None:
        return reject(500, f"状態ファイルが壊れているため上書きしない: {broken}", route)
    return 200, {"ok": True}


def request_problem(handler, allowed_hosts: frozenset) -> str | None:
    if handler.headers.get(CSRF_HEADER) != "1":
        return f"{CSRF_HEADER} ヘッダが無い（別サイトからの送信の疑い）"
    if not (handler.headers.get("Content-Type") or "").startswith("application/json"):
        return "Content-Type が application/json でない"
    origin = handler.headers.get("Origin")
    if origin and urlsplit(origin).netloc not in allowed_hosts:
        return f"許可外の Origin: {origin!r}"
    return None


def read_body(handler) -> tuple[dict | None, str]:
    raw_length = handler.headers.get("Content-Length") or "0"
    length = int(raw_length) if raw_length.isdigit() else -1
    if length <= 0 or length > BODY_LIMIT:
        return None, f"本文の長さが範囲外: {length}"
    # 送られてきた本文の JSON 解析は外部入力の境界なので失敗を隔離する
    # Parsing the submitted JSON body is an external-input boundary, so failures are isolated
    try:
        body = json.loads(handler.rfile.read(length).decode("utf-8"))
    except (ValueError, UnicodeDecodeError) as error:
        return None, f"本文が JSON でない: {error}"
    return (body, "") if isinstance(body, dict) else (None, "本文が JSON オブジェクトでない")


def report_key(body: dict, reports_root: Path) -> tuple[str | None, str]:
    steam_id, report_id = body.get("steamId"), body.get("id")
    if not is_safe_segment(steam_id) or not is_safe_segment(report_id):
        return None, "steamId/id が安全なパスセグメントでない"
    if not (reports_root / steam_id / report_id / "ingest.json").is_file():
        return None, "その報告は存在しない"
    return f"{steam_id}/{report_id}", ""


def reject(status: int, reason: str, route: str) -> tuple[int, dict]:
    print(f"[dashboard] {status} 書き込み拒否 {route}: {reason}", file=sys.stderr)
    return status, {"ok": False, "error": reason}
