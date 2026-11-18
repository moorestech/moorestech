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
SKILL_DIR="$(cd "$(dirname "$0")/../../.." && pwd)"
MARKERS="$PROJECT/PlaytestResults/scratch/ime-mac"
LOG="$PROJECT/PlaytestResults/scratch/ime-mac-runner.log"
rm -rf "$MARKERS"
mkdir -p "$MARKERS"

# 終わり方によらず入力ソースを英数へ戻す
# Always return the input source to eisu (ABC), however this ends
restore_input_source() { osascript -e 'tell application "System Events" to key code 102' >/dev/null 2>&1; }
# シナリオが途中で落ちて Editor が拡大のまま残ったら、layout-before.txt の値で主窓・Game ビュー・倍率を戻す（正常終了ではシナリオが消すので何もしない）
# If the scenario died and left the Editor enlarged, restore the main window, Game view and zoom from layout-before.txt (the scenario deletes it on a normal finish, so nothing happens then)
restore_layout() {
  [ -f "$MARKERS/layout-before.txt" ] || return 0
  echo "WARN: scenario left the Editor enlarged; restoring layout"
  uloop execute-dynamic-code --project-path "$PROJECT" --code 'using System.Linq;
var v = System.IO.File.ReadAllText(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", "PlaytestResults", "scratch", "ime-mac", "layout-before.txt")).Trim().Split(" ");
var n = System.Array.ConvertAll(v, s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var gv = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>().First(w => w.GetType().Name == "GameView");
gv.maximized = false;
UnityEditor.EditorGUIUtility.SetMainWindowPosition(new UnityEngine.Rect(n[0], n[1], n[2], n[3]));
gv.GetType().GetMethod("SnapZoom", flags).Invoke(gv, new object[] { n[4] });
return "restored";' && rm -f "$MARKERS/layout-before.txt" || echo "ERROR: layout restore failed; layout-before.txt kept ($MARKERS)"
}
on_exit() { restore_layout; restore_input_source; }
trap on_exit EXIT

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
# 締め切りを超えてもランナーが残っていたら止めてから回収する
# If the runner is still alive past the deadline, stop it before collecting
kill -0 "$RUNNER" 2>/dev/null && [ ! -f "$MARKERS/finish" ] && kill "$RUNNER" 2>/dev/null
wait "$RUNNER"
status=$?
tail -40 "$LOG"
exit $status
