"""Command line entry of the UITK no-Web gate (G4)."""

import argparse
import sys
from collections import Counter
from pathlib import Path

from .allowlist import line_allowed, parse_allowlist, path_allowed
from .scan import GitFailure, content_hits, path_hits, tracked_files

DEFAULT_ALLOWLIST = ".github/scripts/uitk_no_web/allowlist.txt"


def main(argv: list) -> int:
    parser = argparse.ArgumentParser(description="UITK no-Web gate (G4)")
    parser.add_argument("--root", default=".")
    parser.add_argument("--allowlist", default=DEFAULT_ALLOWLIST)
    parser.add_argument("--report", action="store_true")
    args = parser.parse_args(argv)
    root = Path(args.root).resolve()

    # 許可リストは必須。読めなければ何も許さず止める
    # The allowlist is required; if it cannot be read, nothing is allowed and the gate stops
    allowlist_path = root / args.allowlist
    if not allowlist_path.is_file():
        print(f"G4: 許可リスト {args.allowlist} が無い", file=sys.stderr)
        return 2
    allowlist, errors = parse_allowlist(allowlist_path.read_text(encoding="utf-8"))
    if errors:
        for error in errors:
            print(f"G4: 許可リストを読めない: {error}", file=sys.stderr)
        return 2

    # 語の当たりを集め、許可リストで落とす。使われた行を数えて腐った行を出す
    # Collect hits, drop allowed ones, and count which entries were used to surface stale ones
    # git は外部プロセスの境界。失敗は stderr に出して 2 で止める（終了の持ち主はここだけ）
    # git is an external-process boundary; a failure goes to stderr and stops with 2 (the only exit owner)
    try:
        hits = path_hits(tracked_files(root)) + content_hits(root)
    except GitFailure as failure:
        print(f"G4: {failure}", file=sys.stderr)
        return 2
    used = set()
    remaining = []
    for hit in hits:
        entry = _allowing_entry(allowlist, hit)
        if entry is None:
            remaining.append(hit)
        else:
            used.add(entry.line_no)
    stale = [entry for entry in (*allowlist.paths, *allowlist.lines) if entry.line_no not in used]

    if args.report:
        _print_report(remaining, stale)
        return 0
    # 当たりを先に全部出す。腐った行があっても残りの一覧は取れる（出さずに 2 で終えると一覧が空になる）
    # Print every hit first; stale entries must not hide the list of remaining tokens
    for hit in remaining:
        print(f"G4: {hit.path}:{hit.line_no}: {hit.token}: {hit.text[:160]}")
    print(f"G4: 許可リスト外 {len(remaining)} 件")
    if stale:
        for entry in stale:
            print(f"G4: 何も許していない許可リストの行 {entry.line_no}: {entry.glob}（{entry.reason}）", file=sys.stderr)
        return 2
    return 1 if remaining else 0


def _allowing_entry(allowlist, hit):
    for entry in allowlist.paths:
        if path_allowed(entry, hit.path):
            return entry
    if hit.line_no == 0:
        return None
    for entry in allowlist.lines:
        if line_allowed(entry, hit.path, hit.text):
            return entry
    return None


def _print_report(remaining: list, stale: list) -> None:
    # 置き場の上 3 段ごとの件数。残りの作業量の見積もりに使う
    # Counts per top three path segments, used to size the remaining work
    counts = Counter("/".join(hit.path.split("/")[:3]) for hit in remaining)
    for area, count in counts.most_common():
        print(f"{count}\t{area}")
    print(f"G4(report): 許可リスト外 {len(remaining)} 件・何も許していない行 {len(stale)} 件")
