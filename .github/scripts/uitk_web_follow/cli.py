"""Command line entry of the UITK Web follow gate (G1)."""

import argparse
import os
import sys
from pathlib import Path

from . import git_history
from .history_ownership import commit_owners
from .checks import RULE_SWITCH, Violation, check_coverage, check_retired_changes, check_switch_records, switching_bundles, unrecorded_commits
from .ledger import STATE_DARK, STATE_WEB, empty_ledger, parse_ledger
from .ownership import WEB_SOURCE_ROOT

LEDGER_PATH = "docs/uitk/TODO.md"


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="UITK Web follow gate (G1)")
    parser.add_argument("--root", default=".")
    parser.add_argument("--base", required=True)
    parser.add_argument("--head", required=True)
    args = parser.parse_args(argv)
    root = Path(args.root).resolve()
    base_rev = git_history.merge_base(root, args.base, args.head)

    # head の台帳は必須。base に無いのは台帳を入れる PR だけなので全束 Web とみなす
    # The head ledger is required; only the PR introducing it lacks a base ledger, read as all-Web
    head_text = git_history.read_file_at(root, args.head, LEDGER_PATH)
    if head_text is None:
        print(f"G1: {LEDGER_PATH} が head に無い", file=sys.stderr)
        return 1
    base_text = git_history.read_file_at(root, base_rev, LEDGER_PATH)
    head_parse = parse_ledger(head_text)
    base_parse = parse_ledger(base_text) if base_text is not None else None
    errors = head_parse.errors + (base_parse.errors if base_parse is not None else ())
    if errors:
        for error in errors:
            print(f"G1: 台帳を読めない: {error}", file=sys.stderr)
        return 1
    head = head_parse.ledger
    base = base_parse.ledger if base_parse is not None else empty_ledger()
    if base_parse is None:
        print(f"G1: base {base_rev[:10]} に台帳が無いので全束を Web とみなす")

    # 網羅と切替済みの束の変更を見る
    # Check ledger coverage and changes to switched bundles
    violations: list[Violation] = [
        *check_coverage(head, git_history.tracked_files(root, args.head, WEB_SOURCE_ROOT)),
        *check_retired_changes(base, head, git_history.changed_files(root, base_rev, args.head)),
    ]

    # 起点を持つ束ごとに未記帳を数え、今回切り替える束だけ赤にする
    # Count unrecorded commits per bundle with a start; only bundles switching in this PR fail
    report: list[str] = []
    switching = switching_bundles(base, head)
    for bundle_id, bundle in sorted(head.bundles.items()):
        # 状態で分岐する。起点は Web 以外の束にだけあることをパーサが保証している
        # Branch on the state; the parser guarantees a start exists exactly for non-Web bundles
        if bundle.state == STATE_WEB or bundle.dark_start is None:
            continue
        start = git_history.resolve_commit(root, bundle.dark_start)
        if start is None or not git_history.is_ancestor(root, start, args.head):
            violations.append(Violation(RULE_SWITCH, f"束 {bundle_id} の暗転の起点 {bundle.dark_start} が head の祖先に無い"))
            continue
        # 継続中の暗転は起点を固定し、短縮表記の変更だけ許す
        # Preserve the origin of an ongoing blackout while accepting equivalent abbreviations
        previous = base.bundles[bundle_id]
        if previous.state == STATE_DARK and git_history.resolve_commit(root, previous.dark_start) != start:
            violations.append(Violation(RULE_SWITCH, f"束 {bundle_id} の継続する暗転の起点は変更できない: {previous.dark_start} → {bundle.dark_start}"))
        commits = git_history.web_commits(root, start, args.head)
        owners, history_errors = commit_owners(root, args.head, commits)
        if history_errors:
            violations.extend(Violation(RULE_SWITCH, error) for error in history_errors)
            continue
        unrecorded = unrecorded_commits(head, bundle_id, commits, owners)
        report.append(f"束 {bundle_id}（{bundle.state}・起点 {bundle.dark_start}）: 未記帳 {len(unrecorded)} 件")
        if bundle_id in switching:
            violations.extend(check_switch_records(bundle_id, unrecorded))
    _emit(report, violations)
    return 1 if violations else 0


def _emit(report: list[str], violations: list[Violation]) -> None:
    lines = ["## UITK Web follow gate (G1)", ""]
    lines += [f"- {line}" for line in report] or ["- 起点を持つ束は無い"]
    lines += [f"- **{violation.rule}**: {violation.message}" for violation in violations]
    for line in report:
        print(f"G1: {line}")
    for violation in violations:
        print(f"G1 {violation.rule}: {violation.message}", file=sys.stderr)
    if not violations:
        print("G1: 違反なし")

    # GitHub では job summary にも同じ内容を書く
    # On GitHub, mirror the same content into the job summary
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as stream:
            stream.write("\n".join(lines) + "\n")
