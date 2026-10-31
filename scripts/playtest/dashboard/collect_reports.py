"""プレイ報告を全期間ぶん読み、ダッシュボード用の行へ畳む（標準ライブラリのみ）。

分類は digest_schema の MANIFEST_SCHEMA で読み、表示用の項目は display_fields で別に読む。
読めない箱も一覧から消さず、理由付きの broken 行として返す（ダイジェストと同じく無音で落とさない）。

Reads every play report and flattens it into dashboard rows (stdlib only).
Classification reads via digest_schema's MANIFEST_SCHEMA; display fields are read separately via display_fields.
Unreadable boxes stay listed as broken rows with a reason, never dropped silently (same contract as the digest).
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

import digest_schema as schema
from digest_collect import jst_date
from display_fields import pick, warn

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "lib"))
from safe_segment import is_safe_segment  # noqa: E402

# 詳細画面から開けるファイル。media.py の配信許可と同じ一覧を使う
# Files the detail view can open; media.py serves exactly this allowlist
MEDIA_FILES = ("screenshot.png", "video.mp4", "logs/unity.log", "manifest.json")
COMMIT_RE = re.compile(r"^[0-9a-f]{7,40}$")

TRIAGE_BUG = "bug"
TRIAGE_EXCLUDED = "excluded"
TRIAGE_NOT_BUG = "notBug"
TRIAGE_BROKEN = "broken"


def load_reports(reports_root: Path) -> list[dict]:
    """全報告を新しい順で返す。動画等の生データは R2 にあり、media.py が受け口経由で中継する
    Returns every report newest first; raw data such as videos lives in R2 and media.py relays it via the receiver"""
    rows = [read_report_box(ingest_path.parent) for ingest_path in sorted(reports_root.glob("*/*/ingest.json"))]
    rows.sort(key=lambda row: row["readyAt"], reverse=True)
    return rows


def read_report_box(box: Path) -> dict:
    meta, ingest_reason = schema.read_conformed(box / "ingest.json", schema.INGEST_SCHEMA)
    row = base_row(box, meta)
    if meta is None:
        return broken(row, ingest_reason or "ingest.json を読めない", box / "ingest.json")
    manifest_path = box / "manifest.json"
    if not manifest_path.is_file():
        return broken(row, "manifest.json が無い（全ファイル見送りの箱）", manifest_path)
    manifest, manifest_reason = schema.read_conformed(manifest_path, schema.MANIFEST_SCHEMA)
    if manifest is None:
        return broken(row, manifest_reason or "manifest.json の型が想定外", manifest_path)
    # 検証と表示の読み取りの間に書き換わっても落ちないよう、2回目が読めなければ空として扱う
    # If the file changes between validation and the display read, an unreadable second read counts as empty
    raw = schema.read_json(manifest_path)[0] or {}
    remote_state, remote_reason = schema.remote_exec_state(manifest)
    return dict(row, kind=manifest["kind"], description=manifest["description"],
                buildLabel=manifest["buildInfo"]["steamBuildLabel"], remoteExec=remote_state, remoteExecReason=remote_reason,
                triage=classify(manifest["kind"], remote_state), **display_manifest(raw, manifest_path))


def base_row(box: Path, meta: dict | None) -> dict:
    meta = meta or {}
    report_id = meta.get("id") or box.name
    ready_at = meta.get("readyAt") or meta.get("ingestedAt") or ""
    remote = remote_files(box)
    return {
        "id": report_id, "steamId": meta.get("steamId") or box.parent.name,
        "boxSteamId": box.parent.name, "boxId": box.name,
        "testerName": meta.get("steamPersonaName", "").strip(), "profileUrl": meta.get("steamProfileUrl", ""),
        "readyAt": ready_at, "date": jst_date(ready_at),
        "media": [name for name in MEDIA_FILES if (box / name).is_file() or name in remote],
        "problem": None, "kind": "", "description": "", "buildLabel": "",
    }


def remote_files(box: Path) -> frozenset:
    """手元に無く R2 にだけある箱内パス。remote.json が無い旧い箱は READY の files[] から手元に無い物を引く
    Box paths present only in R2; older boxes without remote.json fall back to READY's files[] minus what is local"""
    for name in ("remote.json", "READY"):
        data = schema.read_json(box / name)[0] if (box / name).is_file() else None
        files = data.get("files") if isinstance(data, dict) else None
        if isinstance(files, list):
            listed = (f for f in files if isinstance(f, str) and all(is_safe_segment(p) for p in f.split("/")))
            return frozenset(f for f in listed if not (box / f).is_file())
    return frozenset()


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


def classify(kind: str, remote_state: str) -> str:
    """バグ以外→遠隔実行あり/不明→バグの順に判定する（遠隔実行のあった報告はダイジェストの集計からも外す）
    Classifies non-bug first, then remote exec enabled/unknown, else bug (the digest also leaves remote-exec reports out)"""
    if kind != "bug":
        return TRIAGE_NOT_BUG
    if remote_state != schema.REMOTE_EXEC_DISABLED:
        return TRIAGE_EXCLUDED
    return TRIAGE_BUG
