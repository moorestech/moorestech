"""プレイ報告と自動修正ランを全期間ぶん読み、ダッシュボード用の行へ畳む（標準ライブラリのみ）。

Reads every play report and auto-fix run and flattens them into dashboard rows (stdlib only).
読めない箱も一覧から消さず problem 付きの行として返す（ダイジェストと同じく無音で落とさない）。
Unreadable boxes stay in the list as rows carrying a problem, never dropped silently (same contract as the digest).
"""
from __future__ import annotations

import sys
from pathlib import Path

import digest_schema as schema
from digest_collect import jst_date

# ダッシュボードが追加で読む manifest の項目。判定に使う項目は MANIFEST_SCHEMA をそのまま引き継ぐ
# Extra manifest fields the dashboard shows; fields used for decisions come unchanged from MANIFEST_SCHEMA
DASHBOARD_MANIFEST_SCHEMA = dict(
    schema.MANIFEST_SCHEMA,
    createdAt=(schema.STR, ""),
    platform=(schema.STR, ""),
    videoSeconds=(schema.NUMBER, None),
    reportTick=(schema.INT, None),
    buildInfo=({"steamBuildLabel": (schema.STR, ""), "commit": (schema.STR, "")},
               {"steamBuildLabel": "", "commit": ""}),
    clientState=({"uiState": (schema.STR, "")}, {"uiState": ""}),
)
# 詳細画面から開けるファイル。media.py の配信許可と同じ一覧を使う
# Files the detail view can open; media.py serves exactly this allowlist
MEDIA_FILES = ("screenshot.png", "video.mp4", "logs/unity.log", "manifest.json")

TRIAGE_CANDIDATE = "candidate"
TRIAGE_QUEUED = "queued"
TRIAGE_EXCLUDED = "excluded"
TRIAGE_NOT_BUG = "notBug"
TRIAGE_BROKEN = "broken"


def warn(reason: str, path: Path) -> None:
    print(f"[dashboard] {reason}: {path}", file=sys.stderr)


def load_runs(runs_root: Path) -> dict[str, dict]:
    """自動修正ランを報告 id で引ける形にする。型違いの fix-result.json は status=invalid として残す
    Indexes auto-fix runs by report id; a type-mismatched fix-result.json stays as status=invalid"""
    runs: dict[str, dict] = {}
    for run_dir in sorted(p for p in runs_root.glob("*") if p.is_dir()):
        result_path = run_dir / "fix-result.json"
        if not result_path.is_file():
            runs[run_dir.name] = {"id": run_dir.name, "status": "running", "prNumber": None, "summary": "", "finishedAt": ""}
            continue
        result, reason = schema.read_conformed(result_path, schema.FIX_RESULT_SCHEMA)
        if result is None:
            warn(reason or "fix-result.json の型が想定外", result_path)
            runs[run_dir.name] = {"id": run_dir.name, "status": "invalid", "prNumber": None, "summary": reason or "", "finishedAt": ""}
            continue
        runs[run_dir.name] = {"id": run_dir.name, "status": result["status"] or "missing", "prNumber": result["pr_number"],
                              "summary": result["summary"], "finishedAt": result["finishedAt"]}
    return runs


def load_reports(reports_root: Path, runs: dict[str, dict]) -> list[dict]:
    """全報告を新しい順で返す。triage はダイジェストの投入候補判定と同じ規則で決める
    Returns every report newest first; triage follows the digest's candidate rules"""
    rows = [read_report_box(ingest_path.parent, runs) for ingest_path in sorted(reports_root.glob("*/*/ingest.json"))]
    rows.sort(key=lambda row: row["readyAt"], reverse=True)
    return rows


def read_report_box(box: Path, runs: dict[str, dict]) -> dict:
    meta, reason = schema.read_conformed(box / "ingest.json", schema.INGEST_SCHEMA)
    meta = meta or {"id": box.name, "steamId": box.parent.name, "readyAt": "", "ingestedAt": "",
                    "steamPersonaName": "", "steamProfileUrl": "", "steamPersonaMissing": ""}
    ready_at = meta["readyAt"] or meta["ingestedAt"]
    row = {
        "id": meta["id"] or box.name, "steamId": meta["steamId"] or box.parent.name,
        "boxSteamId": box.parent.name, "boxId": box.name,
        "testerName": meta["steamPersonaName"].strip(), "profileUrl": meta["steamProfileUrl"],
        "readyAt": ready_at, "date": jst_date(ready_at), "queued": (box / "AUTOFIX_QUEUED").is_file(),
        "media": [name for name in MEDIA_FILES if (box / name).is_file()],
        "run": runs.get(meta["id"] or box.name), "problem": reason,
    }
    manifest_path = box / "manifest.json"
    if not manifest_path.is_file():
        return dict(row, kind="", triage=TRIAGE_BROKEN, problem=row["problem"] or "manifest.json が無い（全ファイル見送りの箱）")
    manifest, manifest_reason = schema.read_conformed(manifest_path, DASHBOARD_MANIFEST_SCHEMA)
    if manifest is None:
        warn(manifest_reason or "manifest.json の型が想定外", manifest_path)
        return dict(row, kind="", triage=TRIAGE_BROKEN, problem=manifest_reason)
    remote_state, remote_reason = schema.remote_exec_state(manifest)
    return dict(
        row, kind=manifest["kind"], description=manifest["description"], createdAt=manifest["createdAt"],
        platform=manifest["platform"], videoSeconds=manifest["videoSeconds"], reportTick=manifest["reportTick"],
        buildLabel=manifest["buildInfo"]["steamBuildLabel"], commit=manifest["buildInfo"]["commit"],
        uiState=manifest["clientState"]["uiState"], remoteExec=remote_state, remoteExecReason=remote_reason,
        triage=classify(manifest["kind"], remote_state, row["queued"]),
    )


def classify(kind: str, remote_state: str, queued: bool) -> str:
    """digest_candidates と同じ順で判定する（バグ以外→遠隔実行あり/不明→投入済み→候補）
    Mirrors digest_candidates' order: non-bug, then remote exec enabled/unknown, then queued, else candidate"""
    if kind != "bug":
        return TRIAGE_NOT_BUG
    if remote_state != schema.REMOTE_EXEC_DISABLED:
        return TRIAGE_EXCLUDED
    return TRIAGE_QUEUED if queued else TRIAGE_CANDIDATE
