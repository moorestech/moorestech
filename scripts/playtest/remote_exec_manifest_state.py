"""箱の manifest から遠隔実行の印の有無を判定する（enqueue-autofix.sh の投入判定用）。
印の判定はダイジェストと同じ manifest 契約（digest_schema.MANIFEST_SCHEMA）で行い、両者の解釈を揃える。
出力: 印ありなら 1、なしなら 0 を標準出力へ。manifest が読めない・契約違反なら理由を標準エラーへ出して終了コード 1。

Decide from a bundle manifest whether the remote-exec mark is present (used by enqueue-autofix.sh).
The mark is read with the same manifest contract as the digest (digest_schema.MANIFEST_SCHEMA) so both agree.
Output: prints 1 when marked and 0 otherwise; an unreadable or non-conforming manifest prints the reason to stderr and exits 1.
"""
import sys
from pathlib import Path

from digest_schema import MANIFEST_SCHEMA, read_conformed


def enqueue_remote_exec_state(path: Path) -> tuple[bool | None, str | None]:
    manifest, reason = read_conformed(path, MANIFEST_SCHEMA)
    if manifest is None:
        return None, reason
    return manifest["remoteExec"] is not None, None


if __name__ == "__main__":
    marked, problem = enqueue_remote_exec_state(Path(sys.argv[1]))
    if problem is not None:
        print(problem, file=sys.stderr)
        sys.exit(1)
    print("1" if marked else "0")
