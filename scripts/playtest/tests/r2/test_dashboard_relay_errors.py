#!/usr/bin/env python3
"""R2 中継の異常系（受け口の 416・本文の途切れ・起動時の設定読み）を偽の受け口で検証する。
Checks the R2 relay's failure paths (receiver 416, truncated body, startup settings) against a fake receiver.
"""
import http.client
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
sys.path.insert(0, str(TESTS / "r2"))
from digest_fixture import SCRIPTS, write_json  # noqa: E402

sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(SCRIPTS / "dashboard"))
import r2_relay  # noqa: E402
import server  # noqa: E402
from test_dashboard_r2 import ADMIN_KEY, add_box  # noqa: E402


class BrokenReceiver(BaseHTTPRequestHandler):
    """416 は Content-Range 付き、truncated は長さ 10 を名乗って 4 バイトで切る
    Answers 416 with Content-Range, and for truncated claims 10 bytes but closes after 4"""

    def do_GET(self):  # noqa: N802
        if "/unsatisfiable/" in self.path:
            self.send_response(416)
            self.send_header("Content-Range", "bytes */10")
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        self.send_response(200)
        self.send_header("Content-Length", "10")
        self.end_headers()
        self.wfile.write(b"0123")
        self.wfile.flush()
        self.close_connection = True

    def log_message(self, format, *args):  # noqa: A002
        pass


class RelayFailureTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        for report_id in ("unsatisfiable", "truncated"):
            write_json(add_box(root / "harness/playtest/reports", "765", report_id) / "remote.json", {"files": ["video.mp4"]})
        receiver = self.serve(BrokenReceiver)
        server.Config.logs, server.Config.master = root, root / "no-master"
        server.Config.receiver = r2_relay.load_receiver_settings(
            {"PLAYTEST_RECEIVER_BASE": f"http://127.0.0.1:{receiver.server_address[1]}", "PLAYTEST_ADMIN_KEY": ADMIN_KEY})
        dashboard = self.serve(server.DashboardHandler)
        server.Config.allowed_hosts = frozenset({f"127.0.0.1:{dashboard.server_address[1]}"})
        self.base = f"http://127.0.0.1:{dashboard.server_address[1]}/playtest/media/765"

    def serve(self, handler):
        httpd = ThreadingHTTPServer(("127.0.0.1", 0), handler)
        threading.Thread(target=httpd.serve_forever, daemon=True).start()
        self.addCleanup(httpd.server_close)
        self.addCleanup(httpd.shutdown)
        return httpd

    def test_416_passes_content_range(self):
        request = urllib.request.Request(self.base + "/unsatisfiable/video.mp4", headers={"Range": "bytes=50-60"})
        with self.assertRaises(urllib.error.HTTPError) as caught, mock.patch("sys.stderr"):
            urllib.request.urlopen(request)
        with caught.exception as error:
            self.assertEqual(error.code, 416)
            self.assertEqual(error.headers["Content-Range"], "bytes */10")

    def test_truncated_upstream_is_logged_and_cut_short(self):
        with mock.patch("sys.stderr") as stderr:
            with urllib.request.urlopen(self.base + "/truncated/video.mp4") as response:
                with self.assertRaises(http.client.IncompleteRead):
                    response.read()
        logged = "".join(call.args[0] for call in stderr.write.call_args_list)
        self.assertIn("途中で途切れた: video.mp4 4/10 bytes", logged)


class StartupSettingsTest(unittest.TestCase):
    def test_reads_only_environment_and_lists_missing(self):
        settings = r2_relay.load_receiver_settings({"PLAYTEST_RECEIVER_BASE": "https://example.invalid/", "PLAYTEST_ADMIN_KEY": "k"})
        self.assertEqual(settings, ("https://example.invalid", "k", ()))
        self.assertEqual(r2_relay.load_receiver_settings({}).missing, ("PLAYTEST_RECEIVER_BASE", "PLAYTEST_ADMIN_KEY"))


if __name__ == "__main__":
    unittest.main()
