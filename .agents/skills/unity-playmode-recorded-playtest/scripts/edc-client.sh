#!/bin/bash
# uloop execute-dynamic-code の呼び出しと応答の分類（run-scenario.sh と preflight.sh が source する）
# Calls uloop execute-dynamic-code and classifies the response (sourced by run-scenario.sh and preflight.sh)
#
# 呼び出し側は source の前に PROJECT_PATH を決めておく。コンパイルできないコード片は 1 行も実行されないので、
# 再試行せず戻り値 2 で知らせる（検証 0 件の実行を緑に見せないため。bd moorestech-dww3）。
# Callers set PROJECT_PATH before sourcing. A snippet that does not compile runs zero lines,
# so it is reported with return code 2 without retrying (so a zero-check run never looks green; bd moorestech-dww3).

EDC_RETRY_INTERVAL_SECONDS="${EDC_RETRY_INTERVAL_SECONDS:-5}"
# edc_call の第 2 引数（秒）。0 は「タイムアウト無し」で、uloop 内蔵のタイムアウトに任せる（boot とシナリオ投入は長く、外から切らない）
# Second edc_call argument (seconds). 0 means no timeout: uloop's own internal timeouts apply (boot and scenario injection run long and must not be cut from outside)
EDC_NO_TIMEOUT_SECONDS=0

# uloop出力から先頭の警告行を除いてJSON部分だけを取り出す
# Strip leading warning lines from uloop output and keep only the JSON part
extract_json() { sed -n '/^{/,$p'; }
json_get() { python3 -c "import sys,json; print(json.load(sys.stdin).get('$1',''))" 2>/dev/null; }

# domain reloadやcompile中断が残したCLI Loop設定の退避を、次のEDCより前に復元する
# Restore CLI Loop settings left backed up by a domain reload or compile interruption before the next EDC
restore_cli_loop_settings() {
    local settings="$PROJECT_PATH/UserSettings/UnityMcpSettings.json"
    local backup="${settings}.bak"
    [[ -f "$settings" || ! -f "$backup" ]] || mv "$backup" "$settings"
}

# 秒数が 0 か GNU timeout が無い環境（素の macOS）では直接実行する。uloop は内部にもタイムアウトを持つ
# Run directly when seconds is 0 or GNU timeout is missing (stock macOS); uloop has its own internal timeouts
run_with_timeout() {
    local seconds="$1"; shift
    if [[ "$seconds" -gt 0 ]] && command -v timeout >/dev/null 2>&1; then
        timeout "$seconds" "$@"
    else
        "$@"
    fi
}

# 応答から「コンパイル失敗の一覧」と「メッセージ」を取り出す唯一の定義。分類と表示の両方がこれを使う
# The only definition that pulls the compile error list and the message out of a response; classify and print both use it
# コンパイル失敗の判定は moores-code-review の scenario_compile.py（run_edc）と同じく CompilationErrors が空でないことだけ
# A compile error is judged only by a non-empty CompilationErrors, the same rule as run_edc in moores-code-review's scenario_compile.py
EDC_RESPONSE_FIELDS_PY='
def compile_errors(response):
    return response.get("CompilationErrors") or []
def error_message(response):
    return str(response.get("ErrorMessage") or response.get("Error") or "")
'

# 応答を success / compile_error / retry に分ける。JSON でない応答（リロード中の空出力など）と診断なしの失敗（実行時例外など）は retry
# Classify a response as success / compile_error / retry; non-JSON output (reload) and failures without diagnostics (runtime errors) are retry
edc_response_kind() {
    python3 -c "$EDC_RESPONSE_FIELDS_PY"'
import json, sys
try:
    response = json.loads(sys.stdin.read())
except ValueError:
    print("retry")  # silent-ok: リロード中の空出力は再試行 / 使い切ると edc_call が 1 を返し呼び出し側が NG を出す
    sys.exit(0)
if response.get("Success") is True:
    print("success")
elif compile_errors(response):
    print("compile_error")
else:
    print("retry")
'
}

# 標準入力の応答 JSON からコンパイル失敗の一覧を出す
# Print the compile error list from the response JSON on stdin
print_compile_errors() {
    python3 -c "$EDC_RESPONSE_FIELDS_PY"'
import json, sys
response = json.loads(sys.stdin.read())
errors = compile_errors(response)
for error in errors:
    print("  {} (line {}): {}".format(error.get("ErrorCode", "?"), error.get("Line", "?"), error.get("Message", "")))
if not errors:
    print("  " + error_message(response))
'
}

# 成功で 0、コンパイル失敗で 2（再試行しない）、再試行を使い切ったら 1。最後の応答 JSON を標準出力へ出す
# Return 0 on success, 2 on a compile error (no retry), 1 after exhausting attempts; print the last response JSON
edc_call() {
    local attempts="$1" timeout_seconds="$2" code="$3"
    local attempt response kind
    for attempt in $(seq 1 "$attempts"); do
        restore_cli_loop_settings
        response=$(run_with_timeout "$timeout_seconds" uloop execute-dynamic-code --project-path "$PROJECT_PATH" --code "$code" 2>/dev/null | extract_json)
        kind=$(printf '%s' "$response" | edc_response_kind)
        if [[ "$kind" == "success" ]]; then
            echo "$response"
            return 0
        fi
        if [[ "$kind" == "compile_error" ]]; then
            echo "$response"
            return 2
        fi
        if [[ "$attempt" -lt "$attempts" ]]; then sleep "$EDC_RETRY_INTERVAL_SECONDS"; fi
    done
    echo "$response"
    return 1
}
