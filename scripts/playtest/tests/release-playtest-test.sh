#!/bin/bash
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TARGET="$SCRIPT_DIR/../release-playtest.sh"
FAILURES=0
COMMIT="93ddfdab3ffffffffffffffffffffffffffffffff"

fail() { echo "FAIL: $1"; FAILURES=$((FAILURES + 1)); }

make_sandbox() {
    SANDBOX="$(mktemp -d)"
    mkdir -p "$SANDBOX/bin" "$SANDBOX/runs"

    # 呼び出し順と引数を1ファイルへ記録するスタブ群（bash 3.2 で動く書き方に限る）
    # Stubs recording invocation order and arguments into one file (written for bash 3.2)
    cat >"$SANDBOX/bin/moores-wt" <<EOF
#!/bin/bash
echo "moores-wt \$*" >>"$SANDBOX/calls.log"
mkdir -p "$SANDBOX/wt/moorestech_client"
echo "$SANDBOX/wt"
EOF
    cat >"$SANDBOX/bin/unity" <<EOF
#!/bin/bash
echo "unity \$*" >>"$SANDBOX/calls.log"
[ "\${UNITY_EXIT:-0}" = "0" ] || exit "\${UNITY_EXIT}"
mkdir -p "\$MOORESTECH_BUILD_OUTPUT/moorestech_Data/StreamingAssets" "\$MOORESTECH_BUILD_OUTPUT/game/mods"
touch "\$MOORESTECH_BUILD_OUTPUT/moorestech.exe"
printf '{"commit":"%s","steamBuildLabel":"%s"}' "$COMMIT" "\$MOORESTECH_STEAM_BUILD_LABEL" \
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
    chmod +x "$SANDBOX/bin/moores-wt" "$SANDBOX/bin/unity" "$SANDBOX/bin/steamcmd" "$SANDBOX/bin/verify"
}

run_target() {
    ( cd "$SANDBOX" && \
      MOORESTECH_STEAM_USER="${MOORESTECH_STEAM_USER-steamuser}" \
      MOORESTECH_STEAM_DEPOT_ID="${MOORESTECH_STEAM_DEPOT_ID-1958161}" \
      MOORES_WT_BIN="$SANDBOX/bin/moores-wt" UNITY_BIN="$SANDBOX/bin/unity" \
      STEAMCMD_BIN="$SANDBOX/bin/steamcmd" VERIFY_SCRIPT="$SANDBOX/bin/verify" \
      PLAYTEST_RUN_ROOT="$SANDBOX/runs" \
      UNITY_EXIT="${UNITY_EXIT-0}" STEAMCMD_EXIT="${STEAMCMD_EXIT-0}" VERIFY_EXIT="${VERIFY_EXIT-0}" \
      bash "$TARGET" "$COMMIT" 2>&1 )
}

# 成功系: worktree→unity→steamcmd→verify の順に呼ばれ、告知テキストが出る
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "success run exited $STATUS: $OUTPUT"
ORDER=$(awk '{print $1}' "$SANDBOX/calls.log" | tr '\n' ' ')
[ "$ORDER" = "moores-wt unity steamcmd verify " ] || fail "call order was: $ORDER"
grep -q "run_app_build" "$SANDBOX/calls.log" || fail "steamcmd was not asked to run_app_build"
ls "$SANDBOX"/runs/*/announce.md >/dev/null 2>&1 || fail "announce.md was not written"
grep -q "__DEPOT_ID__" "$SANDBOX"/runs/*/steam/*.vdf && fail "vdf still contains a raw token"
grep -q "1958161" "$SANDBOX"/runs/*/steam/depot_build_windows.vdf || fail "depot id was not substituted"

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

if [ "$FAILURES" -ne 0 ]; then
    echo "FAILED: $FAILURES contract checks"
    exit 1
fi
echo "PASS: release-playtest contract"
