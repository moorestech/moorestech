"""UITK Web follow gate (G1) rules over the base and head ledgers and git history."""

from dataclasses import dataclass

from .git_history import ChangedFile, CommitFiles
from .ledger import Ledger, STATE_SWITCHED
from .ownership import owning_bundles, uncovered_sources

RULE_COVERAGE = "G1-0 台帳の網羅"
RULE_SWITCH = "G1-1 切替の記帳"
RULE_RETIRED = "G1-2 切替済みの束"


@dataclass(frozen=True)
class Violation:
    rule: str
    message: str


def check_coverage(head: Ledger, tracked_sources: tuple[str, ...]) -> tuple[Violation, ...]:
    return tuple(
        Violation(RULE_COVERAGE, f"{path} が台帳のどの行の Web パスにも入っていない。docs/uitk/TODO.md の「## 行」に足す")
        for path in uncovered_sources(tracked_sources, head.rows))


def check_retired_changes(base: Ledger, head: Ledger, changed: tuple[ChangedFile, ...]) -> tuple[Violation, ...]:
    # 新規パスも所有束で判定し、今回切替・戻し・削除は通す
    # Use owners even for new paths; allow switching, rollback and deletion
    # head の行を消しても保護が外れないよう、base と head の両方の所有束で判定する
    # Judge by both base and head owners so deleting a head row cannot drop the protection
    def retired_owners(change: ChangedFile) -> tuple[int, ...]:
        for owners in (owning_bundles(change.path, head.rows), owning_bundles(change.path, base.rows)):
            if owners and all(
                    base.bundles[bundle_id].state == STATE_SWITCHED
                    and head.bundles[bundle_id].state == STATE_SWITCHED for bundle_id in owners):
                return tuple(sorted(owners))
        return ()

    return tuple(
        Violation(RULE_RETIRED, f"{change.path}（{change.status}）は切替済みの束 {list(owners)} の Web パス。描かれないコードなので UITK 側を直す")
        for change in changed
        for owners in (retired_owners(change),)
        if change.status != "D" and owners)


def switching_bundles(base: Ledger, head: Ledger) -> tuple[int, ...]:
    return tuple(
        bundle_id for bundle_id, bundle in head.bundles.items()
        if bundle.state == STATE_SWITCHED and base.bundles[bundle_id].state != STATE_SWITCHED)


def unrecorded_commits(head: Ledger, bundle_id: int, commits: tuple[CommitFiles, ...], commit_owners: dict[str, frozenset[int]]) -> tuple[str, ...]:
    # 変更当時と現在の所有束に対して記帳を要求する
    # Require records for both historical and current owners of each change
    recorded = tuple(record.commit for record in head.records if record.bundle_id == bundle_id)
    return tuple(
        commit.sha for commit in commits
        if bundle_id in commit_owners[commit.sha]
        and not any(commit.sha.startswith(prefix) for prefix in recorded))


def check_switch_records(bundle_id: int, unrecorded: tuple[str, ...]) -> tuple[Violation, ...]:
    return tuple(
        Violation(RULE_SWITCH, f"束 {bundle_id} の暗転の起点より後のコミット {sha[:10]} がその束の Web パスを触ったが「## 追従記録」に無い。写したか捨てた（理由）を書く")
        for sha in unrecorded)
