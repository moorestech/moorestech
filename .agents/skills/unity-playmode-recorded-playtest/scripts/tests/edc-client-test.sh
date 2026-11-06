#!/bin/bash
# edc-client.sh の自己テスト。偽 uloop の応答で success / compile_error / retry の扱いを確かめる
# Self-test for edc-client.sh; checks success / compile_error / retry handling against fake uloop responses
set -u

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
WORK_DIR="$(mktemp -d)"
export PATH="$SCRIPT_DIR/fake-uloop:$PATH"
export FAKE_ULOOP_CALL_LOG="$WORK_DIR/calls.log"
PROJECT_PATH="$WORK_DIR/project"
mkdir -p "$PROJECT_PATH/UserSettings"
source "$SCRIPT_DIR/../edc-client.sh"
EDC_RETRY_INTERVAL_SECONDS=0
FAILURES=0
SCENARIO_CODE='return PlaytestRunner.Run("t", options, async p => {});'

expect_edc() {
    local label="$1" response_file="$2" attempts="$3" expected_status="$4" expected_calls="$5"
    local actual_status actual_calls
    : > "$FAKE_ULOOP_CALL_LOG"
    export FAKE_ULOOP_SCENARIO_RESPONSE="$response_file"
    edc_call "$attempts" 0 "$SCENARIO_CODE" >/dev/null
    actual_status=$?
    actual_calls=$(grep -c 'PlaytestRunner.Run' "$FAKE_ULOOP_CALL_LOG")
    if [[ "$actual_status" -ne "$expected_status" || "$actual_calls" -ne "$expected_calls" ]]; then
        echo "FAIL: $label status=$actual_status calls=$actual_calls expected status=$expected_status calls=$expected_calls"
        FAILURES=$((FAILURES + 1))
    fi
}

echo '{"Success": true, "Result": "/tmp/run"}' > "$WORK_DIR/success.json"
echo '{"Success": false, "ErrorMessage": "Unity is reloading (Domain Reload in progress)"}' > "$WORK_DIR/reloading.json"
echo '{"Success": false, "ErrorMessage": "error CS0246: The type or namespace name could not be found"}' > "$WORK_DIR/message-only.json"

# 成功は 1 回で返り、コンパイル失敗は再試行せず 2 で返る（再試行しても直らないため）
# Success returns after one call; a compile error returns 2 without retrying (retrying cannot fix it)
expect_edc "success" "$WORK_DIR/success.json" 8 0 1
expect_edc "compile error" "$SCRIPT_DIR/fixtures/edc-compile-error-response.json" 8 2 1

# リロード中などの一時的な失敗と、CompilationErrors の無い失敗（メッセージに CS 番号があっても）は回数を使い切って 1 で返る
# Transient failures such as a reload, and failures without CompilationErrors (even with a CS code in the message), exhaust the attempts and return 1
expect_edc "transient failure" "$WORK_DIR/reloading.json" 3 1 3
expect_edc "CS code in message only" "$WORK_DIR/message-only.json" 3 1 3

# コンパイル失敗の一覧に CS コードが出る
# The compile error listing shows the CS code
LISTING=$(print_compile_errors < "$SCRIPT_DIR/fixtures/edc-compile-error-response.json")
if [[ ! "$LISTING" =~ CS[0-9]{4} ]]; then
    echo "FAIL: print_compile_errors did not list a CS code: $LISTING"
    FAILURES=$((FAILURES + 1))
fi

rm -rf "$WORK_DIR"
if [[ "$FAILURES" -ne 0 ]]; then
    echo "FAILED: $FAILURES edc-client checks"
    exit 1
fi
echo "PASS: edc-client contract"
