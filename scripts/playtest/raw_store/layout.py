#!/usr/bin/env python3
"""プレイテスト生データ置き場（logs repo の外）の配置規則（標準ライブラリのみ）。

logs repo は git の永久履歴なので、報告を読むのに要る小さな物だけを箱に残し、それ以外は生データ置き場の
同じ相対パス（reports/<steamId>/<id>/...・progress/...）へ置く。箱にはその保存先と期限を書いた raw.json を置く。
残す物は許可リストで決める。リストに無い物と、リストにあっても大きすぎる物は生データ側へ行く（git を太らせないため）。

Placement rules for the playtest raw store, which lives outside the logs repo (stdlib only).
The logs repo is permanent git history, so a box keeps only the small files needed to read the report; everything else
goes to the raw store at the same relative path, and the box gets a raw.json naming where it went and when it expires.
What stays is an allowlist: anything unlisted, or listed but oversized, goes to the raw side so git never grows from it.
"""
from __future__ import annotations

import json
import os
from datetime import datetime, timedelta, timezone
from pathlib import Path

REF_NAME = "raw.json"
SCHEMA_VERSION = 1
DEFAULT_RETENTION_DAYS = 60
# 箱直下に残す物。READY・manifest 等は報告の索引、AUTOFIX_* は投入状態、record.json は進行記録の本体
# Kept at the box root: READY/manifest index the report, AUTOFIX_* record enqueue state, record.json is the progress payload
KEEP_ROOT_FILES = frozenset(("manifest.json", "READY", "ingest.json", REF_NAME, "frames.tsv", "screenshot.png",
                             "record.json", "AUTOFIX_QUEUED", "AUTOFIX_FORCED"))
KEEP_DIRS = ("logs/", "export-markers/")
# repo/ 直下の差分・一覧・転送計画（テキスト）は残し、bundle と未追跡コピー（untracked/ 等）は生データへ
# Text directly under repo/ (diffs, lists, transfer plan) stays; bundles and untracked copies (untracked/ etc.) go raw
KEEP_REPO_SUFFIXES = (".diff", ".txt", ".json")
# 許可リストの物でもこれを超えれば生データ側へ（GitHub の 100MB 上限と履歴の肥大を避ける）
# Even allowlisted files above this go raw (avoids GitHub's 100MB cap and history bloat)
KEEP_MAX_BYTES = 10 * 1000 * 1000
# 箱の索引そのもの。大きくても動かすと報告が読めなくなるので常に残す
# The box's own index; moving it would make the report unreadable, so it always stays regardless of size
ALWAYS_KEEP = frozenset(("manifest.json", "READY", "ingest.json", REF_NAME, "record.json"))
KINDS = ("reports", "progress")


def keeps_in_logs(relative: str, size: int) -> bool:
    """箱内の相対パス（/ 区切り）が logs 側に残るなら True
    True when a box-relative path (slash-separated) stays on the logs side"""
    if relative in ALWAYS_KEEP:
        return True
    if size > KEEP_MAX_BYTES:
        return False
    if relative in KEEP_ROOT_FILES or relative.startswith(KEEP_DIRS):
        return True
    parts = relative.split("/")
    return len(parts) == 2 and parts[0] == "repo" and parts[1].endswith(KEEP_REPO_SUFFIXES)


def default_root() -> Path:
    """MOORESTECH_PLAYTEST_RAW、無ければ本体 clone の兄弟（MOORESTECH_LOGS と同じ流儀でスクリプト位置から導出）
    MOORESTECH_PLAYTEST_RAW, else the main clone's sibling (derived from the script location like MOORESTECH_LOGS)"""
    configured = os.environ.get("MOORESTECH_PLAYTEST_RAW")
    if configured:
        return Path(configured)
    repo = Path(os.environ.get("MOORESTECH_REPO") or Path(__file__).resolve().parents[3])
    return repo.parent / "moorestech_playtest_raw"


def box_relative(box: Path) -> str:
    """logs 側・生データ側で共通の箱の相対パス（<kind>/<steamId>/<id>）
    The box path shared by both sides (<kind>/<steamId>/<id>)"""
    return "/".join((box.parent.parent.name, box.parent.name, box.name))


def walk_files(box: Path) -> list[str]:
    files = []
    for directory, _dirs, names in os.walk(box):
        for name in names:
            files.append(os.path.relpath(os.path.join(directory, name), box).replace(os.sep, "/"))
    return sorted(files)


def utc_text(moment: datetime) -> str:
    return moment.astimezone(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def parse_utc(text: str) -> datetime:
    return datetime.strptime(text, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)


def build_ref(box_rel: str, root: Path, ingested_at: str, files: list[dict], retention_days: int) -> dict:
    expires = parse_utc(ingested_at) + timedelta(days=retention_days)
    return {"schemaVersion": SCHEMA_VERSION, "rawRelative": box_rel, "rawRootAtStore": str(root),
            "storedAt": utc_text(datetime.now(timezone.utc)), "ingestedAt": ingested_at,
            "retentionDays": retention_days, "expiresAt": utc_text(expires),
            "totalBytes": sum(f["bytes"] for f in files), "files": files}


def write_ref(path: Path, ref: dict) -> None:
    path.write_text(json.dumps(ref, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")


def read_ref(path: Path) -> tuple[dict | None, str]:
    """raw.json を読む。壊れていれば (None, 理由)。呼び出し側が理由を利用者へ見せる
    Reads raw.json; a broken one returns (None, reason), which the caller shows to the user"""
    # 外部ファイル（ディスク・人手編集）の読み取り境界
    # Read boundary for an on-disk, possibly hand-edited file
    try:
        ref = json.loads(path.read_text(encoding="utf-8"))
        parse_utc(ref["expiresAt"])
        if not isinstance(ref["files"], list) or not all(isinstance(f.get("path"), str) for f in ref["files"]):
            raise ValueError("files が {path,bytes} の配列でない")
        return ref, ""
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        return None, f"{path} を読めない: {type(error).__name__}: {error}"


def retention_days() -> int:
    value = os.environ.get("PLAYTEST_RAW_RETENTION_DAYS", str(DEFAULT_RETENTION_DAYS))
    if not value.isdigit() or int(value) < 1:
        raise SystemExit(f"[raw-store] ERROR: PLAYTEST_RAW_RETENTION_DAYS が1以上の整数でない: {value!r}")
    return int(value)
