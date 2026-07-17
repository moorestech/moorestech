#!/bin/bash
# tart-vm skill の Stop hook。PR未更新なら停止をブロックし続行を強制する
# Stop hook for the tart-vm skill; blocks stopping until the PR is updated
set -u

INPUT=$(cat)
json() { jq -r "$1" <<<"$INPUT"; }

# Tart VM 以外では何もしない（誤発動ガード。テスト時は環境変数で迂回）
# No-op outside the Tart VM (misfire guard; bypass via env var in tests)
if [ "${ENSURE_PR_HOOK_TEST:-0}" != "1" ]; then
    uname -a | grep -q VMAPPLE || exit 0
fi

# git repo 外・cwd 不明なら判定不能として通す
# Allow stop when cwd is unknown or not a git repository
CWD=$(json '.cwd')
cd "$CWD" 2>/dev/null || exit 0
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || exit 0

# セッション単位のブロック回数カウンタ（3回で諦めて停止を許可＝無限ループ防止）
# Per-session block counter; yield after 3 blocks to avoid infinite loops
SESSION_ID=$(json '.session_id')
COUNT_FILE="${TMPDIR:-/tmp}/tart-vm-pr-hook-${SESSION_ID}.count"

block() {
    local count=0
    [ -f "$COUNT_FILE" ] && count=$(cat "$COUNT_FILE")
    count=$((count + 1))
    echo "$count" >"$COUNT_FILE"
    if [ "$count" -gt 3 ]; then
        exit 0
    fi
    jq -cn --arg r "$1 （tart-vm skill §5。どうしても満たせない場合は理由を最終報告に明記して再度終了すること）" \
        '{decision: "block", reason: $r}'
    exit 0
}

pass() {
    rm -f "$COUNT_FILE"
    exit 0
}

# 1. 未コミットの変更が残っていたらブロック
# 1. Block if uncommitted changes remain
BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [ -n "$(git status --porcelain)" ]; then
    block "未コミットの変更が残っている。task branch（現在: ${BRANCH}）にコミットし、pushしてPRを作成/更新してから終了すること。"
fi

# master 直上・detached HEAD で差分ゼロなら PR 対象なし
# Nothing to PR when clean on master or a detached HEAD
if [ "$BRANCH" = "master" ] || [ "$BRANCH" = "HEAD" ]; then
    pass
fi

# 2. master から進んだコミットが無ければ調査のみのタスクとして通す
# 2. Allow stop when no commits ahead of master (investigation-only task)
if git rev-parse --verify -q origin/master >/dev/null; then
    AHEAD=$(git rev-list --count origin/master..HEAD)
    [ "$AHEAD" -eq 0 ] && pass
fi

# 3. 未push（upstream無し・ローカル先行）ならブロック
# 3. Block if the branch is unpushed (no upstream or local commits ahead)
if ! git rev-parse --verify -q '@{u}' >/dev/null; then
    block "ブランチ ${BRANCH} がpushされていない。git push -u origin ${BRANCH} でpushし、pr-create skillでPRを作成すること。"
fi
if [ -n "$(git log '@{u}..HEAD' --oneline)" ]; then
    block "未pushのコミットがある。pushしてPRを更新すること。"
fi

# 4. このブランチの open な PR が無ければブロック
# 4. Block if this branch has no open PR
PR_STATE=$(gh pr view --json state -q .state 2>/dev/null || echo "NONE")
if [ "$PR_STATE" != "OPEN" ]; then
    block "このブランチのopenなPRが存在しない（state: ${PR_STATE}）。pr-create skill（またはgh pr create）でPRを作成し、URLを最終報告に記載すること。"
fi

pass
