#!/usr/bin/env bash
# アップロード境界の削除・保持と、削除失敗後の続行を検証する
# Verify retention at the upload boundary and continuation after removal failures

assert_release_evidence_retained() {
    local run="$1" path
    for path in unity-build-windows.log unity-build-mac.log steam/output/upload.log \
        steam/app_build_playtest.vdf steam/depot_build_windows.vdf steam/depot_build_mac.vdf; do
        [ -f "$run/$path" ] || fail "release evidence was deleted: $path"
    done
}

# 成功runだけを掃除し、隣のrunとログ・Steam出力・検証資料を残す
# Clean only the successful run, retaining its neighbor and all evidence
make_sandbox
other_run="$SANDBOX/runs/older-run"
mkdir -p "$other_run/build-windows" "$other_run/build-mac"
touch "$other_run/build-windows/keep" "$other_run/build-mac/keep"
OUTPUT=$(MOORESTECH_STEAM_BUILD_LABEL=cleanup-success run_target); STATUS=$?
run="$SANDBOX/runs/cleanup-success"
[ "$STATUS" -eq 0 ] || fail "cleanup success exited $STATUS: $OUTPUT"
assert_payloads_removed "$run"
assert_release_evidence_retained "$run"
[ -f "$run/verify/result.json" ] || fail "verification evidence was deleted"
[ -f "$run/promotion.md" ] || fail "cleanup prevented promotion instructions"
for os in windows mac; do
    [ -f "$other_run/build-$os/keep" ] || fail "another run's $os build was changed"
done

# ビルド・成果物検査・アップロード失敗では調査用の本体を消さない
# Preserve payloads for investigation on build, inspection, or upload failure
for assignment in UNITY_EXIT=1 UNITY_MAC_EXIT=1 BUILD_INFO_TARGET_WINDOWS=StandaloneOSX \
    BUILD_INFO_TARGET_MAC=StandaloneWindows64 STEAMCMD_EXIT=1; do
    make_sandbox
    OUTPUT=$(eval "$assignment" MOORESTECH_STEAM_BUILD_LABEL=before-upload run_target); STATUS=$?
    run="$SANDBOX/runs/before-upload"
    [ "$STATUS" -ne 0 ] || fail "$assignment did not fail"
    assert_payloads_retained "$run"
    grep -q '^rm ' "$SANDBOX/calls.log" && fail "$assignment attempted payload cleanup"
    grep -q '^verify ' "$SANDBOX/calls.log" && fail "$assignment reached verification"
    [ ! -f "$run/promotion.md" ] || fail "$assignment produced promotion instructions"
    case "$assignment" in
      UNITY_EXIT=*|BUILD_INFO_TARGET_WINDOWS=*) ;;
      *) [ -f "$run/build-mac/build-started" ] || fail "$assignment lost Mac build content" ;;
    esac
done

# 後続検証の失敗でも削除済みとし、証拠を残して手動反映を止める
# A later verification failure keeps payloads removed and evidence retained without promotion
make_sandbox
OUTPUT=$(VERIFY_EXIT=1 MOORESTECH_STEAM_BUILD_LABEL=verify-failure run_target); STATUS=$?
run="$SANDBOX/runs/verify-failure"
[ "$STATUS" -ne 0 ] || fail "verification failure did not fail"
assert_payloads_removed "$run"
assert_release_evidence_retained "$run"
[ -f "$run/verify/result.json" ] || fail "failed verification lost its evidence"
[ ! -f "$run/promotion.md" ] || fail "failed verification produced promotion instructions"

# 全失敗とWindowsだけ消える部分失敗でも、残存パスを警告して正常完了する
# Full and partial removal failures warn with remaining paths and still complete successfully
for mode in full partial; do
    make_sandbox
    OUTPUT=$(CLEANUP_FAILURE="$mode" MOORESTECH_STEAM_BUILD_LABEL=cleanup-failure run_target); STATUS=$?
    run="$SANDBOX/runs/cleanup-failure"
    [ "$STATUS" -eq 0 ] || fail "$mode cleanup failure stopped the run: $OUTPUT"
    case "$OUTPUT" in *'WARNING: アップロード済みビルド本体の削除に失敗しました'*) ;;
      *) fail "$mode cleanup failure did not warn";; esac
    case "$OUTPUT" in *"WARNING: 残存パス: $run/build-mac"*) ;;
      *) fail "$mode cleanup failure did not name remaining Mac payload";; esac
    [ -f "$run/build-mac/build-started" ] || fail "$mode cleanup did not retain Mac content"
    if [ "$mode" = full ]; then
        assert_payloads_retained "$run"
        case "$OUTPUT" in *"WARNING: 残存パス: $run/build-windows"*) ;;
          *) fail "full cleanup failure did not name remaining Windows payload";; esac
    else
        [ ! -e "$run/build-windows" ] || fail "partial cleanup kept Windows payload"
        case "$OUTPUT" in *"WARNING: 残存パス: $run/build-windows"*)
          fail "partial cleanup named a removed path";; esac
    fi
    grep -q '^verify ' "$SANDBOX/calls.log" || fail "$mode cleanup failure skipped verification"
    assert_release_evidence_retained "$run"
    [ -f "$run/verify/result.json" ] || fail "$mode cleanup failure lost verification evidence"
    [ -f "$run/promotion.md" ] || fail "$mode cleanup failure prevented promotion instructions"
done

# 別コミットの同時確保で敗者を止め、所有者の成果物を共有させない
# Stop the loser of simultaneous claims from sharing another commit's artifacts
make_sandbox
cat >"$SANDBOX/bin/mkdir" <<'EOF_MKDIR'
#!/bin/bash
set -eu
if [ "$#" -eq 2 ] && [ "$1" = -- ] && [ "$2" = "$PLAYTEST_RUN_ROOT/concurrent-run" ]; then
    race="${PLAYTEST_RUN_ROOT%/runs}/race"
    touch "$race/ready-$RACE_ID"
    for ((attempt=0; attempt<200; attempt++)); do
        [ -f "$race/ready-A" ] && [ -f "$race/ready-B" ] && break
        sleep 0.025
    done
    [ -f "$race/ready-A" ] && [ -f "$race/ready-B" ] || exit 90
    if /bin/mkdir -- "$2"; then
        echo "$RACE_ID" >"$race/owner"
        /bin/mkdir -p "$2/build-windows" "$2/build-mac" "$2/steam/output"
        for path in build-windows/keep build-mac/keep unity-build-windows.log \
            unity-build-mac.log steam/output/upload.log promotion.md; do
            echo owner-evidence >"$2/$path"
        done
        touch "$race/claimed"
        for ((attempt=0; attempt<200; attempt++)); do
            [ -f "$race/release" ] && exit 0
            sleep 0.025
        done
        echo 'owner barrier timed out' >&2
        exit 91
    fi
    exit 1
fi
exec /bin/mkdir "$@"
EOF_MKDIR
chmod +x "$SANDBOX/bin/mkdir"
mkdir -p "$SANDBOX/race"
for identity in A B; do
    (
        RACE_ID="$identity" GIT_VERIFY_OUTPUT="$identity${COMMIT:1}" \
            BUILD_INFO_COMMIT="$identity${COMMIT:1}" MOORESTECH_STEAM_BUILD_LABEL=concurrent-run \
            run_target >"$SANDBOX/race/output-$identity"
        echo "$?" >"$SANDBOX/race/status-$identity"
    ) &
    if [ "$identity" = A ]; then PID_A=$!; else PID_B=$!; fi
done
for ((attempt=0; attempt<200; attempt++)); do
    [ -f "$SANDBOX/race/claimed" ] && \
        { [ -f "$SANDBOX/race/status-A" ] || [ -f "$SANDBOX/race/status-B" ]; } && break
    sleep 0.025
done
if [ -f "$SANDBOX/race/claimed" ]; then
    owner=$(cat "$SANDBOX/race/owner")
    if [ "$owner" = A ]; then loser=B; else loser=A; fi
    [ "$(cat "$SANDBOX/race/status-$loser" 2>/dev/null)" = 2 ] || fail "concurrent loser did not exit 2"
    grep -q 'RUN_DIR.*既存・同時確保・IO失敗' "$SANDBOX/race/output-$loser" || fail "claim failure omitted its reason"
    grep -q "$SANDBOX/runs/concurrent-run" "$SANDBOX/race/output-$loser" || fail "claim failure omitted its path"
    grep -Eq '^(moores-wt|unity|steamcmd|rm|verify) ' "$SANDBOX/calls.log" && fail "loser reached worktree/build/delivery"
    for path in build-windows/keep build-mac/keep unity-build-windows.log \
        unity-build-mac.log steam/output/upload.log promotion.md; do
        [ "$(cat "$SANDBOX/runs/concurrent-run/$path" 2>/dev/null)" = owner-evidence ] || fail "loser modified owner evidence: $path"
    done
else
    fail "simultaneous run claims did not reach the ownership barrier"
fi
# 所有者を解放し、正常配布と片付けまで完走させる
# Release the owner and let normal delivery and cleanup finish
touch "$SANDBOX/race/release"
wait "$PID_A"
wait "$PID_B"
if [ -f "$SANDBOX/race/claimed" ]; then
    [ "$(cat "$SANDBOX/race/status-$owner")" = 0 ] || fail "concurrent owner failed"
    [ "$(grep -c '^moores-wt new ' "$SANDBOX/calls.log")" = 1 ] || fail "multiple worktrees were requested"
    assert_payloads_removed "$SANDBOX/runs/concurrent-run"
    assert_release_evidence_retained "$SANDBOX/runs/concurrent-run"
    [ -f "$SANDBOX/runs/concurrent-run/promotion.md" ] || fail "owner lost promotion instructions"
fi
