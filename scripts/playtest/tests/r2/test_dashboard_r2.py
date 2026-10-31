#!/usr/bin/env python3
"""ダッシュボードが R2 にだけある報告ファイルを一覧・解決・中継することを、偽の受け口（ローカル HTTP）で検証する。
Checks that the dashboard lists, resolves and relays report files living only in R2, against a fake receiver on local HTTP.
"""
import json
import sys
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from unittest import mock

TESTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TESTS))
from digest_fixture import DISABLED_MARK, SCRIPTS, write_ingest, write_json  # noqa: E402

sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(SCRIPTS / "dashboard"))
import collect_reports  # noqa: E402
import media  # noqa: E402
import server  # noqa: E402

VIDEO = b"0123456789"
ADMIN_KEY = "test-admin-key"


def add_box(reports_root: Path, steam_id: str, report_id: str) -> Path:
    box = reports_root / steam_id / report_id
    write_ingest(box / "ingest.json", {"kind": "report", "steamId": steam_id, "id": report_id,
                                       "readyAt": "2026-10-30T05:00:00Z", "ingestedAt": "2026-10-30T05:01:00Z"})
    write_json(box / "manifest.json", {"kind": "bug", "remoteExec": DISABLED_MARK, "description": "x"})
    return box


class FakeReceiver(BaseHTTPRequestHandler):
    """受け口の GET /v1/inbox/... の真似。Range・410（片付け済み）・500 を返し、受けた要求を記録する
    Mimics the receiver's GET /v1/inbox/...: answers Range, 410 (purged) and 500, recording each request"""
    seen: list = []

    def do_GET(self):  # noqa: N802
        FakeReceiver.seen.append((self.path, dict(self.headers)))
        if self.headers.get("X-Admin-Key") != ADMIN_KEY:
            return self.send_error(401)
        if "/purged/" in self.path:
            return self.answer(410, b'{"reason":"purged"}', {})
        if "/broken/" in self.path:
            return self.answer(500, b"boom", {})
        if self.headers.get("Range") == "bytes=2-5":
            return self.answer(206, VIDEO[2:6], {"Content-Range": f"bytes 2-5/{len(VIDEO)}", "Accept-Ranges": "bytes"})
        return self.answer(200, VIDEO, {"Accept-Ranges": "bytes"})

    def answer(self, status, body, headers):
        self.send_response(status)
        for name, value in headers.items():
            self.send_header(name, value)
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *args):  # noqa: A002
        pass


class RemoteMediaTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.reports = Path(self.temp.name) / "harness/playtest/reports"

    def test_remote_json_or_ready_files_put_video_in_media(self):
        box = add_box(self.reports, "765", "with_remote")
        (box / "screenshot.png").write_bytes(b"png")
        write_json(box / "remote.json", {"files": ["video.mp4", "world/save.json", "../escape/video.mp4"]})
        legacy = add_box(self.reports, "765", "ready_only")
        write_json(legacy / "READY", {"files": ["manifest.json", "video.mp4"]})
        add_box(self.reports, "765", "nothing_remote")
        rows = {row["id"]: row for row in collect_reports.load_reports(self.reports)}
        self.assertEqual(rows["with_remote"]["media"], ["screenshot.png", "video.mp4", "manifest.json"])
        self.assertIn("video.mp4", rows["ready_only"]["media"])
        self.assertNotIn("video.mp4", rows["nothing_remote"]["media"])

    def test_resolve_media_tells_local_from_remote(self):
        box = add_box(self.reports, "765", "r1")
        (box / "screenshot.png").write_bytes(b"png")
        write_json(box / "remote.json", {"files": ["video.mp4", "logs/unity.log"]})
        self.assertEqual(media.resolve_media(self.reports, "765", "r1", "screenshot.png"), ("local", box / "screenshot.png"))
        self.assertEqual(media.resolve_media(self.reports, "765", "r1", "video.mp4"), ("remote", None))
        self.assertEqual(media.resolve_media(self.reports, "765", "r1", "logs/unity.log"), ("remote", None))
        self.assertIsNone(media.resolve_media(self.reports, "765", "missing", "video.mp4"))
        self.assertIsNone(media.resolve_media(self.reports, "765", "r1", "world/save.json"))
        (box / "remote.json").unlink()
        self.assertIsNone(media.resolve_media(self.reports, "765", "r1", "video.mp4"))


class RelayHttpTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        reports = root / "harness/playtest/reports"
        for report_id in ("r1", "purged", "broken"):
            write_json(add_box(reports, "765", report_id) / "remote.json", {"files": ["video.mp4", "logs/unity.log"]})
        FakeReceiver.seen = []
        self.receiver = self.serve(FakeReceiver)
        receiver_port = self.receiver.server_address[1]
        self.env_file = root / "env.sh"
        self.env_file.write_text(f"export PLAYTEST_RECEIVER_BASE='http://127.0.0.1:{receiver_port}'\n"
                                 f"export PLAYTEST_ADMIN_KEY={ADMIN_KEY}\n", encoding="utf-8")
        server.Config.logs, server.Config.env_file, server.Config.master = root, self.env_file, root / "no-master"
        self.dashboard = self.serve(server.DashboardHandler)
        port = self.dashboard.server_address[1]
        server.Config.allowed_hosts = frozenset({f"127.0.0.1:{port}"})
        self.base = f"http://127.0.0.1:{port}/playtest/media/765"
        # 実機の管理キーが環境変数にあっても使わず、env.sh だけを読ませる
        # Ignore any real admin key in the environment so only env.sh is read
        patcher = mock.patch.dict("os.environ", {}, clear=False)
        patcher.start()
        self.addCleanup(patcher.stop)
        for name in ("PLAYTEST_ADMIN_KEY", "PLAYTEST_RECEIVER_BASE"):
            server.r2_relay.os.environ.pop(name, None)

    def serve(self, handler):
        httpd = ThreadingHTTPServer(("127.0.0.1", 0), handler)
        threading.Thread(target=httpd.serve_forever, daemon=True).start()
        self.addCleanup(httpd.server_close)
        self.addCleanup(httpd.shutdown)
        return httpd

    def get(self, path, headers=None):
        try:
            with urllib.request.urlopen(urllib.request.Request(self.base + path, headers=headers or {})) as response:
                return response.status, response.headers, response.read()
        except urllib.error.HTTPError as error:
            with error:
                return error.code, error.headers, b""

    def test_range_is_forwarded_with_key_and_user_agent(self):
        code, headers, body = self.get("/r1/video.mp4", {"Range": "bytes=2-5"})
        self.assertEqual((code, body), (206, VIDEO[2:6]))
        self.assertEqual(headers["Content-Range"], f"bytes 2-5/{len(VIDEO)}")
        self.assertEqual(headers["Content-Type"], "video/mp4")
        path, sent = FakeReceiver.seen[-1]
        self.assertEqual(path, "/v1/inbox/report/765/r1/video.mp4")
        self.assertEqual((sent["Range"], sent["X-Admin-Key"]), ("bytes=2-5", ADMIN_KEY))
        self.assertNotIn("Python-urllib", sent["User-Agent"])

    def test_whole_file_and_nested_name(self):
        self.assertEqual(self.get("/r1/video.mp4")[::2], (200, VIDEO))
        self.assertEqual(self.get("/r1/logs/unity.log")[0], 200)
        self.assertEqual(FakeReceiver.seen[-1][0], "/v1/inbox/report/765/r1/logs/unity.log")

    def test_purged_passes_410_and_receiver_failure_is_502(self):
        self.assertEqual(self.get("/purged/video.mp4")[0], 410)
        self.assertEqual(self.get("/broken/video.mp4")[0], 502)

    def test_missing_admin_key_is_503_without_calling_receiver(self):
        self.env_file.write_text("export SOMETHING_ELSE=1\n", encoding="utf-8")
        self.assertEqual(self.get("/r1/video.mp4")[0], 503)
        server.Config.env_file = Path(self.temp.name) / "absent.sh"
        self.assertEqual(self.get("/r1/video.mp4")[0], 503)
        self.assertEqual(FakeReceiver.seen, [])


if __name__ == "__main__":
    unittest.main()
