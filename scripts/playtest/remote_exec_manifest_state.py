"""箱の manifest から遠隔実行の印の状態を判定する（inbox-poller.sh / enqueue-autofix.sh の投入判定用）。
印の判定はダイジェストと同じ manifest 契約（digest_schema.MANIFEST_SCHEMA）で行い、両者の解釈を揃える。
出力: 有効・不明なら 1（不明の理由は標準エラーへ）、無効なら 0 を標準出力へ。manifest が読めない・契約違反なら理由を標準エラーへ出して終了コード 1。

Decide from a bundle manifest what state the remote-exec mark is in (used by inbox-poller.sh / enqueue-autofix.sh).
The mark is read with the same manifest contract as the digest (digest_schema.MANIFEST_SCHEMA) so both agree.
Output: prints 1 for enabled and unknown (an unknown's reason goes to stderr) and 0 for disabled; an unreadable or non-conforming manifest prints the reason to stderr and exits 1.
"""
import sys
from pathlib import Path

from digest_schema import MANIFEST_SCHEMA, REMOTE_EXEC_DISABLED, read_conformed, remote_exec_state


def enqueue_remote_exec_state(path: Path) -> tuple[bool | None, str | None]:
    """自動修正ランへ通してよいかを返す。不明は遮断側（True）へ寄せ、その理由も返す
    Returns whether the box must be held back from an auto-fix run; unknown is held (True) and carries its reason"""
    manifest, reason = read_conformed(path, MANIFEST_SCHEMA)
    if manifest is None:
        return None, reason
    state, unknown_reason = remote_exec_state(manifest)
    return state != REMOTE_EXEC_DISABLED, unknown_reason or None


if __name__ == "__main__":
    marked, problem = enqueue_remote_exec_state(Path(sys.argv[1]))
    if marked is None:
        print(problem, file=sys.stderr)
        sys.exit(1)
    # 遮断理由（不明）は出力に混ぜず標準エラーへ。投入判定は標準出力の 0/1 だけで決まる
    # An unknown's reason goes to stderr rather than stdout; the decision rests on the 0/1 alone
    if problem is not None:
        print(problem, file=sys.stderr)
    print("1" if marked else "0")
