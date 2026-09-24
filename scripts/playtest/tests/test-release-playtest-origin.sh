#!/usr/bin/env bash
# release-playtest.sh の出所まわり（同梱元 master とピンの突き合わせ・配布元 branch・手動反映手順のコミット表記）の契約テスト
# fail()で集計してFAILURES件数を末尾判定する方式のため、set -eは使わない(1件の失敗で打ち切らない)
# Contract tests for release-playtest.sh's origin handling (bundled master vs pin, distribution branch, promotion commit)
# Aggregated via fail() and judged by FAILURES at the end, so set -e is not used (one failure must not abort the rest)
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/release-playtest-sandbox.sh
. "$SCRIPT_DIR/lib/release-playtest-sandbox.sh"

# 同梱元 master（ピンに一致する worktree）の HEAD がピンとずれていたら、ビルド前に合わせ先を示して止まる
# When the bundled master (the worktree matching the pin) drifted from the pin, stop before the build and name what to align
make_sandbox
DRIFTED="0000000000000000000000000000000000beef"
OUTPUT=$(MASTER_HEAD_OUTPUT="$DRIFTED" run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "a drifted master data HEAD did not fail"
grep -q "^unity" "$SANDBOX/calls.log" && fail "unity ran despite a drifted master data HEAD"
case "$OUTPUT" in *"$MASTER_PIN_DIR"*"$DRIFTED"*"$PINNED_MASTER"*) ;; *) fail "the drift failure did not name the path, its HEAD and the pinned commit: $OUTPUT";; esac
grep -q "^git -C $MASTER_PIN_DIR rev-parse HEAD" "$SANDBOX/calls.log" || fail "the master HEAD was not read from the pin worktree"
grep -q "^git -C $SANDBOX/wt show HEAD:.moorestech-external-revisions.json" "$SANDBOX/calls.log" || fail "the pin was not read from the worktree's committed file"
grep -q "^moores-wt .*rm" "$SANDBOX/calls.log" || fail "worktree was not torn down after a master drift"

# 同梱元 master の HEAD を読めなければ（未配置等）ビルド前に理由付きで止まる
# When the bundled master's HEAD is unreadable (e.g. not placed), stop with a reason before the build
make_sandbox
OUTPUT=$(MASTER_HEAD_EXIT=128 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "an unreadable master data HEAD did not fail"
grep -q "^unity" "$SANDBOX/calls.log" && fail "unity ran despite an unreadable master data HEAD"
case "$OUTPUT" in *"$PINNED_MASTER"*) ;; *) fail "the unreadable-master failure did not name the pinned commit: $OUTPUT";; esac

# ピンを HEAD に持つ master worktree が一つも無ければ、ピンと作り方（moores-wt new）を示してビルド前に止まる
# When no master worktree has the pin as HEAD, stop before the build naming the pin and how to create one (moores-wt new)
make_sandbox
OUTPUT=$(MASTER_LIST_HEAD="0000000000000000000000000000000000abcd" run_target); STATUS=$?
[ "$STATUS" -eq 3 ] || fail "no matching master worktree did not exit 3 (got $STATUS): $OUTPUT"
grep -q "^unity" "$SANDBOX/calls.log" && fail "unity ran despite no matching master worktree"
case "$OUTPUT" in *"$PINNED_MASTER"*"moores-wt new"*) ;; *) fail "the no-match failure did not name the pin and moores-wt new: $OUTPUT";; esac

# moores-wt が再利用した pin-<8桁> 以外の名前の一致 worktree でも解決し、それを Unity へ渡す
# A matching worktree reused by moores-wt under a name other than pin-<8> still resolves and reaches Unity
make_sandbox_named() { MASTER_MATCH_DIR_NAME="$1" make_sandbox; }
make_sandbox_named pin-587ce983
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "a matching worktree not named pin-<8> did not succeed: $OUTPUT"
grep -q "masterRoot=$SANDBOX/master-worktrees/pin-587ce983$" "$SANDBOX/calls.log" || fail "the reused non-pin-<8> worktree was not passed to Unity"

# メインclone も同じピンにいる（dirty）場合は、clean な pin worktree を選ぶ
# When the main clone also sits at the pin (dirty), the clean pin worktree is chosen
make_sandbox
OUTPUT=$(MASTER_MAIN_HEAD="$PINNED_MASTER" run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "a dirty main clone at the pin blocked a clean pin worktree: $OUTPUT"
grep -q "masterRoot=$MASTER_PIN_DIR$" "$SANDBOX/calls.log" || fail "the clean pin worktree was not preferred over the main clone"

# 同梱元 master・非公開アセットに未コミット変更があれば焼かない（出所を保証できない）
# Uncommitted changes in the bundled master or the private assets are never baked (provenance cannot be guaranteed)
for dirty in MASTER_DIRTY PRIVATE_DIRTY; do
    make_sandbox
    OUTPUT=$(eval "$dirty=1" run_target); STATUS=$?
    [ "$STATUS" -eq 3 ] || fail "$dirty did not exit 3 (got $STATUS): $OUTPUT"
    grep -q "^unity" "$SANDBOX/calls.log" && fail "unity ran despite $dirty"
    case "$OUTPUT" in *"git status --porcelain"*) ;; *) fail "$dirty failure did not say git status --porcelain: $OUTPUT";; esac
done

# 非公開アセットのピンずれ・ffmpeg の不在や LFS 殻はビルド前に止まる（strict が数十分後に落ちない）
# A drifted private-asset pin and a missing or LFS-husk ffmpeg stop before the build (strict never fails much later)
for case_env in "PRIVATE_HEAD_OUTPUT=0000000000000000000000000000000000cafe" "FFMPEG_STATE=missing" "FFMPEG_STATE=lfs"; do
    make_sandbox
    OUTPUT=$(eval "$case_env" run_target); STATUS=$?
    [ "$STATUS" -eq 3 ] || fail "$case_env did not exit 3 (got $STATUS): $OUTPUT"
    grep -q "^unity" "$SANDBOX/calls.log" && fail "unity ran despite $case_env"
done

# 配布元 branch は既定 master で Unity へ渡り、build-info.json の branch も照合される
# The distribution branch defaults to master, reaches Unity, and build-info.json's branch is verified
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "default branch run exited $STATUS: $OUTPUT"
grep -q "^unity .*branch=master masterRoot=" "$SANDBOX/calls.log" || fail "MOORESTECH_BUILD_BRANCH=master was not passed to Unity"
grep -q "masterRoot=$MASTER_PIN_DIR$" "$SANDBOX/calls.log" || fail "MOORESTECH_MASTER_DATA_ROOT was not the pin worktree"
grep -q "^git .*merge-base --is-ancestor $COMMIT origin/master" "$SANDBOX/calls.log" || fail "reachability from origin/master was not checked"

make_sandbox
OUTPUT=$(MOORESTECH_BUILD_BRANCH="release/steam" run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "explicit branch run exited $STATUS: $OUTPUT"
grep -q "^unity .*branch=release/steam masterRoot=" "$SANDBOX/calls.log" || fail "an explicit MOORESTECH_BUILD_BRANCH was not passed to Unity"

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
grep -q "コミット: $FULL" "$SANDBOX"/runs/*/promotion.md || fail "promotion.md did not carry the resolved commit"
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
