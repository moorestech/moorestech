#!/usr/bin/env python3
"""プレイテストダッシュボードの集計・メディア配信・HTTP 経路を検証する。
Checks the playtest dashboard's collection, media serving and HTTP routes.
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
from digest_fixture import DISABLED_MARK, SCRIPTS, build_fixture, write_ingest, write_json

sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(SCRIPTS / "dashboard"))
import collect_progress  # noqa: E402
import collect_reports  # noqa: E402
import media  # noqa: E402
import server  # noqa: E402


class DashboardCollectTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        build_fixture(self.root)
        self.reports_root = self.root / "harness/playtest/reports"
        self.runs_root = self.root / "harness/bug-report/runs"

    def tearDown(self):
        self.temp.cleanup()

    def add_report(self, steam_id, report_id, manifest):
        box = self.reports_root / steam_id / report_id
        write_ingest(box / "ingest.json", {"kind": "report", "steamId": steam_id, "id": report_id,
                                           "readyAt": "2026-09-13T05:00:00Z", "ingestedAt": "2026-09-13T05:01:00Z"})
        if manifest is not None:
            write_json(box / "manifest.json", manifest)
        return box

    def reports_by_id(self):
        runs = collect_reports.load_runs(self.runs_root)
        return {r["id"]: r for r in collect_reports.load_reports(self.reports_root, runs)}

    def test_triage_matches_digest_candidate_rules(self):
        self.add_report("7656005", "20260913_unknown", {"kind": "bug", "description": "印なし"})
        rows = self.reports_by_id()
        self.assertEqual(rows["20260912_100000_bug1"]["triage"], "candidate")
        self.assertEqual(rows["20260912_101000_bug2"]["triage"], "queued")
        self.assertEqual(rows["20260912_110000_fb1"]["triage"], "notBug")
        self.assertEqual(rows["20260913_unknown"]["triage"], "excluded")

    def test_broken_boxes_stay_listed_with_reason(self):
        self.add_report("7656005", "20260913_nomanifest", None)
        self.add_report("7656005", "20260913_badtype", {"kind": 3, "remoteExec": DISABLED_MARK})
        rows = self.reports_by_id()
        self.assertEqual(rows["20260913_nomanifest"]["triage"], "broken")
        self.assertIn("manifest.json が無い", rows["20260913_nomanifest"]["problem"])
        self.assertEqual(rows["20260913_badtype"]["triage"], "broken")
        self.assertIn("kind", rows["20260913_badtype"]["problem"])

    def test_unreadable_ingest_is_broken_not_candidate(self):
        box = self.add_report("7656005", "20260913_badingest", {"kind": "bug", "remoteExec": DISABLED_MARK})
        (box / "ingest.json").write_text("{broken", encoding="utf-8")
        row = next(r for r in self.reports_by_id().values() if r["boxId"] == "20260913_badingest")
        self.assertEqual(row["triage"], "broken")
        self.assertIn("JSON", row["problem"])

    def test_mistyped_display_field_keeps_triage(self):
        self.add_report("7656005", "20260913_tick", {"kind": "bug", "remoteExec": DISABLED_MARK, "reportTick": "123",
                                                      "repository": {"commit": "../../x"}})
        row = self.reports_by_id()["20260913_tick"]
        self.assertEqual(row["triage"], "candidate")
        self.assertIsNone(row["reportTick"])
        self.assertEqual(row["commit"], "")

    def test_commit_falls_back_to_repository(self):
        self.add_report("7656005", "20260913_repo", {"kind": "bug", "remoteExec": DISABLED_MARK,
                                                      "repository": {"commit": "24226d1ac30574f2c74e75cc179f6be4d6884d54"}})
        self.assertEqual(self.reports_by_id()["20260913_repo"]["commit"], "24226d1ac30574f2c74e75cc179f6be4d6884d54")

    def test_queued_report_links_its_fix_run(self):
        write_json(self.runs_root / "20260912_101000_bug2" / "fix-result.json",
                   {"status": "fixed", "pr_number": 1500, "summary": "直した", "finishedAt": "2026-09-13T00:00:00Z"})
        (self.runs_root / "20260912_100000_bug1").mkdir(parents=True)
        rows = self.reports_by_id()
        self.assertEqual(rows["20260912_101000_bug2"]["run"]["prNumber"], 1500)
        self.assertEqual(rows["20260912_100000_bug1"]["run"]["status"], "noResult")

    def test_sessions_keep_remote_exec_flag_and_count_invalid(self):
        box = self.root / "harness/playtest/progress/7656009/20260913_bad"
        write_ingest(box / "ingest.json", {"kind": "progress", "steamId": "7656009", "id": "20260913_bad",
                                           "readyAt": "2026-09-13T00:00:00Z", "ingestedAt": ""})
        write_json(box / "record.json", {"schemaVersion": 2, "playSeconds": 1.0})
        sessions, invalid = collect_progress.load_sessions(self.root / "harness/playtest/progress")
        self.assertEqual(invalid, 1)
        self.assertEqual({s["id"] for s in sessions}, {"20260912_130000_pg1", "20260912_140000_pg2"})
        self.assertTrue(all(s["remoteExec"] is False for s in sessions))


class DashboardMediaTest(unittest.TestCase):
    def test_resolve_media_rejects_unsafe_and_unlisted(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "765" / "r1").mkdir(parents=True)
            (root / "765" / "r1" / "screenshot.png").write_bytes(b"png")
            (root / "765" / "r1" / "READY").write_text("{}")
            self.assertIsNotNone(media.resolve_media(root, "765", "r1", "screenshot.png"))
            self.assertIsNone(media.resolve_media(root, "765", "r1", "READY"))
            self.assertIsNone(media.resolve_media(root, "..", "r1", "screenshot.png"))
            self.assertIsNone(media.resolve_media(root, "765", "r1", "video.mp4"))

    def test_parse_range(self):
        self.assertEqual(media.parse_range(None, 100), (0, 99))
        self.assertEqual(media.parse_range("bytes=10-19", 100), (10, 19))
        self.assertEqual(media.parse_range("bytes=90-", 100), (90, 99))
        self.assertEqual(media.parse_range("bytes=-10", 100), (90, 99))
        self.assertEqual(media.parse_range("bytes=50-500", 100), (50, 99))
        for bad in ("bytes=100-", "bytes=-0", "bytes=20-10", "bytes=0-1,5-6", "items=0-1"):
            self.assertIsNone(media.parse_range(bad, 100)[0], bad)


class DashboardHttpTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        root = Path(self.temp.name)
        build_fixture(root)
        server.Config.logs, server.Config.master = root, root / "no-master"
        self.httpd = ThreadingHTTPServer(("127.0.0.1", 0), server.DashboardHandler)
        port = self.httpd.server_address[1]
        server.Config.allowed_hosts = frozenset({f"127.0.0.1:{port}"})
        threading.Thread(target=self.httpd.serve_forever, daemon=True).start()
        self.base = f"http://127.0.0.1:{port}"

    def tearDown(self):
        self.httpd.shutdown()
        self.httpd.server_close()
        self.temp.cleanup()

    def status(self, path, headers=None):
        try:
            with urllib.request.urlopen(urllib.request.Request(self.base + path, headers=headers or {})) as response:
                return response.status, response.headers, response.read()
        except urllib.error.HTTPError as error:
            with error:
                return error.code, error.headers, b""

    def test_data_endpoint_returns_all_sections_with_csp(self):
        code, headers, body = self.status("/playtest/api/data")
        self.assertEqual(code, 200)
        self.assertIn("script-src 'self'", headers["Content-Security-Policy"])
        data = json.loads(body)
        self.assertEqual(len(data["reports"]), 5)
        self.assertEqual(len(data["sessions"]), 2)
        self.assertEqual(data["master"], {"challenges": [], "research": {}})

    def test_traversal_and_unknown_routes_are_404(self):
        for path in ("/playtest/static/..%2Fserver.py", "/playtest/media/..%2F..%2F/x/video.mp4",
                     "/playtest/api/digest/..%2F..%2Fx", "/playtest/media/7656001/20260912_100000_bug1/manifest.json/x",
                     "/other"):
            self.assertEqual(self.status(path)[0], 404, path)

    def test_foreign_host_and_nul_are_rejected(self):
        self.assertEqual(self.status("/playtest/api/data", {"Host": "attacker.example"})[0], 421)
        self.assertEqual(self.status("/playtest/static/%00")[0], 404)

    def test_media_carries_security_headers(self):
        code, headers, _ = self.status("/playtest/media/7656001/20260912_100000_bug1/manifest.json")
        self.assertEqual(code, 200)
        self.assertEqual(headers["X-Content-Type-Options"], "nosniff")

    def test_index_and_redirect(self):
        self.assertEqual(self.status("/playtest/")[0], 200)
        self.assertEqual(self.status("/playtest/media/7656001/20260912_100000_bug1/manifest.json")[0], 200)


if __name__ == "__main__":
    unittest.main()
