"""プレイ報告と自動修正ランを全期間ぶん読み、ダッシュボード用の行へ畳む（標準ライブラリのみ）。

投入状態の判定は digest_schema の MANIFEST_SCHEMA と digest_candidates の順序をそのまま使い、表示用の項目は display_fields で別に読む。
読めない箱も一覧から消さず、理由付きの broken 行として返す（ダイジェストと同じく無音で落とさない）。

Reads every play report and auto-fix run and flattens them into dashboard rows (stdlib only).
Triage reuses digest_schema's MANIFEST_SCHEMA and digest_candidates' order; display fields are read separately via display_fields.
Unreadable boxes stay listed as broken rows with a reason, never dropped silently (same contract as the digest).
"""
from __future__ import annotations

import re
from pathlib import Path

import digest_schema as schema
from digest_collect import jst_date
from display_fields import pick, warn

# 詳細画面から開けるファイル。media.py の配信許可と同じ一覧を使う
# Files the detail view can open; media.py serves exactly this allowlist
MEDIA_FILES = ("screenshot.png", "video.mp4", "logs/unity.log", "manifest.json")
COMMIT_RE = re.compile(r"^[0-9a-f]{7,40}$")

TRIAGE_CANDIDATE = "candidate"
TRIAGE_QUEUED = "queued"
TRIAGE_EXCLUDED = "excluded"
TRIAGE_NOT_BUG = "notBug"
TRIAGE_BROKEN = "broken"


def load_runs(runs_root: Path) -> dict[str, dict]:
    """自動修正ランを報告 id で引ける形にする。fix-result.json が無いランは noResult（実行中か異常終了か区別できない）
    Indexes auto-fix runs by report id; a run without fix-result.json is noResult (running and crashed look the same)"""
    runs: dict[str, dict] = {}
    for run_dir in sorted(p for p in runs_root.glob("*") if p.is_dir()):
        result_path = run_dir / "fix-result.json"
        if not result_path.is_file():
            runs[run_dir.name] = {"id": run_dir.name, "status": "noResult", "prNumber": None, "summary": "", "finishedAt": ""}
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
    """全報告を新しい順で返す
    Returns every report newest first"""
    rows = [read_report_box(ingest_path.parent, runs) for ingest_path in sorted(reports_root.glob("*/*/ingest.json"))]
    rows.sort(key=lambda row: row["readyAt"], reverse=True)
    return rows


def read_report_box(box: Path, runs: dict[str, dict]) -> dict:
    meta, ingest_reason = schema.read_conformed(box / "ingest.json", schema.INGEST_SCHEMA)
    row = base_row(box, meta, runs)
    # ingest.json が読めない箱はダイジェストも候補から外すので、ここでも投入対象にしない
    # The digest drops boxes with an unreadable ingest.json from candidates, so they are never enqueue targets here either
    if meta is None:
        return broken(row, ingest_reason or "ingest.json を読めない", box / "ingest.json")
    manifest_path = box / "manifest.json"
    if not manifest_path.is_file():
        return broken(row, "manifest.json が無い（全ファイル見送りの箱）", manifest_path)
    manifest, manifest_reason = schema.read_conformed(manifest_path, schema.MANIFEST_SCHEMA)
    if manifest is None:
        return broken(row, manifest_reason or "manifest.json の型が想定外", manifest_path)
    raw, _ = schema.read_json(manifest_path)
    remote_state, remote_reason = schema.remote_exec_state(manifest)
    return dict(row, kind=manifest["kind"], description=manifest["description"],
                buildLabel=manifest["buildInfo"]["steamBuildLabel"], remoteExec=remote_state, remoteExecReason=remote_reason,
                triage=classify(manifest["kind"], remote_state, row["queued"]), **display_manifest(raw, manifest_path))


def base_row(box: Path, meta: dict | None, runs: dict[str, dict]) -> dict:
    meta = meta or {}
    report_id = meta.get("id") or box.name
    ready_at = meta.get("readyAt") or meta.get("ingestedAt") or ""
    return {
        "id": report_id, "steamId": meta.get("steamId") or box.parent.name,
        "boxSteamId": box.parent.name, "boxId": box.name,
        "testerName": meta.get("steamPersonaName", "").strip(), "profileUrl": meta.get("steamProfileUrl", ""),
        "readyAt": ready_at, "date": jst_date(ready_at), "queued": (box / "AUTOFIX_QUEUED").is_file(),
        "media": [name for name in MEDIA_FILES if (box / name).is_file()],
        "run": runs.get(report_id), "problem": None, "kind": "", "description": "", "buildLabel": "",
    }


def broken(row: dict, reason: str, path: Path) -> dict:
    warn(reason, path)
    return dict(row, triage=TRIAGE_BROKEN, problem=reason)


def display_manifest(raw: dict, source: Path) -> dict:
    """表示だけに使う項目。commit は buildInfo を優先し、空の旧い箱は repository.commit へ落とす（16進以外は捨てる）
    Display-only fields; commit prefers buildInfo and falls back to repository.commit for older boxes (non-hex is dropped)"""
    commit = pick(raw, "buildInfo.commit", (str,), source) or pick(raw, "repository.commit", (str,), source) or ""
    if commit and not COMMIT_RE.match(commit):
        warn(f"commit が16進でないためリンクにしない: {commit[:40]!r}", source)
        commit = ""
    return {
        "createdAt": pick(raw, "createdAt", (str,), source) or "",
        "platform": pick(raw, "platform", (str,), source) or "",
        "videoSeconds": pick(raw, "videoSeconds", (int, float), source),
        "reportTick": pick(raw, "reportTick", (int,), source),
        "uiState": pick(raw, "clientState.uiState", (str,), source) or "",
        "commit": commit,
    }


def classify(kind: str, remote_state: str, queued: bool) -> str:
    """digest_candidates と同じ順で判定する（バグ以外→遠隔実行あり/不明→投入済み→候補）
    Mirrors digest_candidates' order: non-bug, then remote exec enabled/unknown, then queued, else candidate"""
    if kind != "bug":
        return TRIAGE_NOT_BUG
    if remote_state != schema.REMOTE_EXEC_DISABLED:
        return TRIAGE_EXCLUDED
    return TRIAGE_QUEUED if queued else TRIAGE_CANDIDATE
