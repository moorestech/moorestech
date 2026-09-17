#!/usr/bin/env bash
# release-playtest.sh の出所まわり（同梱元 master とピンの突き合わせ・配布元 branch・告知のコミット表記）の契約テスト
# fail()で集計してFAILURES件数を末尾判定する方式のため、set -eは使わない(1件の失敗で打ち切らない)
# Contract tests for release-playtest.sh's origin handling (bundled master vs pin, distribution branch, announced commit)
# Aggregated via fail() and judged by FAILURES at the end, so set -e is not used (one failure must not abort the rest)
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/release-playtest-sandbox.sh
. "$SCRIPT_DIR/lib/release-playtest-sandbox.sh"

# 同梱元 master（worktree + ピンの relativePath）の HEAD がピンとずれていたら、ビルド前に合わせ先を示して止まる
# When the bundled master (worktree + the pin's relativePath) drifted from the pin, stop before the build and name what to align
make_sandbox
DRIFTED="0000000000000000000000000000000000beef"
OUTPUT=$(MASTER_HEAD_OUTPUT="$DRIFTED" run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "a drifted master data HEAD did not fail"
grep -q "^unity" "$SANDBOX/calls.log" && fail "unity ran despite a drifted master data HEAD"
case "$OUTPUT" in *"$SANDBOX/moorestech_master"*"$DRIFTED"*"$PINNED_MASTER"*) ;; *) fail "the drift failure did not name the path, its HEAD and the pinned commit: $OUTPUT";; esac
grep -q "^git -C $SANDBOX/moorestech_master rev-parse HEAD" "$SANDBOX/calls.log" || fail "the master HEAD was not read from worktree + relativePath"
grep -q "^git -C $SANDBOX/wt show HEAD:.moorestech-external-revisions.json" "$SANDBOX/calls.log" || fail "the pin was not read from the worktree's committed file"
grep -q "^moores-wt .*rm" "$SANDBOX/calls.log" || fail "worktree was not torn down after a master drift"

# 同梱元 master の HEAD を読めなければ（未配置等）ビルド前に理由付きで止まる
# When the bundled master's HEAD is unreadable (e.g. not placed), stop with a reason before the build
make_sandbox
OUTPUT=$(MASTER_HEAD_EXIT=128 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "an unreadable master data HEAD did not fail"
grep -q "^unity" "$SANDBOX/calls.log" && fail "unity ran despite an unreadable master data HEAD"
case "$OUTPUT" in *"$PINNED_MASTER"*) ;; *) fail "the unreadable-master failure did not name the pinned commit: $OUTPUT";; esac

# 配布元 branch は既定 master で Unity へ渡り、build-info.json の branch も照合される
# The distribution branch defaults to master, reaches Unity, and build-info.json's branch is verified
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "default branch run exited $STATUS: $OUTPUT"
grep -q "^unity .*branch=master$" "$SANDBOX/calls.log" || fail "MOORESTECH_BUILD_BRANCH=master was not passed to Unity"
grep -q "^git .*merge-base --is-ancestor $COMMIT origin/master" "$SANDBOX/calls.log" || fail "reachability from origin/master was not checked"

make_sandbox
OUTPUT=$(MOORESTECH_BUILD_BRANCH="release/steam" run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "explicit branch run exited $STATUS: $OUTPUT"
grep -q "^unity .*branch=release/steam$" "$SANDBOX/calls.log" || fail "an explicit MOORESTECH_BUILD_BRANCH was not passed to Unity"

# 要求コミットが配布元 ref に含まれなければ、嘘の branch を焼かないようビルド前に止まる
# A commit not contained in the distribution ref stops before the build so a false branch is never baked
make_sandbox
OUTPUT=$(GIT_ANCESTOR_EXIT=1 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "a commit outside origin/master did not fail"
grep -q "^moores-wt" "$SANDBOX/calls.log" && fail "a worktree was created despite a commit outside origin/master"
case "$OUTPUT" in *MOORESTECH_BUILD_BRANCH*) ;; *) fail "the unreachable failure did not point at MOORESTECH_BUILD_BRANCH: $OUTPUT";; esac
case "$OUTPUT" in *"見つかりません"*) fail "exit 1 (not an ancestor) was misreported as a missing ref: $OUTPUT" ;; esac

# origin/<branch> 自体が無い(merge-base --is-ancestorが128)場合は「含まれない」ではなく「見つからない」と出す
# ブランチ名の打ち間違いをコミットの取り違えと誤診させないため
# When origin/<branch> itself is missing (merge-base --is-ancestor returns 128), report "not found" rather than
# "not contained", so a typo'd branch name is never misdiagnosed as a commit mixup
make_sandbox
OUTPUT=$(GIT_ANCESTOR_EXIT=128 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "a missing origin/<branch> did not fail"
grep -q "^moores-wt" "$SANDBOX/calls.log" && fail "a worktree was created despite a missing origin/<branch>"
case "$OUTPUT" in *"見つかりません"*MOORESTECH_BUILD_BRANCH*) ;; *) fail "the missing-ref failure did not say 見つかりません and point at MOORESTECH_BUILD_BRANCH: $OUTPUT";; esac
case "$OUTPUT" in *"含まれません"*) fail "exit 128 (missing ref) was misreported as not contained: $OUTPUT" ;; esac

# 成果物の branch が配布元 ref と違えば（一時ブランチ名を焼いた等）steamcmd へ進まない
# An artifact whose branch differs from the distribution ref (e.g. the temporary branch was baked) never reaches steamcmd
make_sandbox
OUTPUT=$(BUILD_INFO_BRANCH="playtest/build-93ddfdab" run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "a baked temporary branch did not fail"
grep -q "^steamcmd" "$SANDBOX/calls.log" && fail "steamcmd ran despite a baked temporary branch"

# 告知と実行ログのコミットは解決前の入力ではなく40桁の解決結果
# The announced and logged commit is the resolved 40-char SHA, not the raw input
make_sandbox
FULL="0123456789abcdef0123456789abcdef01234567"
OUTPUT=$(RELEASE_ARG="origin/master" GIT_VERIFY_OUTPUT="$FULL" BUILD_INFO_COMMIT="$FULL" run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "origin/master input did not succeed: $OUTPUT"
grep -q "コミット: $FULL" "$SANDBOX"/runs/*/announce.md || fail "announce.md did not carry the resolved commit"
case "$OUTPUT" in *"commit=$FULL"*) ;; *) fail "the run log did not carry the resolved commit: $OUTPUT";; esac

# fetch に失敗したら解決にも worktree 作成にも進まない
# A failed fetch never proceeds to resolution or worktree creation
make_sandbox
# git スタブの case 先頭に fetch 失敗の分岐を差し込む（BSD sed の改行置換）
# Insert a failing-fetch branch at the top of the git stub's case (newline substitution for BSD sed)
sed -i '' 's|^case "\$\*" in$|case "$*" in\n  *"fetch origin"*) exit 1 ;;|' "$SANDBOX/bin/git"
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 2 ] || fail "a failed fetch did not exit 2 (got $STATUS): $OUTPUT"
grep -q "rev-parse --verify" "$SANDBOX/calls.log" && fail "rev-parse ran after a failed fetch"
grep -q "^moores-wt" "$SANDBOX/calls.log" && fail "a worktree was created after a failed fetch"

finish_contract "release-playtest origin contract"
