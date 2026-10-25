"""Resolve Web changes against the ledger at each history boundary."""

from pathlib import Path

from . import git_history
from .ledger import Ledger, parse_ledger
from .ownership import owning_bundles

LEDGER_PATH = "docs/uitk/TODO.md"


def commit_owners(root: Path, head: str, commits: tuple[git_history.CommitFiles, ...]) -> tuple[dict[str, frozenset[int]], tuple[str, ...]]:
    # 導入前だけ初版で帰属を定め、導入後の欠落は拒否する
    # Only pre-introduction history uses the first ledger; later omissions fail
    introductions = git_history.file_additions(root, head, LEDGER_PATH)
    if not introductions:
        return {}, ("履歴に台帳の導入コミットが無い",)
    first = introductions[0]
    initial = parse_ledger(git_history.read_file_at(root, first, LEDGER_PATH))
    if initial.errors:
        return {}, tuple(f"履歴台帳 {first[:10]}: {error}" for error in initial.errors)
    cache: dict[str, Ledger] = {}
    errors: list[str] = []
    owners: dict[str, frozenset[int]] = {}
    for commit in commits:
        boundaries = (commit.sha, *git_history.parents(root, commit.sha))
        owned: set[int] = set()
        for rev in boundaries:
            if rev not in cache:
                text = git_history.read_file_at(root, rev, LEDGER_PATH)
                if text is None:
                    if any(git_history.is_ancestor(root, intro, rev) for intro in introductions):
                        errors.append(f"履歴台帳 {rev[:10]}: 導入後に {LEDGER_PATH} が無い")
                        continue
                    print(f"G1: 台帳導入前 {rev[:10]} の所有は初版 {first[:10]} で判定する")
                    cache[rev] = initial.ledger
                else:
                    parsed = parse_ledger(text)
                    if parsed.errors:
                        errors.extend(f"履歴台帳 {rev[:10]}: {error}" for error in parsed.errors)
                        continue
                    cache[rev] = parsed.ledger
            # 改名・削除の前後を合わせ、旧パスの追従義務を保持する
            # Union both sides of renames and deletions to retain old-path obligations
            for path in commit.files:
                owned.update(owning_bundles(path, cache[rev].rows))
        owners[commit.sha] = frozenset(owned)
    return owners, tuple(errors)
