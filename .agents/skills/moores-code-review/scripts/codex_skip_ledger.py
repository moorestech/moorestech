#!/usr/bin/env python3
# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成（配線なき
#   検出器は未実装と同じ・2026-08-03ユーザー裁定）。このバナー自体も必須
#   （tests/test_skill_wiring.py が全スクリプトのバナー実在を機械検証する）。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""codex_skip_ledger.py — Codex 系統の起動結果をレビューをまたいで記録し、連続スキップを数える。

1 回のスキップは各報告に理由付きで書かれるが、認証切れ・枠切れが数日続くと、本質バグを
最も拾う系統が抜けたまま何本ものレビューが通る（1 本ずつの報告では連続に気づけない）。
起動可否の判定（codex_preflight.py）と実行時の失敗（枠切れ・認証失効）を 1 行ずつ台帳へ足し、
末尾から数えた連続スキップ数を返す。連続 2 回以上は、本体が報告の冒頭と PR 本文に書く。

Ledger of Codex outcomes across reviews; returns the trailing skip streak so a multi-day
auth/quota outage is surfaced instead of being reported once per review.

usage:
  codex_skip_ledger.py record --ledger <tsv> --status <ok|missing_auth|missing_binary|quota|auth_expired|...> --target <名前>
出力: {"streak": N, "since": "<最初のスキップ日時>", "statuses": [...], "alert": bool}
"""
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime
from pathlib import Path

ALERT_STREAK = 2
OK_STATUS = "ok"


def read_rows(ledger: Path) -> list[list[str]]:
    if not ledger.is_file():
        return []
    rows = []
    for line in ledger.read_text(encoding="utf-8").splitlines():
        cols = line.split("\t")
        if len(cols) >= 3:
            rows.append(cols)
    return rows


def trailing_streak(rows: list[list[str]]) -> list[list[str]]:
    # 末尾から ok が出るまでのスキップ行 / Skip rows after the last ok
    skipped: list[list[str]] = []
    for row in reversed(rows):
        if row[1] == OK_STATUS:
            break
        skipped.append(row)
    return list(reversed(skipped))


def main() -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("record")
    r.add_argument("--ledger", required=True)
    r.add_argument("--status", required=True)
    r.add_argument("--target", required=True)
    args = ap.parse_args()
    ledger = Path(args.ledger).expanduser()
    ledger.parent.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().astimezone().isoformat(timespec="seconds")
    target = args.target.replace("\t", " ").replace("\n", " ")
    with ledger.open("a", encoding="utf-8") as f:
        f.write(f"{stamp}\t{args.status}\t{target}\n")
    streak = trailing_streak(read_rows(ledger))
    result = {
        "streak": len(streak),
        "since": streak[0][0] if streak else None,
        "statuses": sorted({row[1] for row in streak}),
        "targets": [row[2] for row in streak],
        "alert": ALERT_STREAK <= len(streak),
    }
    print(json.dumps(result, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
