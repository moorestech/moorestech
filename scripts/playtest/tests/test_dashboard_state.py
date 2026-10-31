#!/usr/bin/env python3
"""ダッシュボードの既読・チケットリンクの書き込み（POST）を HTTP 越しに検証する。
Checks the dashboard's read-mark and ticket-link writes (POST) over HTTP.
"""
import json
import sys
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from digest_fixture import SCRIPTS, build_fixture

sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(SCRIPTS / "dashboard"))
import server  # noqa: E402

KEY = {"steamId": "7656001", "id": "20260912_100000_bug1"}
JSON_HEADERS = {"Content-Type": "application/json", "X-Playtest-Dashboard": "1"}


class DashboardStateTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        build_fixture(self.root)
        server.Config.logs, server.Config.master = self.root, self.root / "no-master"
        self.httpd = ThreadingHTTPServer(("127.0.0.1", 0), server.DashboardHandler)
        port = self.httpd.server_address[1]
        server.Config.allowed_hosts = frozenset({f"127.0.0.1:{port}"})
        threading.Thread(target=self.httpd.serve_forever, daemon=True).start()
        self.base = f"http://127.0.0.1:{port}/playtest"
        self.state_file = self.root / ".state/playtest-dashboard-state.json"

    def tearDown(self):
        self.httpd.shutdown()
        self.httpd.server_close()
        self.temp.cleanup()

    def post(self, route, body, headers=None):
        request = urllib.request.Request(f"{self.base}/api/{route}", data=json.dumps(body).encode("utf-8"),
                                         headers=JSON_HEADERS if headers is None else headers, method="POST")
        try:
            with urllib.request.urlopen(request) as response:
                return response.status, json.loads(response.read())
        except urllib.error.HTTPError as error:
            with error:
                return error.code, json.loads(error.read() or b"{}")

    def report(self):
        with urllib.request.urlopen(f"{self.base}/api/data") as response:
            reports = json.loads(response.read())["reports"]
        return next(r for r in reports if r["boxId"] == KEY["id"])

    def test_read_mark_round_trip(self):
        self.assertIsNone(self.report()["readAt"])
        self.assertEqual(self.post("read", {"items": [KEY], "read": True})[0], 200)
        self.assertIsNotNone(self.report()["readAt"])
        self.assertEqual(self.post("read", {"items": [KEY], "read": False})[0], 200)
        self.assertIsNone(self.report()["readAt"])

    def test_links_add_dedupe_and_remove(self):
        url = "https://www.notion.so/ticket-1"
        self.assertEqual(self.post("links/add", dict(KEY, url=url, title="旧題"))[0], 200)
        self.assertEqual(self.post("links/add", dict(KEY, url=url, title="新題"))[0], 200)
        self.assertEqual([(l["url"], l["title"]) for l in self.report()["links"]], [(url, "新題")])
        self.assertEqual(self.post("links/remove", dict(KEY, url=url))[0], 200)
        self.assertEqual(self.report()["links"], [])

    def test_rejects_cross_site_and_bad_input(self):
        self.assertEqual(self.post("read", {"items": [KEY], "read": True}, {"Content-Type": "application/json"})[0], 403)
        self.assertEqual(self.post("read", {"items": [KEY], "read": True}, dict(JSON_HEADERS, Origin="https://evil.example"))[0], 403)
        self.assertEqual(self.post("read", {"items": [KEY], "read": True}, {"Content-Type": "text/plain", "X-Playtest-Dashboard": "1"})[0], 403)
        self.assertEqual(self.post("read", {"items": [{"steamId": "..", "id": "x"}], "read": True})[0], 400)
        self.assertEqual(self.post("read", {"items": [dict(KEY, id="20260912_missing")], "read": True})[0], 400)
        self.assertEqual(self.post("read", {"items": [KEY], "read": "yes"})[0], 400)
        self.assertEqual(self.post("read", {"items": [], "read": True})[0], 400)
        self.assertEqual(self.post("links/remove", dict(KEY, url="https://never.example"))[0], 404)
        self.assertEqual(self.post("links/add", dict(KEY, url="javascript:alert(1)", title="x"))[0], 400)
        self.assertEqual(self.post("links/add", dict(KEY, url="https://x.example/a b", title="x"))[0], 400)
        self.assertFalse(self.state_file.exists())

    def test_bulk_read_is_all_or_nothing(self):
        other = {"steamId": "7656001", "id": "20260912_101000_bug2"}
        missing = {"steamId": "7656001", "id": "20260912_missing"}
        self.assertEqual(self.post("read", {"items": [KEY, missing], "read": True})[0], 400)
        self.assertIsNone(self.report()["readAt"])
        self.assertEqual(self.post("read", {"items": [KEY, other], "read": True})[0], 200)
        self.assertIsNotNone(self.report()["readAt"])

    def test_mistyped_state_file_is_refused_not_crashed(self):
        self.state_file.parent.mkdir(parents=True)
        self.state_file.write_text(json.dumps({"read": {}, "links": {"a/b": "oops"}}), encoding="utf-8")
        self.assertEqual(self.post("links/add", dict(KEY, url="https://x.example", title="t"))[0], 500)

    def test_broken_state_file_is_never_overwritten(self):
        self.state_file.parent.mkdir(parents=True)
        self.state_file.write_text("{broken", encoding="utf-8")
        self.assertEqual(self.post("read", {"items": [KEY], "read": True})[0], 500)
        self.assertEqual(self.state_file.read_text(encoding="utf-8"), "{broken")
        self.assertIsNone(self.report()["readAt"])


if __name__ == "__main__":
    unittest.main()
