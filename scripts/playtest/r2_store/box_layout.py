#!/usr/bin/env python3
"""取り込んだ報告箱のうち、logs repo へ写す小物と R2 にだけ置く生データの振り分け規則（標準ライブラリのみ）。

生データの正本は受け口の R2（バケット moorestech-playtest）だけ。logs repo は git の永久履歴なので、報告を読むのに要る
小さな物だけを写し、それ以外（スナップショット・ワールド・動画・連番フレーム・bundle 等）は R2 に残して remote.json に列挙する。
写す物は許可リストで決める。リストに無い物と、リストにあっても大きすぎる物は R2 側にだけ置く。
使い方:
  box_layout.py wants <箱内パス>                         … logs へ取るべきパス（許可リスト内）なら exit 0、そうでなければ 1
  box_layout.py finalize <箱> <kind> <steamId> <id> <全パス一覧> … 大きすぎる物を箱から外し、箱に無い物を remote.json に書く

Rules splitting an ingested box between small files copied into the logs repo and raw data kept only in R2 (stdlib only).
R2 (bucket moorestech-playtest) is the single source of truth for raw data. The logs repo is permanent git history, so it gets
only the small files needed to read a report; everything else stays in R2 and is listed in remote.json.
An allowlist decides what is copied; anything unlisted, or listed but oversized, lives in R2 only.
"""
from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

REF_NAME = "remote.json"
SCHEMA_VERSION = 1
BUCKET = "moorestech-playtest"
# 箱直下に写す物。READY・manifest 等は報告の索引、record.json は進行記録の本体
# Copied at the box root: READY/manifest index the report, record.json is the progress payload
KEEP_ROOT_FILES = frozenset(("manifest.json", "READY", "ingest.json", REF_NAME, "frames.tsv", "screenshot.png", "record.json"))
KEEP_DIRS = ("logs/", "export-markers/")
# repo/ 直下の差分・一覧・転送計画（テキスト）は写し、bundle と未追跡コピー（untracked/ 等）は R2 にだけ置く
# Text directly under repo/ (diffs, lists, transfer plan) is copied; bundles and untracked copies (untracked/ etc.) stay in R2
KEEP_REPO_SUFFIXES = (".diff", ".txt", ".json")
# 許可リストの物でもこれを超えれば R2 にだけ置く（GitHub の 100MB 上限と履歴の肥大を避ける）
# Even allowlisted files above this stay in R2 only (avoids GitHub's 100MB cap and history bloat)
KEEP_MAX_BYTES = 10 * 1000 * 1000
# 箱の索引そのもの。大きくても外すと報告が読めなくなるので常に写す
# The box's own index; dropping it would make the report unreadable, so it is always copied regardless of size
ALWAYS_KEEP = frozenset(("manifest.json", "READY", "ingest.json", REF_NAME, "record.json"))
KIND_DIRS = {"report": "reports", "progress": "progress"}


def wants(relative: str) -> bool:
    """箱内の相対パス（/ 区切り）が許可リストに入っているなら True（大きさは取った後で finalize が見る）
    True when a box-relative path (slash-separated) is on the allowlist; finalize checks the size after download"""
    if relative in KEEP_ROOT_FILES or relative.startswith(KEEP_DIRS):
        return True
    parts = relative.split("/")
    return len(parts) == 2 and parts[0] == "repo" and parts[1].endswith(KEEP_REPO_SUFFIXES)


def keeps_in_logs(relative: str, size: int) -> bool:
    if relative in ALWAYS_KEEP:
        return True
    return size <= KEEP_MAX_BYTES and wants(relative)


def finalize(box: Path, kind: str, steam_id: str, report_id: str, all_files: list[str]) -> int:
    """大きすぎる物を箱から外し、箱に無い物を R2 の所在として remote.json に書く。R2 にだけある件数を返す
    Drops oversized files from the box and writes what is absent into remote.json as R2 locations; returns the R2-only count"""
    for relative in all_files:
        local = box / relative
        if local.is_file() and not keeps_in_logs(relative, local.stat().st_size):
            local.unlink()
    remove_empty_dirs(box)
    remote = sorted(relative for relative in all_files if not (box / relative).is_file())
    ref = {"schemaVersion": SCHEMA_VERSION, "store": "r2", "bucket": BUCKET,
           "prefix": f"{KIND_DIRS[kind]}/{steam_id}/{report_id}/", "kind": kind, "steamId": steam_id, "id": report_id,
           "recordedAt": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "files": remote}
    (box / REF_NAME).write_text(json.dumps(ref, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    return len(remote)


def read_ref(box: Path) -> tuple[dict | None, str]:
    """remote.json を読む。無い・壊れていれば (None, 理由)。呼び出し側が理由を利用者へ見せる
    Reads remote.json; absent or broken returns (None, reason), which the caller shows to the user"""
    path = box / REF_NAME
    if not path.is_file():
        return None, f"{REF_NAME} が無い（R2 の所在を記録する前に取り込んだ箱）"
    # 外部ファイル（ディスク・人手編集）の読み取り境界
    # Read boundary for an on-disk, possibly hand-edited file
    try:
        ref = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(ref.get("files"), list) or not all(isinstance(f, str) for f in ref["files"]):
            raise ValueError("files が文字列の配列でない")
        return ref, ""
    except (OSError, ValueError, AttributeError) as error:
        return None, f"{path} を読めない: {type(error).__name__}: {error}"


def remove_empty_dirs(box: Path) -> None:
    for directory, _dirs, _names in sorted(os.walk(box), key=lambda entry: -len(entry[0])):
        if Path(directory) != box and not os.listdir(directory):
            os.rmdir(directory)


def main(argv: list[str]) -> int:
    if len(argv) == 2 and argv[0] == "wants":
        return 0 if wants(argv[1]) else 1
    if len(argv) == 6 and argv[0] == "finalize" and argv[2] in KIND_DIRS:
        listed = [line for line in Path(argv[5]).read_text(encoding="utf-8").splitlines() if line]
        print(finalize(Path(argv[1]), argv[2], argv[3], argv[4], listed))
        return 0
    print("usage: box_layout.py wants <path> | finalize <box> <report|progress> <steamId> <id> <list-file>", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
