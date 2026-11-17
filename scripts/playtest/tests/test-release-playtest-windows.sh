#!/usr/bin/env bash
# release-playtest-windows.sh（配布ビルド機で焼く経路）の契約テスト。fail()で集計するため set -e は使わない
# Contract tests for release-playtest-windows.sh (the build-machine route); aggregated via fail(), so set -e is not used
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/release-playtest-sandbox.sh
. "$SCRIPT_DIR/lib/release-playtest-sandbox.sh"
# shellcheck source=lib/release-playtest-winbake-stubs.sh
. "$SCRIPT_DIR/lib/release-playtest-winbake-stubs.sh"
TARGET="$(cd "$SCRIPT_DIR/.." && pwd)/release-playtest-windows.sh"

make_winbake_sandbox() { make_sandbox; write_winbake_stubs "$SANDBOX"; }
run_winbake() { MOORESTECH_WINBUILD_HOST="${MOORESTECH_WINBUILD_HOST-winbuild}" WINBAKE_POLL_SECONDS=0 run_target; }

# 成功系: 焼きは Windows で起動し、両成果物を持ち帰って検査し、Mac の Unity も moores-wt も呼ばずに Steam・検証へ進む
# Success: the bake starts on Windows, both artifacts come back and pass inspection, and Steam and verification follow without Mac Unity or moores-wt
make_winbake_sandbox
OUTPUT=$(run_winbake); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "success run exited $STATUS: $OUTPUT"
ORDER=$(awk '{print $1 ($2 == "winbuild" ? ":" $3 : "")}' "$SANDBOX/calls.log" | tr '\n' ' ')
case "$ORDER" in
  *"ssh:launch"*"ssh:poll"*"ssh:tar"*"codesign lipo lipo steamcmd rm verify "*) ;;
  *) fail "call order was: $ORDER" ;;
esac
grep -q '^moores-wt' "$SANDBOX/calls.log" && fail "the Windows route created a Mac worktree"
grep -q '^unity .*-projectPath' "$SANDBOX/calls.log" && fail "the Windows route ran Unity on the Mac"
grep -q "run_app_build" "$SANDBOX/calls.log" || fail "steamcmd was not asked to run_app_build"
ls "$SANDBOX"/runs/*/promotion.md >/dev/null 2>&1 || fail "promotion.md was not written"
ls "$SANDBOX"/runs/*/windows-bake-result.json >/dev/null 2>&1 || fail "the bake result was not kept in RUN_DIR"

# 持ち帰った Mac 成果物は実行権が付け直されて検査を通る（Windows は実行権を持たない）
# The fetched Mac artifact gets its executable bits back and passes inspection (Windows keeps none)
make_winbake_sandbox
WINBAKE_KEEP=1 STEAMCMD_EXIT=1 run_winbake >/dev/null 2>&1
for exe in "$SANDBOX"/runs/*/build-mac/moorestech.app/Contents/MacOS/moorestech "$SANDBOX"/runs/*/build-mac/moorestech.app/Contents/MacOS/ffmpeg; do
    [ -x "$exe" ] || fail "executable bit was not restored: $exe"
done

# 宛先が無ければ何も呼ばずに落ちる
# A missing build-machine target fails before any call
make_winbake_sandbox
OUTPUT=$(MOORESTECH_WINBUILD_HOST="" run_winbake); STATUS=$?
[ "$STATUS" -eq 2 ] || fail "missing MOORESTECH_WINBUILD_HOST did not exit 2 (got $STATUS)"
[ ! -f "$SANDBOX/calls.log" ] || fail "missing MOORESTECH_WINBUILD_HOST reached git/ssh"
case "$OUTPUT" in *MOORESTECH_WINBUILD_HOST*) ;; *) fail "missing host was not named";; esac

# Windows 側の焼きが失敗したら理由を出して止まり、成果物を持ち帰らず Steam へ進まない
# A failed Windows bake stops with its reason, fetches no artifact and never reaches Steam
make_winbake_sandbox
OUTPUT=$(WINBAKE_FAIL=1 run_winbake); STATUS=$?
[ "$STATUS" -eq 3 ] || fail "a failed Windows bake did not exit 3 (got $STATUS)"
case "$OUTPUT" in *"Unity failed"*) ;; *) fail "the bake failure reason was not shown: $OUTPUT";; esac
grep -q 'ssh winbuild tar' "$SANDBOX/calls.log" && fail "artifacts were fetched after a failed bake"
grep -q '^steamcmd' "$SANDBOX/calls.log" && fail "steamcmd ran after a failed bake"

# 持ち帰った成果物の取り違え（ターゲット違い）は Mac の検査で止まる
# A mixed-up fetched artifact (wrong target) is stopped by the Mac-side inspection
make_winbake_sandbox
OUTPUT=$(BUILD_INFO_TARGET_MAC=StandaloneWindows64 run_winbake); STATUS=$?
[ "$STATUS" -eq 4 ] || fail "Mac target mismatch did not exit 4 (got $STATUS)"
grep -q '^steamcmd' "$SANDBOX/calls.log" && fail "steamcmd ran despite the Mac target mismatch"

finish_contract "release-playtest windows contract"
