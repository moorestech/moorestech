"""R2 にだけある報告ファイル（動画等）を、受け口の管理 API から取って画面へ中継する（Range をそのまま渡しシークに対応）。

受け口の URL と管理キーは環境変数 PLAYTEST_RECEIVER_BASE・PLAYTEST_ADMIN_KEY だけから起動時に1回読む。
env.sh は起動ラッパー bin/run.sh が source して渡す（ingest-dispatch.sh と同じ）。足りなければ起動ログに出し、中継は 503 を返す。
受け口が 410 を返したら片付けで消した箱。理由はログに出し、ブラウザへはステータスをそのまま返す。

Relays report files that live only in R2 (videos etc.) from the receiver's admin API to the page, passing Range through for seeking.
The receiver URL and admin key are read once at startup from PLAYTEST_RECEIVER_BASE and PLAYTEST_ADMIN_KEY only.
env.sh is sourced by the bin/run.sh wrapper (like ingest-dispatch.sh); anything missing is logged at startup and relays answer 503.
A 410 from the receiver means the box was purged after settling; the reason is logged and the status is passed to the browser as is.
"""
from __future__ import annotations

import http.client
import sys
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler
from typing import Mapping, NamedTuple
from urllib.parse import quote

from security_headers import send_security_headers

REQUIRED_ENV = ("PLAYTEST_RECEIVER_BASE", "PLAYTEST_ADMIN_KEY")
PASS_HEADERS = ("Content-Length", "Content-Range", "Accept-Ranges")
CHUNK = 1 << 20
TIMEOUT_SECONDS = 60
USER_AGENT = "moorestech-playtest-dashboard/1"


class NoRedirect(urllib.request.HTTPRedirectHandler):
    # 管理キーを別ホストへ送らないため、リダイレクトは追わない（receiver-api.sh と同じ方針）
    # Never follow redirects so the admin key is never sent to another host (same policy as receiver-api.sh)
    def redirect_request(self, *args, **kwargs):  # noqa: ANN002, ANN003
        return None


OPENER = urllib.request.build_opener(NoRedirect)


class ReceiverSettings(NamedTuple):
    base: str
    key: str
    missing: tuple[str, ...]


def load_receiver_settings(environ: Mapping[str, str]) -> ReceiverSettings:
    """起動時に1回だけ呼ぶ。足りない変数名を missing に入れ、呼び出し側が起動ログへ出す
    Called once at startup; missing names the absent variables so the caller logs them at startup"""
    missing = tuple(name for name in REQUIRED_ENV if not environ.get(name))
    return ReceiverSettings(environ.get("PLAYTEST_RECEIVER_BASE", "").rstrip("/"), environ.get("PLAYTEST_ADMIN_KEY", ""), missing)


def relay(handler: BaseHTTPRequestHandler, settings: ReceiverSettings, steam_id: str, report_id: str, name: str, content_type: str) -> None:
    if settings.missing:
        print(f"[dashboard] 503 {'・'.join(settings.missing)} が起動時の環境変数に無く R2 の {name} を取れない（bin/run.sh で起動する）", file=sys.stderr)
        handler.send_error(503)
        return
    base, key = settings.base, settings.key
    path = "/".join(quote(segment, safe="") for segment in ("report", steam_id, report_id, *name.split("/")))
    # Cloudflare は Python 標準の User-Agent を 403（error 1010）で弾くため、名乗りを付ける
    # Cloudflare rejects Python's default User-Agent with 403 (error 1010), so identify explicitly
    request = urllib.request.Request(f"{base}/v1/inbox/{path}", headers={"X-Admin-Key": key, "User-Agent": USER_AGENT})
    if handler.headers.get("Range"):
        request.add_header("Range", handler.headers["Range"])
    # 受け口への HTTP はネットワーク境界なので失敗を隔離し、ステータスと理由をログへ出してブラウザへ返す
    # HTTP to the receiver is a network boundary, so failures are isolated, logged with status and reason, and returned to the browser
    try:
        response = OPENER.open(request, timeout=TIMEOUT_SECONDS)
    except urllib.error.HTTPError as error:
        with error:
            body = error.read(200).decode("utf-8", "replace")
        print(f"[dashboard] R2 中継 {error.code}: {steam_id}/{report_id}/{name} {body}", file=sys.stderr)
        if error.code == 416:
            return send_unsatisfiable(handler, error.headers.get("Content-Range"))
        handler.send_error(error.code if error.code in (404, 410) else 502)
        return
    except (urllib.error.URLError, OSError) as error:
        print(f"[dashboard] 502 受け口へ届かない: {steam_id}/{report_id}/{name} {error}", file=sys.stderr)
        handler.send_error(502)
        return
    with response:
        handler.send_response(response.status)
        handler.send_header("Content-Type", content_type)
        for header in PASS_HEADERS:
            if response.headers.get(header):
                handler.send_header(header, response.headers[header])
        handler.send_header("Cache-Control", "private, max-age=300")
        send_security_headers(handler)
        handler.end_headers()
        copy_body(handler, response, name)


def send_unsatisfiable(handler: BaseHTTPRequestHandler, content_range: str | None) -> None:
    # 416 にはプレイヤーが範囲を直せるよう受け口の Content-Range（bytes */全長）をそのまま付ける
    # A 416 carries the receiver's Content-Range (bytes */size) so the player can correct its range
    handler.send_response(416)
    if content_range:
        handler.send_header("Content-Range", content_range)
    handler.send_header("Content-Length", "0")
    send_security_headers(handler)
    handler.end_headers()


def copy_body(handler: BaseHTTPRequestHandler, response, name: str) -> None:
    # ブラウザはシークのたびに接続を切るため、送信途中の切断はネットワーク境界の正常系として隔離する
    # Browsers drop the connection on every seek, so a mid-send disconnect is isolated as a normal network-boundary event
    expected = response.headers.get("Content-Length")
    sent = 0
    try:
        while chunk := response.read(CHUNK):
            handler.wfile.write(chunk)
            sent += len(chunk)
    except (BrokenPipeError, ConnectionResetError) as error:
        print(f"[dashboard] 中継途中でクライアントが切断: {name} {error}", file=sys.stderr)
        return
    except (http.client.IncompleteRead, TimeoutError) as error:
        report_truncated(handler, name, f"{type(error).__name__}: {error}")
        return
    # read(量指定) は受け口が途中で閉じても例外を出さず空を返すので、名乗った長さと突き合わせる
    # read(amt) returns empty instead of raising when the receiver closes early, so compare with the advertised length
    if expected is not None and expected.isdigit() and sent < int(expected):
        report_truncated(handler, name, f"{sent}/{expected} bytes")


def report_truncated(handler: BaseHTTPRequestHandler, name: str, detail: str) -> None:
    # ヘッダは送信済みなので接続を閉じ、ブラウザには長さ不足として見せる
    # Headers are already sent, so close the connection and let the browser see a short body
    print(f"[dashboard] 受け口からの本文が途中で途切れた: {name} {detail}", file=sys.stderr)
    handler.close_connection = True
