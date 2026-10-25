"""UITK Web follow gate (G1) rules over the base and head ledgers and git history."""

from dataclasses import dataclass

from .git_history import ChangedFile, CommitFiles
from .ledger import Ledger, STATE_SWITCHED
from .ownership import is_retired, owning_bundles, uncovered_sources

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
    # 削除は通す。base と head の両方で切替済みのパスだけ落とす（切替を戻す PR は通る）
    # Deletions pass; only paths retired in both base and head fail, so a rollback PR passes
    return tuple(
        Violation(RULE_RETIRED, f"{change.path}（{change.status}）は切替済みの束 {sorted(owning_bundles(change.path, head.rows))} の Web パス。描かれないコードなので UITK 側を直す")
        for change in changed
        if change.status != "D" and is_retired(change.path, base) and is_retired(change.path, head))


def switching_bundles(base: Ledger, head: Ledger) -> tuple[int, ...]:
    return tuple(
        bundle_id for bundle_id, bundle in head.bundles.items()
        if bundle.state == STATE_SWITCHED and base.bundles[bundle_id].state != STATE_SWITCHED)


def unrecorded_commits(head: Ledger, bundle_id: int, commits: tuple[CommitFiles, ...]) -> tuple[str, ...]:
    # その束が持つファイルを 1 つでも触ったコミットのうち、記録の無いもの
    # Commits touching any file the bundle owns that have no follow record for it
    recorded = tuple(record.commit for record in head.records if record.bundle_id == bundle_id)
    return tuple(
        commit.sha for commit in commits
        if any(bundle_id in owning_bundles(path, head.rows) for path in commit.files)
        and not any(commit.sha.startswith(prefix) for prefix in recorded))


def check_switch_records(bundle_id: int, unrecorded: tuple[str, ...]) -> tuple[Violation, ...]:
    return tuple(
        Violation(RULE_SWITCH, f"束 {bundle_id} の暗転の起点より後のコミット {sha[:10]} がその束の Web パスを触ったが「## 追従記録」に無い。写したか捨てた（理由）を書く")
        for sha in unrecorded)
