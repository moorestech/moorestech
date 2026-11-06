#!/bin/bash
# run-scenario.sh の自己テスト。シナリオがコンパイルできないと投入 1 回で exit 2、通るシナリオは exit 0
# Self-test for run-scenario.sh: an uncompilable scenario injects once and exits 2, a passing one exits 0
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
RUNNER="$SCRIPT_DIR/../run-scenario.sh"
WORK_DIR="$(mktemp -d)"
FAILURES=0
export PATH="$SCRIPT_DIR/fake-uloop:$PATH"
export FAKE_ULOOP_CALL_LOG="$WORK_DIR/calls.log"
export FAKE_SESSION_DIR="$WORK_DIR/session"
unset PLAYTEST_WORLD_DIRECTORY PLAYTEST_MAP_MODE PLAYTEST_SEED

# 偽のプロジェクト・master・セッションを用意する（ready.marker は先に置く）
# Prepare a fake project, master and session (ready.marker is placed up front)
mkdir -p "$WORK_DIR/project/UserSettings" "$WORK_DIR/master/mods/vanilla" "$WORK_DIR/master/map" "$FAKE_SESSION_DIR" "$WORK_DIR/run"
echo '{}' > "$WORK_DIR/master/mods/vanilla/mod.json"
echo '{}' > "$WORK_DIR/master/map/map.json"
touch "$FAKE_SESSION_DIR/ready.marker"
echo 'return PlaytestRunner.Run("t", options, async p => {});' > "$WORK_DIR/scenario.cs"

run_runner() {
    : > "$FAKE_ULOOP_CALL_LOG"
    export FAKE_ULOOP_SCENARIO_RESPONSE="$1"
    RUNNER_OUTPUT=$(bash "$RUNNER" "$WORK_DIR/project" "$WORK_DIR/scenario.cs" "$WORK_DIR/master" 2>&1)
    RUNNER_STATUS=$?
    SCENARIO_CALLS=$(grep -c 'PlaytestRunner.Run' "$FAKE_ULOOP_CALL_LOG")
}

# コンパイル失敗: 投入は 1 回、exit 2、NG の行と CS コードを出す
# Compile error: one injection, exit 2, an NG line and the CS code
run_runner "$SCRIPT_DIR/fixtures/edc-compile-error-response.json"
if [[ "$RUNNER_STATUS" -ne 2 || "$SCENARIO_CALLS" -ne 1 || "$RUNNER_OUTPUT" != *"NG: scenario compile failed"* || ! "$RUNNER_OUTPUT" =~ CS[0-9]{4} ]]; then
    echo "FAIL: compile error status=$RUNNER_STATUS calls=$SCENARIO_CALLS"
    echo "$RUNNER_OUTPUT"
    FAILURES=$((FAILURES + 1))
fi

# 対照: 通るシナリオは result.json を読んで exit 0（偽 uloop の配線が投入まで届いている証拠）
# Control: a passing scenario reads result.json and exits 0 (proof the fake wiring reaches injection)
printf '{"Success": true, "Result": "%s"}\n' "$WORK_DIR/run" > "$WORK_DIR/success.json"
echo '{"Success": true}' > "$WORK_DIR/run/result.json"
run_runner "$WORK_DIR/success.json"
if [[ "$RUNNER_STATUS" -ne 0 || "$SCENARIO_CALLS" -ne 1 ]]; then
    echo "FAIL: passing scenario status=$RUNNER_STATUS calls=$SCENARIO_CALLS"
    echo "$RUNNER_OUTPUT"
    FAILURES=$((FAILURES + 1))
fi

rm -rf "$WORK_DIR"
if [[ "$FAILURES" -ne 0 ]]; then
    echo "FAILED: $FAILURES run-scenario checks"
    exit 1
fi
echo "PASS: run-scenario compile error contract"
