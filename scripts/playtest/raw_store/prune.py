#!/usr/bin/env python3
"""生データ置き場から期限（raw.json の expiresAt・取り込みから既定 60 日）を過ぎた箱を消す（標準ライブラリのみ）。

使い方: prune.py [-n]
  消した物・消せなかった物・判定できなかった物を標準出力へ出す。何もしなかった回は何も出さない
  （Hermes の no-agent cron は stdout をそのまま Discord へ送り、空なら投稿しない）。
  -n は消さずに「消す予定」だけ出す。消せなかった物があれば exit 1。
  置き場の根は MOORESTECH_PLAYTEST_RAW（既定は本体 clone の兄弟 moorestech_playtest_raw）。
  logs repo 側の raw.json は残す。読む側は期限切れを raw.json と実物の不在から判定して利用者に示す。

Deletes boxes past their expiry (raw.json expiresAt, default 60 days after ingest) from the raw store (stdlib only).
Usage: prune.py [-n]. Prints what was deleted, what could not be and what could not be judged; prints nothing when idle
(Hermes no-agent cron posts stdout to Discord verbatim and skips empty output). -n only lists what would go. Exit 1 on
any failure. The root is MOORESTECH_PLAYTEST_RAW (default: the main clone's sibling moorestech_playtest_raw).
The logs-side raw.json stays; readers infer expiry from it plus the missing files and show that to the user.
"""
from __future__ import annotations

import shutil
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import raw_layout as layout  # noqa: E402

# 取り込み途中の下書き（*.partial）はこれより古ければ残骸とみなす（取り込みの timeout は 600 秒）
# A *.partial draft older than this is debris (ingest times out after 600 seconds)
STALE_PARTIAL_SECONDS = 24 * 3600


def megabytes(path: Path) -> str:
    total = sum(p.stat().st_size for p in path.rglob("*") if p.is_file())
    return f"{total / 1e6:.1f}MB"


def judge(box: Path, now: datetime) -> tuple[str, str]:
    """(action, 説明)。action は delete|keep|error
    Returns (action, note); action is delete, keep or error"""
    if box.name.endswith(".partial"):
        if time.time() - box.stat().st_mtime < STALE_PARTIAL_SECONDS:
            return "keep", ""
        return "delete", "取り込み途中で止まった下書きの残骸"
    ref, reason = layout.read_ref(box / layout.REF_NAME)
    if ref is None:
        return "error", f"期限を判定できない（{reason}）"
    if now < layout.parse_utc(ref["expiresAt"]):
        return "keep", ""
    return "delete", f"期限 {ref['expiresAt']}"


def prune(root: Path, dry_run: bool) -> int:
    now = datetime.now(timezone.utc)
    lines, failed = [], 0
    for kind in layout.KINDS:
        for box in sorted(p for p in (root / kind).glob("*/*") if p.is_dir()):
            relative = f"{kind}/{box.parent.name}/{box.name}"
            # ディスクIO（他プロセスの書き込み・権限）の境界。失敗はその箱の理由として出し、残りは続ける
            # Disk IO boundary (concurrent writers, permissions); a failure is reported for that box and the rest continue
            try:
                action, note = judge(box, now)
                if action == "error":
                    failed += 1
                    lines.append(f"- 消せなかった: {relative}（{note}）")
                elif action == "delete" and dry_run:
                    lines.append(f"- 消す予定: {relative}（{note}・{megabytes(box)}）")
                elif action == "delete":
                    size = megabytes(box)
                    shutil.rmtree(box)
                    lines.append(f"- 消した: {relative}（{note}・{size}）")
                    remove_if_empty(box.parent)
            except OSError as error:
                failed += 1
                lines.append(f"- 消せなかった: {relative}（{type(error).__name__}: {error}）")
    if lines:
        title = "期限切れの生データ（判定のみ）" if dry_run else "期限切れの生データを消した"
        print(f"## moorestech プレイテスト: {title}（{root}）")
        print("\n".join(lines))
    return 1 if failed else 0


def remove_if_empty(directory: Path) -> None:
    if not any(directory.iterdir()):
        directory.rmdir()


def main(argv: list[str]) -> int:
    if argv not in ([], ["-n"]):
        print("usage: prune.py [-n]", file=sys.stderr)
        return 2
    root = layout.default_root()
    if not root.is_dir():
        # 置き場が無いのは設定の誤り。黙ると期限切れが溜まり続けるので stdout（Discord）へ出す
        # A missing root is misconfiguration; staying quiet would let expired data pile up, so say so on stdout (Discord)
        print(f"## moorestech プレイテスト: 生データ置き場が無いので期限削除できない（{root}）")
        return 1
    return prune(root, argv == ["-n"])


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
