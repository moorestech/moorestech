#!/usr/bin/env python3
"""片付いた報告（修正のマージか却下）のうち、片付けから 7 日たったものを R2 の削除対象として選ぶ（標準ライブラリのみ）。

入力は logs repo の git 管理外 .state/report-resolutions.json（誰がいつ書くかは未決。書き手が無い間は削除対象が出ない）:
  {"resolutions": {"<kind>/<steamId>/<id>": {"outcome": "merged" | "rejected", "resolvedAt": "YYYY-MM-DDTHH:MM:SSZ"}}}
削除の済んだ箱は .state/r2-purges.json に記録し、次回から選ばない。
使い方:
  settled.py due <logs>                          … 削除対象を1行1件（kind US steamId US id US outcome US resolvedAt）で出す
  settled.py record <logs> <kind> <steamId> <id> <outcome> … 削除済みとして記録する

Selects settled reports (fix merged or rejected) whose settling is 7+ days old as R2 deletion targets (stdlib only).
The input is .state/report-resolutions.json outside git in the logs repo (who writes it is undecided; with no writer nothing is due).
Purged boxes are recorded in .state/r2-purges.json and skipped afterwards.
"""
from __future__ import annotations

import json
import os
import sys
import tempfile
from datetime import datetime, timedelta, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "lib"))
from safe_segment import is_safe_segment  # noqa: E402

# ユーザー裁定 2026-10-31: R2 の生データは報告が片付いてから 7 日で消す
# User ruling 2026-10-31: R2 raw data is deleted 7 days after a report is settled
SETTLED_RETENTION = timedelta(days=7)
OUTCOMES = ("merged", "rejected")
KINDS = ("report", "progress")


def state_dir(logs: Path) -> Path:
    return logs / ".state"


def read_object(path: Path, key: str) -> tuple[dict | None, str]:
    """無ければ空、壊れていれば (None, 理由)。人手で直したファイルで落ちないよう形まで見る
    Absent gives empty, broken gives (None, reason); the shape is checked so a hand-edited file never crashes this"""
    if not path.is_file():
        return {}, ""
    # 外部ファイル（ディスク・人手編集）の読み取り境界
    # Read boundary for an on-disk, possibly hand-edited file
    try:
        value = json.loads(path.read_text(encoding="utf-8")).get(key)
    except (OSError, ValueError, AttributeError) as error:
        return None, f"{path} を読めない: {type(error).__name__}: {error}"
    if not isinstance(value, dict):
        return None, f"{path} の {key} が辞書でない"
    return value, ""


def parse_key(key: str) -> tuple[str, str, str] | None:
    parts = key.split("/")
    if len(parts) != 3 or parts[0] not in KINDS or not all(is_safe_segment(p) for p in parts[1:]):
        return None
    return parts[0], parts[1], parts[2]


def due(logs: Path, now: datetime) -> int:
    resolutions_path = state_dir(logs) / "report-resolutions.json"
    if not resolutions_path.is_file():
        print(f"[r2-prune] 片付けの記録が無い（{resolutions_path}）。削除対象なし", file=sys.stderr)
        return 0
    resolutions, reason = read_object(resolutions_path, "resolutions")
    purged, purged_reason = read_object(state_dir(logs) / "r2-purges.json", "purged")
    if resolutions is None or purged is None:
        print(f"[r2-prune] ERROR: {reason or purged_reason}（何も消さない）", file=sys.stderr)
        return 1
    bad = 0
    for key, entry in sorted(resolutions.items()):
        parsed = parse_key(key)
        resolved_at = entry.get("resolvedAt") if isinstance(entry, dict) else None
        outcome = entry.get("outcome") if isinstance(entry, dict) else None
        moment = parse_utc(resolved_at)
        if parsed is None or outcome not in OUTCOMES or moment is None:
            print(f"[r2-prune] ERROR: 片付けの記録の形が不正なので飛ばす: {key!r} {entry!r:.120}", file=sys.stderr)
            bad += 1
            continue
        if key in purged or now - moment < SETTLED_RETENTION:
            continue
        print("\x1f".join((*parsed, outcome, resolved_at)))
    return 1 if bad else 0


def parse_utc(text: object) -> datetime | None:
    if not isinstance(text, str):
        return None
    try:
        return datetime.strptime(text, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=timezone.utc)
    except ValueError:
        return None


def record(logs: Path, kind: str, steam_id: str, report_id: str, outcome: str) -> int:
    path = state_dir(logs) / "r2-purges.json"
    purged, reason = read_object(path, "purged")
    if purged is None:
        print(f"[r2-prune] ERROR: 削除済みの記録を更新できない: {reason}", file=sys.stderr)
        return 1
    purged[f"{kind}/{steam_id}/{report_id}"] = {
        "purgedAt": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "outcome": outcome}
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", dir=path.parent, delete=False, suffix=".tmp") as handle:
        json.dump({"purged": purged}, handle, ensure_ascii=False, indent=1)
    os.replace(handle.name, path)
    return 0


def main(argv: list[str]) -> int:
    if len(argv) == 2 and argv[0] == "due":
        return due(Path(argv[1]), datetime.now(timezone.utc))
    if len(argv) == 6 and argv[0] == "record" and argv[2] in KINDS:
        return record(Path(argv[1]), argv[2], argv[3], argv[4], argv[5])
    print("usage: settled.py due <logs> | record <logs> <kind> <steamId> <id> <outcome>", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
