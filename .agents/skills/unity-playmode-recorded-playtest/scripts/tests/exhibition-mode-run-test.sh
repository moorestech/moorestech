#!/bin/bash
# exhibition-mode-run.sh の自己テスト（Unity を使わない）。偽の uloop を PATH の先頭に置き、引数の誤り・武装の外れ・復元の外れの経路を見る
# Self-test for exhibition-mode-run.sh without Unity; a fake uloop first on PATH exercises bad arguments, failed arming and failed restore
set -u
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DRIVER="$SCRIPT_DIR/../../scenarios/uitk/stage5/exhibition-mode-run.sh"
WORK="$(mktemp -d)"
FAILURES=0
mkdir -p "$WORK/bin" "$WORK/project"
export FAKE_ULOOP_CALL_LOG="$WORK/calls.log"

# 偽 uloop: --code-file はファイル名で、--code は中身で応答を選び、呼ばれた順に 1 行ずつ記録する
# Fake uloop: picks a response by the --code-file name or the --code text and logs one line per call
cat > "$WORK/bin/uloop" <<'FAKE'
#!/bin/bash
shift
CODE=""; FILE=""
while [ $# -gt 0 ]; do
  case "$1" in --code) CODE="$2"; shift ;; --code-file) FILE="$(basename "$2")"; shift ;; esac
  shift
done
echo "${FILE:-code: ${CODE:0:60}}" >> "$FAKE_ULOOP_CALL_LOG"
case "$FILE" in
  exhibition-arm.edc) printf '{"Success": true, "Result": "%s"}\n' "$FAKE_ARM_RESULT" ;;
  exhibition-disarm.edc) printf '{"Success": true, "Result": "%s"}\n' "$FAKE_DISARM_RESULT" ;;
  "") echo '{"Success": true, "Result": "switched"}' ;;
  *) echo '{"Success": false, "ErrorMessage": "fake uloop: unexpected code file"}'; exit 1 ;;
esac
FAKE
chmod +x "$WORK/bin/uloop"
export PATH="$WORK/bin:$PATH"

# 1 ケースを流し、終了コード・出力の語・記録の行数か最後の行を比べる
# Run one case and compare the exit code, an output word, and the call count or last call
expect() {
  local label="$1" expected_code="$2" expected_word="$3" expected_calls="$4" expected_last="$5"; shift 5
  : > "$FAKE_ULOOP_CALL_LOG"
  local output code calls last
  output=$(bash "$DRIVER" "$@" 2>&1); code=$?
  calls=$(wc -l < "$FAKE_ULOOP_CALL_LOG" | tr -d ' '); last=$(tail -1 "$FAKE_ULOOP_CALL_LOG")
  if [ "$code" -ne "$expected_code" ] || ! echo "$output" | grep -q -- "$expected_word" \
    || { [ -n "$expected_calls" ] && [ "$calls" != "$expected_calls" ]; } || { [ -n "$expected_last" ] && [ "$last" != "$expected_last" ]; }; then
    echo "FAIL: $label code=$code calls=$calls last=$last output=$output"
    FAILURES=$((FAILURES + 1))
  else
    echo "PASS: $label"
  fi
}

# 1・2. 引数の数・系の誤りは uloop を 1 回も呼ばずに 2
# 1-2. A wrong argument count or system exits 2 without calling uloop
export FAKE_ARM_RESULT="exhibition-armed" FAKE_DISARM_RESULT="disarmed / language-untouched"
expect "two arguments" 2 "usage:" 0 "" "$WORK/project" GameUi
expect "system Classic" 2 "NG: UI system" 0 "" "$WORK/project" Classic "$WORK/x.cs"

# 3. 武装の語が外れたら流さず 1。終わり方によらず最後に Disarm が流れる
# 3. A missed arming word exits 1 without running; Disarm still runs last however it ends
export FAKE_ARM_RESULT="ERROR: already playing"
expect "arm refused while playing" 1 "NG: exhibition mode was not armed" "" "exhibition-disarm.edc" "$WORK/project" GameUi "$WORK/x.cs"

# 4. 復元の語が外れたら ERROR と kept を出し、控えを残す
# 4. A missed restore word prints ERROR and kept, and keeps the note
NOTE="$WORK/project/PlaytestResults/scratch/event-language-gate/language-before.txt"
mkdir -p "$(dirname "$NOTE")"; echo japanese > "$NOTE"
export FAKE_DISARM_RESULT="disarmed / language-restore-failed (japanese; note kept: x)"
expect "restore failed keeps the note" 1 "ERROR: .*kept" "" "exhibition-disarm.edc" "$WORK/project" GameUi "$WORK/x.cs"
if [ ! -f "$NOTE" ]; then echo "FAIL: language-before.txt was removed"; FAILURES=$((FAILURES + 1)); fi

rm -rf "$WORK"
if [ "$FAILURES" -ne 0 ]; then echo "exhibition-mode-run-test: $FAILURES failure(s)"; exit 1; fi
echo "exhibition-mode-run-test: all passed"
