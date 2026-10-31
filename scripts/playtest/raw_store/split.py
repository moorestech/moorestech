#!/usr/bin/env python3
"""取り込み途中の箱から生データを抜き出し、生データ側の下書きへ移す（標準ライブラリのみ）。

使い方: split.py <logs側の下書き箱> <生データ側の下書き> <kind/steamId/id> <生データ置き場の根>
  logs 側に残さない物を生データ側の下書きへ移し、両方に raw.json（保存先・期限・ファイル一覧）を書く。
  移した件数を標準出力へ出す。移す物が無ければ 0 を出し、生データ側も raw.json も作らない。
  期限は箱の ingest.json の ingestedAt から PLAYTEST_RAW_RETENTION_DAYS 日（既定 60）。
  失敗すると理由を出して非0。呼び出し側（ingest.sh 等）は箱を公開せず ack しない。

Moves raw data out of a box being ingested into a raw-side draft (stdlib only).
Usage: split.py <logs draft box> <raw draft> <kind/steamId/id> <raw root>. Files that do not stay on the logs side move to
the raw draft and both sides get a raw.json (location, expiry, file list). Prints the moved count; with nothing to move it
prints 0 and creates neither the raw draft nor raw.json. Expiry is ingest.json's ingestedAt plus PLAYTEST_RAW_RETENTION_DAYS
(default 60). Any failure prints why and exits non-zero, and the caller (ingest.sh etc.) neither publishes nor acks.
"""
from __future__ import annotations

import json
import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import raw_layout as layout  # noqa: E402


def split(box: Path, raw_draft: Path, box_rel: str, root: Path) -> int:
    days = layout.retention_days()
    ingested_at = json.loads((box / "ingest.json").read_text(encoding="utf-8"))["ingestedAt"]
    moving = []
    for relative in layout.walk_files(box):
        size = (box / relative).stat().st_size
        if not layout.keeps_in_logs(relative, size):
            moving.append({"path": relative, "bytes": size})
    if not moving:
        print(0)
        return 0
    if raw_draft.exists():
        raise OSError(f"生データ側の下書きが既にある（呼び出し側が消してから呼ぶ）: {raw_draft}")
    for entry in moving:
        target = raw_draft / entry["path"]
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(str(box / entry["path"]), str(target))
    layout.remove_empty_dirs(box)
    ref = layout.build_ref(box_rel, root, ingested_at, moving, days)
    layout.write_ref(raw_draft / layout.REF_NAME, ref)
    layout.write_ref(box / layout.REF_NAME, ref)
    print(len(moving))
    return 0


def main(argv: list[str]) -> int:
    if len(argv) != 4:
        print("usage: split.py <logs draft box> <raw draft> <kind/steamId/id> <raw root>", file=sys.stderr)
        return 2
    # ディスクIOと取り込み済み ingest.json の読み取り境界。失敗は理由を出して箱を公開させない
    # Disk IO and ingest.json read boundary; a failure prints why and keeps the box unpublished
    try:
        return split(Path(argv[0]), Path(argv[1]), argv[2], Path(argv[3]))
    except (OSError, ValueError, KeyError) as error:
        print(f"[raw-store] ERROR: 生データを分けられない: {type(error).__name__}: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
