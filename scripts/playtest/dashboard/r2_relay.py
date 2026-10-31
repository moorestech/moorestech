"""R2 にだけある報告ファイル（動画等）を、受け口の管理 API から取って画面へ中継する（Range をそのまま渡しシークに対応）。

管理キーは環境変数 PLAYTEST_ADMIN_KEY、無ければ取り込みと同じ env.sh（`export 名前=値` の行）から読む。
受け口が 410 を返したら片付けで消した箱。理由はログに出し、ブラウザへはステータスをそのまま返す。

Relays report files that live only in R2 (videos etc.) from the receiver's admin API to the page, passing Range through for seeking.
The admin key comes from PLAYTEST_ADMIN_KEY, else from the same env.sh as ingest (`export NAME=value` lines).
A 410 from the receiver means the box was purged after settling; the reason is logged and the status is passed to the browser as is.
"""
from __future__ import annotations

import os
import sys
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler
from pathlib import Path
from urllib.parse import quote

from security_headers import send_security_headers

DEFAULT_BASE = "https://playtest.moores.tech"
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


def read_env_file(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    if not path.is_file():
        return values
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line.startswith("export ") and "=" in line:
            name, value = line[len("export "):].split("=", 1)
            values[name.strip()] = value.strip().strip("'\"")
    return values


def receiver_settings(env_file: Path) -> tuple[str, str]:
    """(受け口の URL, 管理キー)。キーが無ければ空文字を返し、呼び出し側が 503 と理由を出す
    Returns (receiver URL, admin key); an empty key makes the caller answer 503 with the reason"""
    from_file = read_env_file(env_file)
    base = os.environ.get("PLAYTEST_RECEIVER_BASE") or from_file.get("PLAYTEST_RECEIVER_BASE") or DEFAULT_BASE
    key = os.environ.get("PLAYTEST_ADMIN_KEY") or from_file.get("PLAYTEST_ADMIN_KEY") or ""
    return base.rstrip("/"), key


def relay(handler: BaseHTTPRequestHandler, env_file: Path, steam_id: str, report_id: str, name: str, content_type: str) -> None:
    base, key = receiver_settings(env_file)
    if not key:
        print(f"[dashboard] 503 管理キーが無く R2 の {name} を取れない（PLAYTEST_ADMIN_KEY か {env_file}）", file=sys.stderr)
        handler.send_error(503)
        return
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
        handler.send_error(error.code if error.code in (404, 410, 416) else 502)
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


def copy_body(handler: BaseHTTPRequestHandler, response, name: str) -> None:
    # ブラウザはシークのたびに接続を切るため、送信途中の切断はネットワーク境界の正常系として隔離する
    # Browsers drop the connection on every seek, so a mid-send disconnect is isolated as a normal network-boundary event
    try:
        while chunk := response.read(CHUNK):
            handler.wfile.write(chunk)
    except (BrokenPipeError, ConnectionResetError) as error:
        print(f"[dashboard] 中継途中でクライアントが切断: {name} {error}", file=sys.stderr)
