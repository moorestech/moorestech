#!/usr/bin/env bash
# fail()で集計してFAILURES件数を末尾判定する方式のため、set -eは使わない(1件の失敗で打ち切らない)
# Aggregated via fail() and judged by FAILURES at the end, so set -e is not used (one failure must not abort the rest)
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/release-playtest-sandbox.sh
. "$SCRIPT_DIR/lib/release-playtest-sandbox.sh"

# 成功系: worktree→unity→steamcmd→verify→worktree破棄(trap) の順に呼ばれ、告知テキストが出る
make_sandbox
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -eq 0 ] || fail "success run exited $STATUS: $OUTPUT"
ORDER=$(awk '{print $1}' "$SANDBOX/calls.log" | tr '\n' ' ')
[ "$ORDER" = "git git git moores-wt git git git git git git git git git unity unity codesign lipo lipo steamcmd verify moores-wt " ] || fail "call order was: $ORDER"
grep -q "run_app_build" "$SANDBOX/calls.log" || fail "steamcmd was not asked to run_app_build"
ls "$SANDBOX"/runs/*/promotion.md >/dev/null 2>&1 || fail "promotion.md was not written"
grep -q '"setlive" "playtest-staging"' "$SANDBOX"/runs/*/steam/app_build_playtest.vdf || fail "Steam upload did not target playtest-staging"
grep -q '手動でライブ設定' "$SANDBOX"/runs/*/promotion.md || fail "promotion instructions omitted manual playtest update"
grep -q "__[A-Z_]*__" "$SANDBOX"/runs/*/steam/*.vdf && fail "vdf still contains a raw token"
grep -q "1958161" "$SANDBOX"/runs/*/steam/depot_build_windows.vdf || fail "depot id was not substituted"
grep -q "1958162" "$SANDBOX"/runs/*/steam/depot_build_mac.vdf || fail "mac depot id was not substituted"
grep -q '"1958161"' "$SANDBOX"/runs/*/steam/app_build_playtest.vdf || fail "app build did not list the windows depot"
grep -q '"1958162"' "$SANDBOX"/runs/*/steam/app_build_playtest.vdf || fail "app build did not list the mac depot"
grep -q "/build-windows" "$SANDBOX"/runs/*/steam/depot_build_windows.vdf || fail "windows depot contentroot is not build-windows"
grep -q "/build-mac" "$SANDBOX"/runs/*/steam/depot_build_mac.vdf || fail "mac depot contentroot is not build-mac"
grep -q 'Mac 版の手動確認' "$SANDBOX"/runs/*/promotion.md || fail "promotion.md lacks the Mac manual check"
grep -q '回避操作' "$SANDBOX"/runs/*/promotion.md || fail "promotion.md lacks the workaround record"
grep -q '案内' "$SANDBOX"/runs/*/promotion.md || fail "promotion.md does not say workarounds are guided, not blocking"
grep -q "$SANDBOX/runs" "$SANDBOX"/runs/*/steam/*.vdf || fail "contentroot was not pointed at the run's build dir"
grep -q "^moores-wt .*rm" "$SANDBOX/calls.log" || fail "the disposable worktree was not torn down"

# 必須envの欠落はビルド前に落ちる
make_sandbox
OUTPUT=$(MOORESTECH_STEAM_DEPOT_ID_WINDOWS="" run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "missing depot id did not fail"
[ ! -f "$SANDBOX/calls.log" ] || fail "missing env reached the build"
case "$OUTPUT" in *MOORESTECH_STEAM_DEPOT_ID_WINDOWS*) ;; *) fail "missing env did not name the variable";; esac

# 検証機・受け口の env が欠けていても、ビルドやアップロードの後ではなく最初に落ちる（F12）
# Missing check-machine/receiver env fails up front rather than after the build or the upload
for name in MOORESTECH_VERIFY_HOST PLAYTEST_ADMIN_KEY; do
    make_sandbox
    OUTPUT=$(eval "$name=''" run_target); STATUS=$?
    [ "$STATUS" -eq 2 ] || fail "missing $name did not exit 2 (got $STATUS)"
    [ ! -f "$SANDBOX/calls.log" ] || fail "missing $name reached git/build"
    case "$OUTPUT" in *"$name"*) ;; *) fail "missing $name was not named: $OUTPUT";; esac
done

# ラベルは許可リスト外（区切り・引用符・空白を含む等）なら何も呼ばずに落ち、depot id は数字以外を拒む（F14）
# A label outside the allowlist (separators, quotes, spaces) fails before any call, and a non-numeric depot id is refused
for label in "bad'label" "a/b" "-lead" "has space"; do
    make_sandbox
    OUTPUT=$(MOORESTECH_STEAM_BUILD_LABEL="$label" run_target); STATUS=$?
    [ "$STATUS" -eq 2 ] || fail "label '$label' was not refused (got $STATUS)"
    [ ! -f "$SANDBOX/calls.log" ] || fail "label '$label' reached git/build"
done
make_sandbox
OUTPUT=$(MOORESTECH_STEAM_DEPOT_ID_WINDOWS="1958161|x" run_target); STATUS=$?
[ "$STATUS" -eq 2 ] || fail "a non-numeric depot id was not refused (got $STATUS)"
[ ! -f "$SANDBOX/calls.log" ] || fail "a non-numeric depot id reached git/build"

# ビルド失敗ならsteamcmdへ進まない
make_sandbox
OUTPUT=$(UNITY_EXIT=1 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "build failure did not fail the run"
grep -q "^steamcmd" "$SANDBOX/calls.log" && fail "steamcmd ran after a failed build"

# Windows成果物の不一致はMacビルドを始める前に止める
# Stop on a bad Windows artifact before starting the Mac build
make_sandbox
OUTPUT=$(BUILD_INFO_TARGET_WINDOWS=StandaloneOSX run_target); STATUS=$?
[ "$STATUS" -eq 4 ] || fail "Windows target mismatch did not exit 4 (got $STATUS)"
[ "$(grep -c '^unity' "$SANDBOX/calls.log")" -eq 1 ] || fail "Mac build ran despite the Windows artifact mismatch"
grep -q '^steamcmd' "$SANDBOX/calls.log" && fail "steamcmd ran despite the Windows artifact mismatch"

# 検証機の通し検証が落ちたら手動反映手順を書かない
make_sandbox
OUTPUT=$(VERIFY_EXIT=1 run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "verification failure did not fail the run"
ls "$SANDBOX"/runs/*/promotion.md >/dev/null 2>&1 && fail "promotion.md was written for a failed verification"

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
case "\$*" in
  *MacOsSteamPlaytestBuild*)
    app="\$MOORESTECH_BUILD_OUTPUT/moorestech.app"
    mkdir -p "\$app/Contents/MacOS" "\$app/Contents/Resources/Data/StreamingAssets" "\$MOORESTECH_BUILD_OUTPUT/game/mods"
    touch "\$app/Contents/MacOS/moorestech" "\$app/Contents/MacOS/ffmpeg" "\$app/Contents/Resources/ffmpeg-LICENSE.txt"
    printf '{"commit":"%s","branch":"%s","steamBuildLabel":"%s","target":"StandaloneOSX"}' \
      "$COMMIT" "\$MOORESTECH_BUILD_BRANCH" "\$MOORESTECH_STEAM_BUILD_LABEL" \
      >"\$app/Contents/Resources/Data/StreamingAssets/build-info.json"
    ;;
  *)
    mkdir -p "\$MOORESTECH_BUILD_OUTPUT/moorestech_Data/StreamingAssets" "\$MOORESTECH_BUILD_OUTPUT/game/mods"
    touch "\$MOORESTECH_BUILD_OUTPUT/moorestech.exe"
    printf '{"commit":"%s","branch":"%s","someOtherField":"%s","target":"StandaloneWindows64"}' \
      "$COMMIT" "\$MOORESTECH_BUILD_BRANCH" "\$MOORESTECH_STEAM_BUILD_LABEL" \
      >"\$MOORESTECH_BUILD_OUTPUT/moorestech_Data/StreamingAssets/build-info.json"
    ;;
esac
EOF
chmod +x "$SANDBOX/bin/unity"
OUTPUT=$(run_target); STATUS=$?
[ "$STATUS" -ne 0 ] || fail "build-info.json missing steamBuildLabel key did not fail"
case "$OUTPUT" in *"steamBuildLabel mismatch"*) ;; *) fail "missing steamBuildLabel was not the failure reason: $OUTPUT";; esac
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

finish_contract "release-playtest contract"
