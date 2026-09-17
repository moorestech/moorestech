#!/usr/bin/env bash
# fail()で集計してFAILURES件数を末尾判定する方式のため、set -eは使わない(1件の失敗で打ち切らない)
# Aggregated via fail() and judged by FAILURES at the end, so set -e is not used (one failure must not abort the rest)
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TARGET="$SCRIPT_DIR/../release-playtest.sh"
FAILURES=0
COMMIT="93ddfdab3ffffffffffffffffffffffffffffffff"

fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }

# 各make_sandboxが作った一時ディレクトリを配列に積み、終了時にまとめて削除する
# Every temp dir created by make_sandbox is tracked here and removed together on exit
SANDBOXES=()
cleanup() { for dir in "${SANDBOXES[@]}"; do rm -rf "$dir"; done; }
trap cleanup EXIT

make_sandbox() {
    SANDBOX="$(mktemp -d)"
    SANDBOXES+=("$SANDBOX")
    mkdir -p "$SANDBOX/bin" "$SANDBOX/runs"

    # 呼び出し順と引数を1ファイルへ記録するスタブ群（bash 3.2 で動く書き方に限る）
    # Stubs recording invocation order and arguments into one file (written for bash 3.2)
    # COMMIT解決とworktreeのHEAD照合をスタブする。既定は両方ともCOMMITそのものを返すので、
    # 既存の成功系フローはこのスタブを挟んでも変わらない
    # Stubs commit resolution and the worktree HEAD check; both default to echoing COMMIT itself,
    # so the existing happy-path flow is unaffected by inserting this stub
    cat >"$SANDBOX/bin/git" <<EOF
#!/bin/bash
echo "git \$*" >>"$SANDBOX/calls.log"
case "\$*" in
  *"rev-parse --verify"*)
    [ "\${GIT_VERIFY_EXIT:-0}" = "0" ] || exit "\${GIT_VERIFY_EXIT}"
    echo "\${GIT_VERIFY_OUTPUT:-$COMMIT}"
    ;;
  *"rev-parse HEAD"*)
    echo "\${GIT_HEAD_OUTPUT:-\${GIT_VERIFY_OUTPUT:-$COMMIT}}"
    ;;
esac
EOF
    cat >"$SANDBOX/bin/moores-wt" <<EOF
#!/bin/bash
echo "moores-wt \$*" >>"$SANDBOX/calls.log"
case "\$1" in
  rm) exit "\${MOORES_WT_RM_EXIT:-0}" ;;
esac
# 既存の(stale)worktreeディレクトリを模して先に作ってから失敗するケースをMOORES_WT_EXITで再現する
# MOORES_WT_EXIT reproduces a stale worktree dir that already exists before the command fails
mkdir -p "$SANDBOX/wt/moorestech_client"
echo "$SANDBOX/wt"
exit "\${MOORES_WT_EXIT:-0}"
EOF
    cat >"$SANDBOX/bin/unity" <<EOF
#!/bin/bash
echo "unity \$*" >>"$SANDBOX/calls.log"
[ "\${UNITY_EXIT:-0}" = "0" ] || exit "\${UNITY_EXIT}"
mkdir -p "\$MOORESTECH_BUILD_OUTPUT/moorestech_Data/StreamingAssets" "\$MOORESTECH_BUILD_OUTPUT/game/mods"
touch "\$MOORESTECH_BUILD_OUTPUT/moorestech.exe"
printf '{"commit":"%s","steamBuildLabel":"%s","target":"StandaloneWindows64"}' \
  "\${BUILD_INFO_COMMIT:-$COMMIT}" "\$MOORESTECH_STEAM_BUILD_LABEL" \
  >"\$MOORESTECH_BUILD_OUTPUT/moorestech_Data/StreamingAssets/build-info.json"
EOF
    cat >"$SANDBOX/bin/steamcmd" <<EOF
#!/bin/bash
echo "steamcmd \$*" >>"$SANDBOX/calls.log"
exit "\${STEAMCMD_EXIT:-0}"
EOF
    cat >"$SANDBOX/bin/verify" <<EOF
#!/bin/bash
echo "verify \$*" >>"$SANDBOX/calls.log"
exit "\${VERIFY_EXIT:-0}"
EOF
    chmod +x "$SANDBOX/bin/git" "$SANDBOX/bin/moores-wt" "$SANDBOX/bin/unity" "$SANDBOX/bin/steamcmd" "$SANDBOX/bin/verify"
}

run_target() {
    ( cd "$SANDBOX" && \
      MOORESTECH_STEAM_USER="${MOORESTECH_STEAM_USER-steamuser}" \
      MOORESTECH_STEAM_DEPOT_ID="${MOORESTECH_STEAM_DEPOT_ID-1958161}" \
      MOORESTECH_STEAM_BUILD_LABEL="${MOORESTECH_STEAM_BUILD_LABEL-}" \
      GIT_BIN="$SANDBOX/bin/git" \
      MOORES_WT_BIN="$SANDBOX/bin/moores-wt" UNITY_BIN="$SANDBOX/bin/unity" \
      STEAMCMD_BIN="$SANDBOX/bin/steamcmd" VERIFY_SCRIPT="$SANDBOX/bin/verify" \
      PLAYTEST_RUN_ROOT="$SANDBOX/runs" \
      UNITY_EXIT="${UNITY_EXIT-0}" STEAMCMD_EXIT="${STEAMCMD_EXIT-0}" VERIFY_EXIT="${VERIFY_EXIT-0}" \
      MOORES_WT_EXIT="${MOORES_WT_EXIT-0}" MOORES_WT_RM_EXIT="${MOORES_WT_RM_EXIT-0}" \
      GIT_VERIFY_EXIT="${GIT_VERIFY_EXIT-0}" GIT_VERIFY_OUTPUT="${GIT_VERIFY_OUTPUT-}" GIT_HEAD_OUTPUT="${GIT_HEAD_OUTPUT-}" \
      BUILD_INFO_COMMIT="${BUILD_INFO_COMMIT-$COMMIT}" \
      bash "$TARGET" "${RELEASE_ARG-$COMMIT}" 2>&1 )
}

# 成功系: worktree→unity→steamcmd→verify→worktree破棄(trap) の順に呼ばれ、告知テキストが出る
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "success run exited $STATUS: $OUTPUT"
ORDER=$(awk '{print $1}' "$SANDBOX/calls.log" | tr '\n' ' ')
[ "$ORDER" = "git git moores-wt git unity steamcmd verify moores-wt " ] || fail "call order was: $ORDER"
grep -q "run_app_build" "$SANDBOX/calls.log" || fail "steamcmd was not asked to run_app_build"
ls "$SANDBOX"/runs/*/announce.md >/dev/null 2>&1 || fail "announce.md was not written"
grep -q "__[A-Z_]*__" "$SANDBOX"/runs/*/steam/*.vdf && fail "vdf still contains a raw token"
grep -q "1958161" "$SANDBOX"/runs/*/steam/depot_build_windows.vdf || fail "depot id was not substituted"
grep -q "$SANDBOX/runs" "$SANDBOX"/runs/*/steam/*.vdf || fail "contentroot was not pointed at the run's build dir"
grep -q "^moores-wt .*rm" "$SANDBOX/calls.log" || fail "the disposable worktree was not torn down"

# 必須envの欠落はビルド前に落ちる
make_sandbox
OUTPUT=$(MOORESTECH_STEAM_DEPOT_ID="" run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "missing depot id did not fail"
[ ! -f "$SANDBOX/calls.log" ] || fail "missing env reached the build"
case "$OUTPUT" in *MOORESTECH_STEAM_DEPOT_ID*) ;; *) fail "missing env did not name the variable";; esac

# ビルド失敗ならsteamcmdへ進まない
make_sandbox
OUTPUT=$(UNITY_EXIT=1 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "build failure did not fail the run"
grep -q "^steamcmd" "$SANDBOX/calls.log" && fail "steamcmd ran after a failed build"

# 検証機の通し検証が落ちたら告知テキストを書かない
make_sandbox
OUTPUT=$(VERIFY_EXIT=1 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "verification failure did not fail the run"
ls "$SANDBOX"/runs/*/announce.md >/dev/null 2>&1 && fail "announce.md was written for a failed verification"

# moores-wtがstaleな(既に存在する)worktreeディレクトリを残しつつ非0終了しても、
# set -o pipefailがパイプの失敗を伝搬させ、そのまま後続(unity等)へ進まない
# Even when moores-wt leaves a stale worktree dir behind before exiting non-zero,
# set -o pipefail must propagate the pipe failure so the run never reaches unity/steamcmd
make_sandbox
OUTPUT=$(MOORES_WT_EXIT=1 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "moores-wt failure did not fail the run (pipefail missing?)"
grep -q "^unity" "$SANDBOX/calls.log" 2>/dev/null && fail "unity ran after moores-wt failed"

# build-info.jsonの他キーの値がたまたまBUILD_LABELと一致しても、steamBuildLabelキー名まで見て弾く
# A coincidental match on another key's value must not pass; the check must name the steamBuildLabel key
make_sandbox
cat >"$SANDBOX/bin/unity" <<EOF
#!/bin/bash
echo "unity \$*" >>"$SANDBOX/calls.log"
mkdir -p "\$MOORESTECH_BUILD_OUTPUT/moorestech_Data/StreamingAssets" "\$MOORESTECH_BUILD_OUTPUT/game/mods"
touch "\$MOORESTECH_BUILD_OUTPUT/moorestech.exe"
printf '{"commit":"%s","someOtherField":"%s"}' "$COMMIT" "\$MOORESTECH_STEAM_BUILD_LABEL" \
  >"\$MOORESTECH_BUILD_OUTPUT/moorestech_Data/StreamingAssets/build-info.json"
EOF
chmod +x "$SANDBOX/bin/unity"
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "build-info.json missing steamBuildLabel key did not fail"
grep -q "^steamcmd" "$SANDBOX/calls.log" 2>/dev/null && fail "steamcmd ran despite missing steamBuildLabel key"

# 成果物のcommitが指定コミットと違えばsteamcmdへ進まない(stale worktree/取り違え対策)
# An artifact whose commit differs from the requested one must never reach steamcmd (stale worktree / mix-up guard)
make_sandbox
OUTPUT=$(BUILD_INFO_COMMIT="0000000000000000000000000000000000dead" run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "a commit mismatch did not fail"
grep -q "^steamcmd" "$SANDBOX/calls.log" 2>/dev/null && fail "steamcmd ran despite a commit mismatch"

# 同じラベルのRUN_DIRが既にあれば再利用せず即座に落ちる(前回残骸の混入対策)
# An already-existing RUN_DIR for the same label is refused instead of reused (guards against stale leftovers)
make_sandbox
MOORESTECH_STEAM_BUILD_LABEL="playtest-reuse-test" run_target >/dev/null 2>&1
OUTPUT=$(MOORESTECH_STEAM_BUILD_LABEL="playtest-reuse-test" run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "a reused RUN_DIR did not fail"
case "$OUTPUT" in *"RUN_DIR"*) ;; *) fail "reused RUN_DIR failure did not name RUN_DIR";; esac

# 短縮SHA等の入力でも、git rev-parseで40桁へ解決してから成功する
# A short SHA (or any git-revparse-able input) resolves to the 40-char SHA and the run still succeeds
make_sandbox
FULL="0123456789abcdef0123456789abcdef01234567"
OUTPUT=$(RELEASE_ARG="0123456" GIT_VERIFY_OUTPUT="$FULL" GIT_HEAD_OUTPUT="$FULL" BUILD_INFO_COMMIT="$FULL" run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "short SHA input did not succeed: $OUTPUT"
grep -q "^steamcmd" "$SANDBOX/calls.log" || fail "short SHA input never reached steamcmd"

# 入力コミットを解決できなければビルド前に落ちる
# An unresolvable commit input fails before the build starts
make_sandbox
OUTPUT=$(GIT_VERIFY_EXIT=1 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "an unresolvable commit did not fail"
grep -q "^unity" "$SANDBOX/calls.log" 2>/dev/null && fail "unity ran despite an unresolvable commit"

# worktreeのHEADが要求コミットとずれていたら、ビルドへ入らずsteamcmdへも進まない
# A worktree whose HEAD drifted from the requested commit never reaches the build or steamcmd
make_sandbox
OUTPUT=$(GIT_HEAD_OUTPUT="0000000000000000000000000000000000dead" run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "a worktree HEAD mismatch did not fail"
grep -q "^unity" "$SANDBOX/calls.log" 2>/dev/null && fail "unity ran despite a worktree HEAD mismatch"
grep -q "^moores-wt .*rm" "$SANDBOX/calls.log" || fail "worktree was not torn down after a HEAD mismatch"

# fetchがrev-parse --verifyより前に来ることを確認する(未fetchのコミット/古いorigin/masterを掴まないため)
# fetch must precede rev-parse --verify (guards against an unfetched commit or a stale origin/master ref)
make_sandbox
run_target >/dev/null 2>&1
FETCH_LINE=$(grep -n "fetch origin" "$SANDBOX/calls.log" | head -n1 | cut -d: -f1)
VERIFY_LINE=$(grep -n "rev-parse --verify" "$SANDBOX/calls.log" | head -n1 | cut -d: -f1)
[ -n "$FETCH_LINE" ] || fail "fetch origin was not called"
[ -n "$VERIFY_LINE" ] || fail "rev-parse --verify was not called"
[ "$FETCH_LINE" -lt "$VERIFY_LINE" ] || fail "fetch did not happen before rev-parse --verify"

if [ "$FAILURES" -ne 0 ]; then
    echo "FAILED: $FAILURES contract checks"
    exit 1
fi
echo "PASS: release-playtest contract"
