#!/usr/bin/env python3
"""受け口由来・運用者入力の steamId/id/ファイルパスを、ファイル系へ連結する前に検証する（標準ライブラリのみ）。

規則は受け口の tools/playtest-receiver/src/keys.ts isSafeSegment と同じ（空・"."・".."・"/"・"\\"・制御文字を拒否）。
使い方:
  safe_segment.py segment <値>       … 単一の安全セグメントなら exit 0、そうでなければ理由を stderr に出して exit 1
  safe_segment.py ready-files <READY> … READY 要約の files[] を1行1件で出す。files キーが無ければ exit 3、不正パスは exit 4、
    空配列（クライアントが全ファイルを見送った正規の箱）は skipped[] の件数を出して exit 5、取り込み側の予約名と衝突すれば exit 6

Validates steamId/id/file paths from the receiver or an operator before they are joined into filesystem paths (stdlib only).
The rule mirrors isSafeSegment in tools/playtest-receiver/src/keys.ts (rejects empty, ".", "..", "/", "\\" and control characters).
Usage: "segment <value>" exits 0 when safe, else prints the reason and exits 1;
"ready-files <READY>" prints files[] one per line, exiting 3 when the files key is absent, 4 on an unsafe path,
5 with the skipped[] count printed for an empty list (the client legitimately skipped every file) and 6 on a reserved name.
"""
from __future__ import annotations

import json
import re
import sys

UNSAFE_CHARACTERS = re.compile(r"[/\\\x00-\x1f\x7f]")
# 取り込み・投入・自動修正ランが箱直下へ書く名前。payload が同名だと上書き・マーカー偽装になる。APFS は大文字小文字を区別しないので casefold で比べる
# Names ingest/enqueue/auto-fix runs write at the box root; a same-named payload would clobber or forge them. APFS is case-insensitive, so compare casefolded
RESERVED_ROOT_NAMES = frozenset(n.casefold() for n in (
    "ingest.json", "READY", "AUTOFIX_QUEUED", "AUTOFIX_FORCED", "fix-result.json", "run.env"))


def is_safe_segment(value: object) -> bool:
    if not isinstance(value, str) or value in ("", ".", ".."):
        return False
    return UNSAFE_CHARACTERS.search(value) is None


def read_ready_files(ready_path: str) -> int:
    """READY 要約の files[] を検証して出力する。改行を含むパスは行分割でのすり抜けになるので制御文字として拒否される
    Validates and prints files[] from a READY summary; a path with a newline is rejected as a control character"""
    with open(ready_path, encoding="utf-8") as ready:
        summary = json.load(ready)
    if not isinstance(summary, dict) or "files" not in summary:
        return 3
    files = summary["files"]
    if not isinstance(files, list):
        print(f"files が配列でない: {files!r}", file=sys.stderr)
        return 4
    if not files:
        skipped = summary.get("skipped")
        print(len(skipped) if isinstance(skipped, list) else "不明")
        return 5
    for path in files:
        if not isinstance(path, str) or not all(is_safe_segment(segment) for segment in path.split("/")):
            print(f"不正なパス: {path!r}", file=sys.stderr)
            return 4
    for path in files:
        root = path.split("/")[0].casefold()
        if root in RESERVED_ROOT_NAMES or root.endswith(".partial"):
            print(f"取り込み側の予約名と衝突: {path!r}", file=sys.stderr)
            return 6
    for path in files:
        print(path)
    return 0


def main(argv: list[str]) -> int:
    if len(argv) == 2 and argv[0] == "segment":
        if is_safe_segment(argv[1]):
            return 0
        print(f"安全な単一セグメントでない（空・.・..・/・\\・制御文字を含む）: {argv[1]!r}", file=sys.stderr)
        return 1
    if len(argv) == 2 and argv[0] == "ready-files":
        return read_ready_files(argv[1])
    print("usage: safe_segment.py segment <value> | ready-files <READY>", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
