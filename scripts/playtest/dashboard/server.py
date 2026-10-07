#!/usr/bin/env python3
"""プレイテストダッシュボードの HTTP サーバー（標準ライブラリのみ・127.0.0.1 待受。報告データは読むだけで、書くのは既読とチケットのリンクだけ）。
外部公開は review.moores.tech の /playtest パスを Cloudflare Access 越しにトンネルで通す前提。

Playtest dashboard HTTP server (stdlib only, binds 127.0.0.1; report data is read-only, only read marks and ticket links are written).
Exposure is via the tunnel on review.moores.tech/playtest behind Cloudflare Access.

Usage: python3 server.py [--port 8932] [--logs <moorestech_logs>] [--master <master dir>]
"""
from __future__ import annotations

import argparse
import hashlib
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
import dashboard_state  # noqa: E402
import media  # noqa: E402
import write_api  # noqa: E402
from security_headers import send_security_headers  # noqa: E402

PREFIX = "/playtest"
STATIC_ROOT = HERE / "static"
STATIC_TYPES = {".html": "text/html; charset=utf-8", ".css": "text/css; charset=utf-8",
                ".js": "text/javascript; charset=utf-8"}
DATE_RE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
# 公開ホスト名以外の Host を拒む（ローカルのブラウザ経由の DNS リバインディングで Access を迂回させないため）
# Rejects any Host but the known names, so DNS rebinding through a local browser cannot bypass Access
PUBLIC_HOST = "review.moores.tech"


class Config:
    logs = Path()
    master = Path()
    allowed_hosts: frozenset = frozenset()


def app_version() -> str:
    """画面ファイル群の版。開いたままのページがこれの変化で古いコードと気づき、読み込み直す
    Version of the static files; an open page notices a change and reloads instead of running stale code"""
    stamp = sorted((str(p.relative_to(STATIC_ROOT)), p.stat().st_mtime_ns, p.stat().st_size)
                   for p in STATIC_ROOT.rglob("*") if p.is_file() and not p.name.startswith("."))
    return hashlib.sha256(repr(stamp).encode("utf-8")).hexdigest()[:16]


def build_payload() -> dict:
    playtest = Config.logs / "harness" / "playtest"
    runs = collect_reports.load_runs(Config.logs / "harness" / "bug-report" / "runs")
    sessions, invalid_sessions = collect_progress.load_sessions(playtest / "progress")
    digests = sorted((p.stem for p in (playtest / "digests").glob("*.md") if DATE_RE.match(p.stem)), reverse=True)
    return {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "appVersion": app_version(),
        "reports": with_human_state(collect_reports.load_reports(playtest / "reports", runs), state_path()),
        "sessions": sessions, "invalidSessions": invalid_sessions,
        "runs": sorted(runs.values(), key=lambda run: run["id"], reverse=True),
        "digests": digests, "master": master_names.load_master_names(Config.master),
    }


def state_path() -> Path:
    # git 管理外（.state/）に置く理由は dashboard_state のモジュール説明を参照
    # Kept outside git (.state/); see dashboard_state's module docstring for why
    return Config.logs / ".state" / "playtest-dashboard-state.json"


def with_human_state(reports: list[dict], path: Path) -> list[dict]:
    """人が付けた既読とチケットのリンクを各報告へ載せる
    Attaches the human-made read marks and ticket links to each report"""
    state = dashboard_state.load_state_for_display(path)
    for report in reports:
        key = f"{report['boxSteamId']}/{report['boxId']}"
        report["readAt"] = state["read"].get(key) if isinstance(state["read"].get(key), str) else None
        links = state["links"].get(key)
        report["links"] = [link for link in links if isinstance(link, dict)] if isinstance(links, list) else []
    return reports


class DashboardHandler(BaseHTTPRequestHandler):
    def do_POST(self) -> None:  # noqa: N802 (http.server の規約名 / http.server naming)
        if self.headers.get("Host", "") not in Config.allowed_hosts:
            return self.reject(421, f"許可外の Host: {self.headers.get('Host', '')!r}")
        path = urlsplit(self.path).path
        if not path.startswith(PREFIX + "/api/"):
            return self.not_found("書き込みルート外")
        reports_root = Config.logs / "harness" / "playtest" / "reports"
        status, body = write_api.handle_post(self, path[len(PREFIX):], reports_root, state_path(), Config.allowed_hosts)
        self.send_bytes(json.dumps(body, ensure_ascii=False).encode("utf-8"), "application/json; charset=utf-8", "no-store", status)

    def do_GET(self) -> None:  # noqa: N802 (http.server の規約名 / http.server naming)
        if self.headers.get("Host", "") not in Config.allowed_hosts:
            return self.reject(421, f"許可外の Host: {self.headers.get('Host', '')!r}")
        path = unquote(urlsplit(self.path).path)
        if "\x00" in path:
            return self.not_found("パスに NUL を含む")
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
        # 端末やトンネル途中のキャッシュに古い画面ファイルを残さない（反映後も古いコードが動き続けるのを防ぐ）
        # Never leave old static files in device or tunnel caches, so stale code does not keep running after a deploy
        self.send_bytes(target.read_bytes(), STATIC_TYPES[target.suffix], "no-store", 200)

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
        self.send_bytes(json.dumps(data, ensure_ascii=False, allow_nan=False).encode("utf-8"), "application/json; charset=utf-8", "no-store", 200)

    def send_bytes(self, body: bytes, content_type: str, cache: str, status: int) -> None:
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", cache)
        send_security_headers(self)
        self.end_headers()
        self.wfile.write(body)

    def redirect(self, location: str) -> None:
        self.send_response(302)
        self.send_header("Location", location)
        self.end_headers()

    def not_found(self, reason: str) -> None:
        self.reject(404, reason)

    def reject(self, status: int, reason: str) -> None:
        print(f"[dashboard] {status} {reason}: {self.path}", file=sys.stderr)
        self.send_error(status)

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
    Config.allowed_hosts = frozenset({PUBLIC_HOST, f"127.0.0.1:{args.port}", f"localhost:{args.port}"})
    server = ThreadingHTTPServer(("127.0.0.1", args.port), DashboardHandler)
    print(f"playtest dashboard: http://127.0.0.1:{args.port}{PREFIX}/ (logs={Config.logs})")
    server.serve_forever()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
