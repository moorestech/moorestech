#!/usr/bin/env bash
# Mac 配布の契約: 失敗時は理由を出し、Steam アップロード前に止まる
# Mac release contract: failures report a reason and stop before Steam upload
# fail() で集計するため set -e は使わない
# Aggregate with fail() instead of stopping at the first failure
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/release-playtest-sandbox.sh
. "$SCRIPT_DIR/lib/release-playtest-sandbox.sh"
# 必須パス列は検査の正本から読む。検査項目を足せば欠損ケースが自動で増える
# Read the required paths from the checker itself, so a new check adds its missing case automatically
# shellcheck source=../lib/release-artifact.sh
. "$SCRIPT_DIR/../lib/release-artifact.sh"

# 検査の正本から読む必須パス列そのものが縮んでいないことを先に確かめる（縮小改変を緑にしないため）
# Check the sourced required-path list itself has not shrunk, so a removal cannot pass silently
for expected_path in "moorestech.app/Contents/MacOS/moorestech" "moorestech.app/Contents/MacOS/ffmpeg" \
    "moorestech.app/Contents/Resources/ffmpeg-LICENSE.txt" \
    "moorestech.app/Contents/Resources/Data/StreamingAssets/build-info.json" "game/mods"; do
    case " ${MAC_REQUIRED_RELATIVE_PATHS[*]} " in
      *" $expected_path "*) ;;
      *) fail "MAC_REQUIRED_RELATIVE_PATHS lost $expected_path";;
    esac
done

# CEF の名前の列そのものが縮んでいないことを先に確かめる（縮小改変を緑にしないため）
# Check the sourced CEF name list itself has not shrunk, so a removal cannot pass silently
for expected_pattern in "cef-unity-*" "cef_unity_*" "libcef*" "CefUnity.*" "chrome_elf.dll" "icudtl.dat" "v8_context_snapshot.bin" "chrome_*_percent.pak"; do
    case " ${CEF_RUNTIME_NAME_PATTERNS[*]} " in
      *" $expected_pattern "*) ;;
      *) fail "CEF_RUNTIME_NAME_PATTERNS lost $expected_pattern";;
    esac
done

# find が失敗する成果物（読めないディレクトリ）は「CEF 0」と見なさず exit 4 で止める
# An artifact whose find fails (unreadable directory) is not read as CEF-free and stops with exit 4
if [ "$(id -u)" -ne 0 ]; then
    UNREADABLE_ROOT="$(mktemp -d)"
    mkdir -p "$UNREADABLE_ROOT/locked"
    chmod 000 "$UNREADABLE_ROOT/locked"
    OUTPUT=$( (release_require_no_cef_files "$UNREADABLE_ROOT") 2>&1 ); STATUS=$?
    chmod 755 "$UNREADABLE_ROOT/locked"; rm -rf "$UNREADABLE_ROOT"
    [ "$STATUS" -eq 4 ] || fail "find failure under the artifact did not exit 4 (got $STATUS)"
    case "$OUTPUT" in *'find が失敗'*) ;; *) fail "find failure reason was absent: $OUTPUT";; esac
else
    # root では chmod 000 でも読めてしまい失敗を作れないので、飛ばしたことを出す
    # As root chmod 000 stays readable, so the failure cannot be staged; say it was skipped
    echo "SKIP: find failure case needs a non-root user"
fi

# 同じ worktree から Windows、Mac の順に OS 別ディレクトリへ焼く
# Build Windows then Mac from one worktree into separate directories
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "success run exited $STATUS: $OUTPUT"
UNITY_LINES=$(grep "^unity" "$SANDBOX/calls.log")
echo "$UNITY_LINES" | sed -n 1p | grep -q "WindowsSteamPlaytestBuild" || fail "first unity call was not the Windows build"
echo "$UNITY_LINES" | sed -n 2p | grep -q "MacOsSteamPlaytestBuild" || fail "second unity call was not the Mac build"
[ "$(grep -c "^steamcmd" "$SANDBOX/calls.log")" -eq 1 ] || fail "steamcmd was not called exactly once"
for run in "$SANDBOX"/runs/*; do
    assert_payloads_removed "$run"
    [ -f "$run/steam/upload-complete" ] || fail "upload did not observe both payloads"
done

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
FAILURE_CASES=("mac-build UNITY_MAC_EXIT=1" "codesign CODESIGN_EXIT=1" "universal LIPO_ARCHS=x86_64_arm64" \
    "intel LIPO_ARCHS=x86_64" "ffmpeg-architecture LIPO_FFMPEG_ARCHS=x86_64" \
    "event-script MAC_LEAKS_EVENT_SCRIPT=1" "mac-target BUILD_INFO_TARGET_MAC=StandaloneWindows64" \
    "mac-leaks-cef MAC_LEAKS_CEF=1")
for required_path in "${MAC_REQUIRED_RELATIVE_PATHS[@]}"; do
    FAILURE_CASES+=("missing-$required_path MAC_MISSING_PATH=$required_path")
done
for case_spec in "${FAILURE_CASES[@]}"; do
    name="${case_spec%% *}"
    assignment="${case_spec#* }"
    make_sandbox
    OUTPUT=$(eval "$assignment" run_target); STATUS=$?
    [ "$STATUS" -ne 0 ] || fail "$name did not fail the run"
    grep -q "^steamcmd" "$SANDBOX/calls.log" && fail "steamcmd ran despite $name"
    for run in "$SANDBOX"/runs/*; do
        assert_payloads_retained "$run"
        [ -f "$run/build-mac/build-started" ] || fail "$name lost the partial Mac build"
    done
    grep -q "^moores-wt .*rm" "$SANDBOX/calls.log" || fail "worktree was not torn down after $name"
done

# Macの欠損とtarget不一致は理由を示す
# Mac missing paths and target mismatches name their reason
make_sandbox
OUTPUT=$(BUILD_INFO_TARGET_MAC=StandaloneWindows64 run_target); STATUS=$?
[ "$STATUS" -eq 4 ] || fail "Mac target mismatch did not exit 4 (got $STATUS)"
case "$OUTPUT" in *'target mismatch'*) ;; *) fail "Mac target mismatch reason was absent";; esac
for missing_path in "${MAC_REQUIRED_RELATIVE_PATHS[@]}"; do
    make_sandbox
    OUTPUT=$(MAC_MISSING_PATH="$missing_path" run_target); STATUS=$?
    [ "$STATUS" -eq 4 ] || fail "missing Mac $missing_path did not exit 4 (got $STATUS)"
    case "$OUTPUT" in *"成果物に "*"/$missing_path "*) ;; *) fail "missing Mac $missing_path path was absent";; esac
done

# CEF のファイルが残った Mac 成果物は理由とパスを名指しする
# A Mac artifact that still carries a CEF file names the reason and the path
make_sandbox
OUTPUT=$(MAC_LEAKS_CEF=1 run_target); STATUS=$?
[ "$STATUS" -eq 4 ] || fail "leaked Mac CEF helper did not exit 4 (got $STATUS)"
case "$OUTPUT" in *'CEF のファイル'*'cef-unity-server'*) ;; *) fail "leaked Mac CEF helper reason or path was absent";; esac
# CEF の無い成功系では lipo が CEF helper を見に行かない
# On a CEF-free success run, lipo never inspects a CEF helper
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "CEF-free Mac run exited $STATUS: $OUTPUT"
grep -q '^lipo .*cef-unity-server' "$SANDBOX/calls.log" && fail "lipo still inspected a CEF helper"

# Mac depot ID が欠落・非数字ならビルド前に exit 2
# A missing or nonnumeric Mac depot ID exits 2 before building
for value in "" "12a"; do
    make_sandbox
    OUTPUT=$(MOORESTECH_STEAM_DEPOT_ID_MAC="$value" run_target); STATUS=$?
    [ "$STATUS" -eq 2 ] || fail "mac depot id '$value' did not exit 2 (got $STATUS)"
    [ ! -f "$SANDBOX/calls.log" ] || fail "mac depot id '$value' reached git/build"
    case "$OUTPUT" in *MOORESTECH_STEAM_DEPOT_ID_MAC*) ;; *) fail "mac depot id '$value' was not named";; esac
done

# Windows と Mac の depot ID が同じ値ならビルド前に exit 2
# Windows and Mac depot ids being equal exits 2 before building
make_sandbox
OUTPUT=$(MOORESTECH_STEAM_DEPOT_ID_MAC="1958161" run_target); STATUS=$?
[ "$STATUS" -eq 2 ] || fail "equal depot ids did not exit 2 (got $STATUS)"
[ ! -f "$SANDBOX/calls.log" ] || fail "equal depot ids reached git/build"

# 先頭ゼロが違っても数値として同じdepotなら拒否する
# Reject numerically equal depot ids despite different leading zeroes
make_sandbox
OUTPUT=$(MOORESTECH_STEAM_DEPOT_ID_WINDOWS=01958161 MOORESTECH_STEAM_DEPOT_ID_MAC=1958161 run_target); STATUS=$?
[ "$STATUS" -eq 2 ] || fail "numeric-equal depot ids did not exit 2 (got $STATUS)"
[ ! -f "$SANDBOX/calls.log" ] || fail "numeric-equal depot ids reached git/build"

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
