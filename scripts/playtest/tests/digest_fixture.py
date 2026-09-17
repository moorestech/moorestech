#!/usr/bin/env python3
"""日次ダイジェストのテスト用 fixture 木の構築と CLI 実行ヘルパー。

Builds the fixture tree for the daily digest tests and runs the CLI.
"""
import json
import subprocess
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent
TARGET_DATE = "2026-09-12"


def write_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data), encoding="utf-8")


def build_fixture(root: Path) -> None:
    """前日(2026-09-12 JST)分と対象外の日を1件ずつ混ぜた木を作る
    Builds a tree with boxes for the target day (2026-09-12 JST) plus one out-of-range box"""
    pt = root / "harness" / "playtest"
    bug = pt / "reports" / "7656001" / "20260912_100000_bug1"
    write_json(bug / "ingest.json", {"kind": "report", "steamId": "7656001",
                                     "id": "20260912_100000_bug1", "readyAt": "2026-09-12T05:00:00Z",
                                     "ingestedAt": "2026-09-12T05:01:00Z"})
    write_json(bug / "manifest.json", {"kind": "bug", "description": "ベルトが止まる\n2個目から",
                                       "buildInfo": {"steamBuildLabel": "playtest-20260912-1730"}})
    bug2 = pt / "reports" / "7656001" / "20260912_101000_bug2"
    write_json(bug2 / "ingest.json", {"kind": "report", "steamId": "7656001",
                                      "id": "20260912_101000_bug2", "readyAt": "2026-09-12T05:10:00Z",
                                      "ingestedAt": "2026-09-12T05:11:00Z"})
    write_json(bug2 / "manifest.json", {"kind": "bug", "description": "投入済みのバグ"})
    (bug2 / "AUTOFIX_QUEUED").write_text("queued at 2026-09-12T06:00:00Z\n", encoding="utf-8")
    fb = pt / "reports" / "7656002" / "20260912_110000_fb1"
    write_json(fb / "ingest.json", {"kind": "report", "steamId": "7656002",
                                    "id": "20260912_110000_fb1", "readyAt": "2026-09-12T06:00:00Z",
                                    "ingestedAt": "2026-09-12T06:01:00Z"})
    write_json(fb / "manifest.json", {"kind": "feedback", "description": "序盤の歩きが長い",
                                      "buildInfo": {"steamBuildLabel": "playtest-20260912-1730"}})
    cr = pt / "reports" / "7656003" / "20260912_120000_cr1"
    write_json(cr / "ingest.json", {"kind": "report", "steamId": "7656003",
                                    "id": "20260912_120000_cr1", "readyAt": "2026-09-12T07:00:00Z",
                                    "ingestedAt": "2026-09-12T07:01:00Z"})
    write_json(cr / "manifest.json", {"kind": "crash", "description": ""})
    old = pt / "reports" / "7656004" / "20260901_100000_old1"
    write_json(old / "ingest.json", {"kind": "report", "steamId": "7656004",
                                     "id": "20260901_100000_old1", "readyAt": "2026-09-01T05:00:00Z",
                                     "ingestedAt": "2026-09-01T05:01:00Z"})
    write_json(old / "manifest.json", {"kind": "feedback", "description": "対象外の日"})

    pg1 = pt / "progress" / "7656001" / "20260912_130000_pg1"
    write_json(pg1 / "ingest.json", {"kind": "progress", "steamId": "7656001",
                                     "id": "20260912_130000_pg1", "readyAt": "2026-09-12T08:00:00Z",
                                     "ingestedAt": "2026-09-12T08:01:00Z"})
    write_json(pg1 / "record.json", {"schemaVersion": 1, "steamId": "7656001", "playSeconds": 1200.0,
                                     "endReason": "quit", "reachedChallenges": ["a", "b", "c"],
                                     "completedResearch": ["r1"], "lastUiState": "GameScreen",
                                     "events": [{"type": "challengeCompleted"}, {"type": "buildModeCancelled"}]})
    pg2 = pt / "progress" / "7656002" / "20260912_140000_pg2"
    write_json(pg2 / "ingest.json", {"kind": "progress", "steamId": "7656002",
                                     "id": "20260912_140000_pg2", "readyAt": "2026-09-12T09:00:00Z",
                                     "ingestedAt": "2026-09-12T09:01:00Z"})
    write_json(pg2 / "record.json", {"schemaVersion": 1, "steamId": "7656002", "playSeconds": 600.0,
                                     "endReason": "crash-recovered", "reachedChallenges": [],
                                     "completedResearch": [], "lastUiState": "InventoryScreen",
                                     "events": [{"type": "buildModeCancelled"}]})

    run = root / "harness" / "bug-report" / "runs" / "20260910_090000_bug0"
    write_json(run / "fix-result.json", {"status": "fixed", "pr_number": 1400, "base": "master",
                                         "determinism": "ok", "summary": "ベルト停止を修正",
                                         "finishedAt": "2026-09-12T10:00:00Z"})


def run_digest(root: Path, date: str, *extra: str) -> subprocess.CompletedProcess:
    """digest.py を実行する。終了コードは呼び出し側で検証する
    Runs digest.py; callers assert the exit code"""
    cmd = [sys.executable, str(SCRIPTS / "digest.py"), "--date", date, "--logs", str(root), *extra]
    return subprocess.run(cmd, capture_output=True, text=True)
