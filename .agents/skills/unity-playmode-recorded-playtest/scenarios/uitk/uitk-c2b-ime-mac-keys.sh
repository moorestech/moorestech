#!/bin/bash
# uitk-c2b-ime-mac.cs の運転役: run-scenario.sh でシナリオを起動し、シナリオが書いた step-<n>.keys の key code を osascript で Unity へ送る
# Driver for uitk-c2b-ime-mac.cs: boots the scenario via run-scenario.sh and sends the key codes in step-<n>.keys to Unity via osascript
# osascript を Unity の中から呼ぶと Unity にアクセシビリティの許可が要るので、許可のある端末側のこのスクリプトから送る
# Calling osascript from inside Unity would need Accessibility permission for Unity, so keys are sent from this terminal-side script
#
# usage: uitk-c2b-ime-mac-keys.sh <unity-project-path> <scenario.cs>
set -u
PROJECT="$1"
SCENARIO="$2"
SKILL_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
MARKERS="$PROJECT/PlaytestResults/scratch/ime-mac"
LOG="$PROJECT/PlaytestResults/scratch/ime-mac-runner.log"
rm -rf "$MARKERS"
mkdir -p "$MARKERS"

# 終わり方によらず入力ソースを英数へ戻す
# Always return the input source to eisu (ABC), however this ends
restore_input_source() { osascript -e 'tell application "System Events" to key code 102' >/dev/null 2>&1; }
trap restore_input_source EXIT

# シナリオは子プロセスで走らせ、このスクリプトが前景で鍵を送る（wait で終わりを待つ）
# Run the scenario as a child while this script sends keys in the foreground (wait collects it)
bash "$SKILL_DIR/scripts/run-scenario.sh" "$PROJECT" "$SCENARIO" >"$LOG" 2>&1 &
RUNNER=$!
step=1
deadline=$((SECONDS + 540))
while [ $SECONDS -lt $deadline ]; do
  [ -f "$MARKERS/finish" ] && break
  kill -0 "$RUNNER" 2>/dev/null || { echo "runner exited before finish"; break; }
  KEYS="$MARKERS/step-$step.keys"
  if [ -f "$KEYS" ]; then
    pid=$(head -1 "$KEYS")
    osascript -e "tell application \"System Events\" to set frontmost of (first process whose unix id is $pid) to true" >/dev/null
    outcome=sent
    for code in $(tail -n +2 "$KEYS"); do
      # 他のセッションが前面を取っていたら送らずに止める（別アプリへ打たない）
      # Stop without sending if another session took the front (never type into another app)
      front=$(osascript -e 'tell application "System Events" to unix id of first process whose frontmost is true')
      if [ "$front" != "$pid" ]; then outcome="aborted: frontmost pid $front is not unity $pid"; break; fi
      osascript -e "tell application \"System Events\" to key code $code"
      sleep 0.25
    done
    screencapture -x "$MARKERS/step-$step.png"
    echo "$outcome" >"$MARKERS/step-$step.done"
    echo "step $step: $outcome"
    step=$((step + 1))
  fi
  sleep 0.2
done
wait "$RUNNER"
status=$?
tail -40 "$LOG"
exit $status
