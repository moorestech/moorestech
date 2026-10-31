"""報告箱のスクショ・動画・ログを許可リストどおりに配信する（動画のシーク用に Range に対応）。
手元（logs repo の箱）にあればそれを、R2 にだけある物（動画等）は r2_relay が受け口経由で中継する。
steamId/id はテスター由来の値なので、パスへ連結する前に取り込みと同じ安全セグメント規則で検証する。

Serves a report box's screenshot, video and logs strictly by allowlist, with Range support for video seeking.
Local files (the box in the logs repo) are served directly; files only in R2 (videos etc.) are relayed via the receiver by r2_relay.
steamId/id are tester-supplied, so they pass the same safe-segment rule as ingest before any path join.
"""
from __future__ import annotations

import re
import sys
from http.server import BaseHTTPRequestHandler
from pathlib import Path

from collect_reports import MEDIA_FILES, remote_files
from security_headers import send_security_headers

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "lib"))
from safe_segment import is_safe_segment  # noqa: E402

CONTENT_TYPES = {".png": "image/png", ".mp4": "video/mp4", ".log": "text/plain; charset=utf-8",
                 ".json": "application/json; charset=utf-8"}
RANGE_RE = re.compile(r"^bytes=(\d*)-(\d*)$")
CHUNK = 1 << 20


def resolve_media(reports_root: Path, steam_id: str, report_id: str, name: str) -> tuple[str, Path | None] | None:
    """("local", パス) か R2 にだけある物の ("remote", None)。許可リスト外・危険なセグメント・どこにも無い物は None（呼び出し側が 404）
    Returns ("local", path) or ("remote", None) for R2-only files; None for non-allowlisted names, unsafe segments or absent files"""
    if name not in MEDIA_FILES or not is_safe_segment(steam_id) or not is_safe_segment(report_id):
        return None
    box = reports_root / steam_id / report_id
    if (box / name).is_file():
        return "local", box / name
    return ("remote", None) if box.is_dir() and name in remote_files(box) else None


def content_type(name: str) -> str:
    return CONTENT_TYPES.get(Path(name).suffix, "application/octet-stream")


def send_file(handler: BaseHTTPRequestHandler, path: Path) -> None:
    size = path.stat().st_size
    start, end = parse_range(handler.headers.get("Range"), size)
    if start is None:
        print(f"[dashboard] 416 解釈できない Range: {handler.headers.get('Range')!r} {path.name}", file=sys.stderr)
        handler.send_response(416)
        handler.send_header("Content-Range", f"bytes */{size}")
        handler.end_headers()
        return
    partial = handler.headers.get("Range") is not None
    handler.send_response(206 if partial else 200)
    handler.send_header("Content-Type", content_type(path.name))
    handler.send_header("Accept-Ranges", "bytes")
    handler.send_header("Content-Length", str(end - start + 1))
    handler.send_header("Cache-Control", "private, max-age=300")
    send_security_headers(handler)
    if partial:
        handler.send_header("Content-Range", f"bytes {start}-{end}/{size}")
    handler.end_headers()
    copy_range(handler, path, start, end)


def parse_range(header: str | None, size: int) -> tuple[int | None, int]:
    """単一範囲だけ扱う（ブラウザの動画シークはこれで足りる）。解釈できない範囲は 416 にする
    Handles a single range only, which is all browser video seeking needs; anything else becomes 416"""
    if header is None:
        return 0, size - 1
    match = RANGE_RE.match(header.strip())
    if match is None or (not match.group(1) and not match.group(2)):
        return None, 0
    if not match.group(1):
        suffix = int(match.group(2))
        return (max(0, size - suffix), size - 1) if suffix > 0 and size > 0 else (None, 0)
    start = int(match.group(1))
    end = min(int(match.group(2)), size - 1) if match.group(2) else size - 1
    if start >= size or start > end:
        return None, 0
    return start, end


def copy_range(handler: BaseHTTPRequestHandler, path: Path, start: int, end: int) -> None:
    remaining = end - start + 1
    # ブラウザはシークのたびに接続を切るため、送信途中の切断はネットワーク境界の正常系として隔離する
    # Browsers drop the connection on every seek, so a mid-send disconnect is isolated as a normal network-boundary event
    try:
        with path.open("rb") as source:
            source.seek(start)
            while remaining > 0:
                chunk = source.read(min(CHUNK, remaining))
                if not chunk:
                    break
                handler.wfile.write(chunk)
                remaining -= len(chunk)
    except (BrokenPipeError, ConnectionResetError) as error:
        print(f"[dashboard] 配信途中でクライアントが切断: {path.name} {error}", file=sys.stderr)
