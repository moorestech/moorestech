#!/usr/bin/env bash
# Mac 配布の契約: 失敗時は理由を出し、Steam アップロード前に止まる
# Mac release contract: failures report a reason and stop before Steam upload
# fail() で集計するため set -e は使わない
# Aggregate with fail() instead of stopping at the first failure
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/release-playtest-sandbox.sh
. "$SCRIPT_DIR/lib/release-playtest-sandbox.sh"

# 同じ worktree から Windows、Mac の順に OS 別ディレクトリへ焼く
# Build Windows then Mac from one worktree into separate directories
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "success run exited $STATUS: $OUTPUT"
UNITY_LINES=$(grep "^unity" "$SANDBOX/calls.log")
echo "$UNITY_LINES" | sed -n 1p | grep -q "WindowsSteamPlaytestBuild" || fail "first unity call was not the Windows build"
echo "$UNITY_LINES" | sed -n 2p | grep -q "MacOsSteamPlaytestBuild" || fail "second unity call was not the Mac build"
[ "$(grep -c "^steamcmd" "$SANDBOX/calls.log")" -eq 1 ] || fail "steamcmd was not called exactly once"
ls -d "$SANDBOX"/runs/*/build-windows "$SANDBOX"/runs/*/build-mac >/dev/null 2>&1 || fail "per-OS build dirs were not created"

# run root の & と \ を sed の置換記号として解釈せず VDF にそのまま書く
# Keep ampersand and backslash in the run root literal when rendering VDFs with sed
make_sandbox
special_root="$SANDBOX/runs&back\\slash"
OUTPUT=$(PLAYTEST_RUN_ROOT="$special_root" MOORESTECH_STEAM_BUILD_LABEL=special-path run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "special-character run root exited $STATUS: $OUTPUT"
special_run="$special_root/special-path"
grep -Fqx "$(printf '\t"contentroot" "%s"' "$special_run")" "$special_run/steam/app_build_playtest.vdf" || fail "app VDF changed the special-character run root"
grep -Fqx "$(printf '\t"contentroot" "%s/build-windows"' "$special_run")" "$special_run/steam/depot_build_windows.vdf" || fail "Windows VDF changed the special-character run root"
grep -Fqx "$(printf '\t"contentroot" "%s/build-mac"' "$special_run")" "$special_run/steam/depot_build_mac.vdf" || fail "Mac VDF changed the special-character run root"

# Mac 側の失敗では steamcmd へ進まず、worktree を片付ける
# Mac failures do not reach steamcmd and still remove the worktree
for case_spec in "mac-build UNITY_MAC_EXIT=1" "codesign CODESIGN_EXIT=1" "universal LIPO_ARCHS=x86_64_arm64" \
    "intel LIPO_ARCHS=x86_64" "event-script MAC_LEAKS_EVENT_SCRIPT=1"; do
    name="${case_spec%% *}"
    assignment="${case_spec#* }"
    make_sandbox
    OUTPUT=$(eval "$assignment" run_target); STATUS=$?
    [ "$STATUS" -ne 0 ] || fail "$name did not fail the run"
    grep -q "^steamcmd" "$SANDBOX/calls.log" && fail "steamcmd ran despite $name"
    grep -q "^moores-wt .*rm" "$SANDBOX/calls.log" || fail "worktree was not torn down after $name"
done

# Mac depot ID が欠落・非数字ならビルド前に exit 2
# A missing or nonnumeric Mac depot ID exits 2 before building
for value in "" "12a"; do
    make_sandbox
    OUTPUT=$(MOORESTECH_STEAM_DEPOT_ID_MAC="$value" run_target); STATUS=$?
    [ "$STATUS" -eq 2 ] || fail "mac depot id '$value' did not exit 2 (got $STATUS)"
    [ ! -f "$SANDBOX/calls.log" ] || fail "mac depot id '$value' reached git/build"
    case "$OUTPUT" in *MOORESTECH_STEAM_DEPOT_ID_MAC*) ;; *) fail "mac depot id '$value' was not named";; esac
done

# Mac ffmpeg が欠落または LFS ポインタならビルド前に exit 3
# Missing Mac ffmpeg or an LFS pointer exits 3 before building
for state in missing lfs; do
    make_sandbox
    OUTPUT=$(FFMPEG_MAC_STATE="$state" run_target); STATUS=$?
    [ "$STATUS" -eq 3 ] || fail "mac ffmpeg '$state' did not exit 3 (got $STATUS)"
    grep -q "^unity" "$SANDBOX/calls.log" && fail "unity ran despite mac ffmpeg '$state'"
    case "$OUTPUT" in *macos-arm64*) ;; *) fail "mac ffmpeg '$state' failure did not name macos-arm64";; esac
done

# Mac ffmpeg の LICENSE 欠落はビルド前に理由付き exit 3
# A missing Mac ffmpeg LICENSE exits 3 with a reason before building
make_sandbox
OUTPUT=$(FFMPEG_MAC_LICENSE_STATE=missing run_target); STATUS=$?
[ "$STATUS" -eq 3 ] || fail "missing Mac ffmpeg LICENSE did not exit 3 (got $STATUS)"
grep -q "^unity" "$SANDBOX/calls.log" && fail "unity ran despite missing Mac ffmpeg LICENSE"
case "$OUTPUT" in *macos-arm64/LICENSE*) ;; *) fail "missing Mac ffmpeg LICENSE was not named";; esac

finish_contract "release-playtest mac contract"
