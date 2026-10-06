#!/usr/bin/env python3
"""プレイテストダッシュボードの HTTP サーバー（標準ライブラリのみ・127.0.0.1 待受・読み取り専用）。

Playtest dashboard HTTP server (stdlib only, binds 127.0.0.1, read-only).
外部公開は review.moores.tech の /playtest パスを Cloudflare Access 越しにトンネルで通す前提。
Exposure is via the tunnel on review.moores.tech/playtest behind Cloudflare Access.

Usage: python3 server.py [--port 8932] [--logs <moorestech_logs>] [--master <master dir>]
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(HERE))
import collect_progress  # noqa: E402
import collect_reports  # noqa: E402
import master_names  # noqa: E402
import media  # noqa: E402

PREFIX = "/playtest"
STATIC_ROOT = HERE / "static"
STATIC_TYPES = {".html": "text/html; charset=utf-8", ".css": "text/css; charset=utf-8",
                ".js": "text/javascript; charset=utf-8"}
DATE_RE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
# テスター由来の文字列を表示するページなので、スクリプトは自オリジンの静的ファイルだけに絞る
# The page renders tester-supplied text, so scripts are limited to this origin's static files
CSP = "default-src 'self'; img-src 'self' data:; media-src 'self'; style-src 'self'; script-src 'self'; frame-ancestors 'none'"


class Config:
    logs = Path()
    master = Path()


def build_payload() -> dict:
    playtest = Config.logs / "harness" / "playtest"
    runs = collect_reports.load_runs(Config.logs / "harness" / "bug-report" / "runs")
    sessions, invalid_sessions = collect_progress.load_sessions(playtest / "progress")
    digests = sorted((p.stem for p in (playtest / "digests").glob("*.md") if DATE_RE.match(p.stem)), reverse=True)
    return {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "reports": collect_reports.load_reports(playtest / "reports", runs),
        "sessions": sessions, "invalidSessions": invalid_sessions,
        "runs": sorted(runs.values(), key=lambda run: run["id"], reverse=True),
        "digests": digests, "master": master_names.load_master_names(Config.master),
    }


class DashboardHandler(BaseHTTPRequestHandler):
    def do_GET(self) -> None:  # noqa: N802 (http.server の規約名 / http.server naming)
        path = unquote(urlsplit(self.path).path)
        if path in ("/", PREFIX):
            return self.redirect(PREFIX + "/")
        if not path.startswith(PREFIX + "/"):
            return self.not_found("プレフィックス外")
        route = path[len(PREFIX):]
        if route == "/":
            return self.send_static("index.html")
        if route.startswith("/static/"):
            return self.send_static(route[len("/static/"):])
        if route == "/api/data":
            return self.send_json(build_payload())
        if route.startswith("/api/digest/"):
            return self.send_digest(route[len("/api/digest/"):])
        if route.startswith("/media/"):
            return self.send_media(route[len("/media/"):])
        return self.not_found("未知のルート")

    def send_static(self, relative: str) -> None:
        target = (STATIC_ROOT / relative).resolve()
        if STATIC_ROOT not in target.parents or not target.is_file() or target.suffix not in STATIC_TYPES:
            return self.not_found("静的ファイル外")
        self.send_bytes(target.read_bytes(), STATIC_TYPES[target.suffix], "no-cache")

    def send_digest(self, date: str) -> None:
        archive = Config.logs / "harness" / "playtest" / "digests" / f"{date}.md"
        if not DATE_RE.match(date) or not archive.is_file():
            return self.not_found("ダイジェストが無い")
        self.send_json({"date": date, "markdown": archive.read_text(encoding="utf-8")})

    def send_media(self, rest: str) -> None:
        parts = rest.split("/", 2)
        target = media.resolve_media(Config.logs / "harness" / "playtest" / "reports", *parts) if len(parts) == 3 else None
        if target is None:
            return self.not_found("配信許可外または実在しないメディア")
        media.send_file(self, target)

    def send_json(self, data: dict) -> None:
        self.send_bytes(json.dumps(data, ensure_ascii=False).encode("utf-8"), "application/json; charset=utf-8", "no-store")

    def send_bytes(self, body: bytes, content_type: str, cache: str) -> None:
        self.send_response(200)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", cache)
        self.send_header("Content-Security-Policy", CSP)
        self.send_header("X-Content-Type-Options", "nosniff")
        self.end_headers()
        self.wfile.write(body)

    def redirect(self, location: str) -> None:
        self.send_response(302)
        self.send_header("Location", location)
        self.end_headers()

    def not_found(self, reason: str) -> None:
        print(f"[dashboard] 404 {reason}: {self.path}", file=sys.stderr)
        self.send_error(404)

    def log_message(self, format: str, *args) -> None:  # noqa: A002 (基底の引数名 / base-class signature)
        # 200 系のアクセスログは出さず、404 の理由は not_found が出す
        # Successful requests stay quiet; not_found logs the reason for 404s
        return


def main(argv: list[str] | None = None) -> int:
    repo = HERE.parents[2]
    parser = argparse.ArgumentParser(description="プレイテストダッシュボード / playtest dashboard")
    parser.add_argument("--port", type=int, default=8932)
    parser.add_argument("--logs", default=str(repo.parent / "moorestech_logs"))
    parser.add_argument("--master", default=str(
        repo.parent / "moorestech_master" / "server_v8" / "mods" / "moorestechAlphaMod_8" / "master"))
    args = parser.parse_args(argv)
    Config.logs, Config.master = Path(args.logs), Path(args.master)
    server = ThreadingHTTPServer(("127.0.0.1", args.port), DashboardHandler)
    print(f"playtest dashboard: http://127.0.0.1:{args.port}{PREFIX}/ (logs={Config.logs})")
    server.serve_forever()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
