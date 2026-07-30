#!/bin/bash
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
source "$SCRIPT_DIR/../fixed-world-environment.sh"
FAILURES=0

expect_status() {
    local label="$1"
    local expected_status="$2"
    local expected_message="$3"
    local output actual_status

    output=$(validate_fixed_world_environment 2>&1)
    actual_status=$?
    if [[ "$actual_status" -ne "$expected_status" ]]; then
        echo "FAIL: $label status=$actual_status expected=$expected_status"
        FAILURES=$((FAILURES + 1))
        return
    fi
    if [[ -n "$expected_message" && "$output" != *"$expected_message"* ]]; then
        echo "FAIL: $label missing message: $expected_message"
        FAILURES=$((FAILURES + 1))
    fi
}

# 3変数が全て未設定なら従来起動として受理する
# Accept the legacy boot only when all three variables are unset
unset PLAYTEST_WORLD_DIRECTORY PLAYTEST_MAP_MODE PLAYTEST_SEED
expect_status "all unset" 1 ""

# 部分指定と空値は不足変数名を伴う入力エラーにする
# Reject partial input and empty values while naming the missing variable
PLAYTEST_WORLD_DIRECTORY="/tmp/world"
expect_status "world only" 2 "PLAYTEST_MAP_MODE"

PLAYTEST_MAP_MODE="generated"
PLAYTEST_SEED=""
expect_status "empty seed" 2 "PLAYTEST_SEED"

PLAYTEST_SEED="12345"
PLAYTEST_MAP_MODE=""
expect_status "empty map mode" 2 "PLAYTEST_MAP_MODE"

# 3変数が全て非空かつseedが整数なら固定world起動として受理する
# Accept fixed-world boot when all variables are non-empty and the seed is an integer
PLAYTEST_MAP_MODE="generated"
expect_status "complete fixed world" 0 ""

PLAYTEST_SEED="not-a-number"
expect_status "invalid seed" 2 "PLAYTEST_SEED"

# 実runnerも部分指定をpreflightへ到達させないことを固定する
# Pin that the real runner rejects partial input before reaching preflight
unset PLAYTEST_WORLD_DIRECTORY PLAYTEST_MAP_MODE PLAYTEST_SEED
RUNNER_PATH="$SCRIPT_DIR/../run-scenario.sh"
RUNNER_OUTPUT=$(PLAYTEST_WORLD_DIRECTORY="/tmp/world" bash "$RUNNER_PATH" /missing-project /missing-scenario /missing-master 2>&1)
RUNNER_STATUS=$?
if [[ "$RUNNER_STATUS" -ne 1 || "$RUNNER_OUTPUT" != *"PLAYTEST_MAP_MODE"* || "$RUNNER_OUTPUT" == *"== preflight =="* ]]; then
    echo "FAIL: real runner did not reject partial input before preflight"
    FAILURES=$((FAILURES + 1))
fi

if [[ "$FAILURES" -ne 0 ]]; then
    echo "FAILED: $FAILURES contract checks"
    exit 1
fi

echo "PASS: fixed-world environment contract"
