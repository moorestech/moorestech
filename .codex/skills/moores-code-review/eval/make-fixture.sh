#!/usr/bin/env bash
# レビュー当時のPR diffをgit履歴から再生成する（コミットはマージ済みなので永続）
# Regenerate the at-review-time PR diff from git history (commits are merged, hence permanent)
#
# Usage: ./make-fixture.sh <label> [out_dir]
#        ./make-fixture.sh all [out_dir]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TSV="$SCRIPT_DIR/fixtures.tsv"
OUT_DIR="${2:-/tmp/moores-review-fixtures}"
mkdir -p "$OUT_DIR"

gen() {
    local label="$1" review="$2" merge="$3"
    # マージコミットの第1親=当時のベース先端。そのfork点からreview時点までがPR diff
    # First parent of the merge commit = base tip at merge; diff from its fork point to the reviewed commit
    # 未マージPRは merge 列に base=<commit> でベースを直指定する
    # For unmerged PRs, the merge column carries base=<commit> to pin the base directly
    local base
    if [[ "$merge" == base=* ]]; then
        base="${merge#base=}"
    else
        base=$(git merge-base "${merge}^1" "$review")
    fi
    git diff "$base" "$review" > "$OUT_DIR/$label.diff"
    echo "$OUT_DIR/$label.diff ($(wc -l < "$OUT_DIR/$label.diff") lines)"
}

grep -v '^#' "$TSV" | while IFS=$'\t' read -r label review merge _; do
    [ -z "$label" ] && continue
    if [ "$1" = "all" ] || [ "$1" = "$label" ]; then
        gen "$label" "$review" "$merge"
    fi
done
