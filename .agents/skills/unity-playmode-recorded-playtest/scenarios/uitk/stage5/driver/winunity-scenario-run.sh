#!/bin/bash
# Mac から Windows の GUI Editor でシナリオを 1 本流す運転役: 印の EDC → （出展モードなら武装の EDC）→ winunity play → （復元の EDC）→ 数える EDC
# 出展モードの武装は GUI Editor の SessionState にあるので、この 1 回の中で武装・play・復元を続けて流す（間に同じ worktree の compile/test を挟むと Editor ごと消える）
# Driver that runs one scenario on the Windows GUI Editor from the Mac: mark EDC -> (arm EDC in exhibition mode) -> winunity play -> (restore EDC) -> count EDC
# Exhibition arming lives in the GUI Editor's SessionState, so arm, play and restore run back to back here (a compile/test on the same worktree in between closes the Editor and drops it)
#
# usage: winunity-scenario-run.sh <branch> <GameUi|Web> <normal|exhibition> <scenario.cs> <winunity-name>
# branch は push 済みであること（winunity が GitHub から取る）。WINUNITY で winunity の実体を差し替えられる
# The branch must be pushed (winunity fetches it from GitHub); WINUNITY overrides the winunity executable
set -u
if [ "$#" -ne 5 ]; then echo "usage: winunity-scenario-run.sh <branch> <GameUi|Web> <normal|exhibition> <scenario.cs> <winunity-name>"; exit 2; fi
BRANCH="$1"; SYSTEM="$2"; MODE="$3"; SCENARIO="$4"; NAME="$5"
case "$SYSTEM" in GameUi|Web) ;; *) echo "NG: UI system must be GameUi or Web (got $SYSTEM)"; exit 2 ;; esac
case "$MODE" in normal|exhibition) ;; *) echo "NG: mode must be normal or exhibition (got $MODE)"; exit 2 ;; esac
[ -f "$SCENARIO" ] || { echo "NG: scenario not found: $SCENARIO"; exit 2; }
HERE="$(cd "$(dirname "$0")" && pwd)"
WINUNITY="${WINUNITY:-$HOME/bin/winunity}"
WORK="$(mktemp -d)"
TOKEN="s5ladder-$(date +%Y%m%d-%H%M%S)-$$"
RESULT=0
RUN_DIR=""

# コード片を GUI Editor で 1 回流し、出力を見せて残す（語の判定は呼び手が grep で行う。終了コードは EDC が走ったかしか表さない）
# Run one snippet in the GUI Editor, show and keep the output (callers grep for the word; the exit code only says EDC ran)
edc_words() {
  "$WINUNITY" edc "$BRANCH" "$1" --name "$NAME" > "$WORK/$2.txt" 2>&1
  cat "$WORK/$2.txt"
}

# EDC の出力から uloop の Result の値だけを取る（無ければ出力の最後の行）
# Take only uloop's Result value from an EDC output (the output's last line when there is none)
edc_result() {
  local value
  value=$(grep -o '"Result"[[:space:]]*:[[:space:]]*"[^"]*"' "$WORK/$1.txt" | tail -1 | sed 's/^"Result"[[:space:]]*:[[:space:]]*"//; s/"$//')
  if [ -n "$value" ]; then echo "$value"; else tail -1 "$WORK/$1.txt"; fi
}

# 終わり方によらず武装を解き、シナリオが言語を戻せずに落ちていたら選ぶ前の言語へ戻す（出展モードのときだけ張る）
# However this ends, disarm and restore the pre-choice language if the scenario died before doing so (set only in exhibition mode)
on_exit() {
  local code=$?
  echo "[driver] disarm and restore the language"
  edc_words "$HERE/exhibition-disarm.edc" disarm >/dev/null
  echo "[driver] disarm: $(edc_result disarm)"
  if grep -q 'language-untouched\|language-restored' "$WORK/disarm.txt"; then exit "$code"; fi
  echo "ERROR: disarm or language restore failed; language-before.txt kept on Windows: $(edc_result disarm)"
  exit 1
}

# 1. 印: 流す前の GUI Editor のログに印を書き、後で印より後の行だけを数える
# 1. Mark: write a mark into the GUI Editor log before the run, and later count only the lines after it
cat > "$WORK/mark.edc" <<EOF
// 印より後のログだけを数えるための印を書く
// Write the mark that bounds the counted log lines
UnityEngine.Debug.Log("[S5LadderLogMark] $TOKEN");
return "log-marked " + UnityEngine.Application.consoleLogPath;
EOF
edc_words "$WORK/mark.edc" mark >/dev/null
grep -q 'log-marked' "$WORK/mark.txt" || { cat "$WORK/mark.txt"; echo "NG: could not write the log mark"; exit 1; }
echo "[driver] $(edc_result mark)"

# 2. 出展モードなら武装する。PlayMode が残っていたら止めてから武装し直す（武装しないまま通常の起動で流さない）
# 2. In exhibition mode, arm; if a PlayMode is left, stop it and re-arm (never run a normal boot unarmed)
if [ "$MODE" = exhibition ]; then
  trap on_exit EXIT
  edc_words "$HERE/exhibition-arm.edc" arm >/dev/null
  if ! grep -q 'exhibition-armed' "$WORK/arm.txt" && grep -q 'already playing' "$WORK/arm.txt"; then
    echo "[driver] PlayMode is still running; stopping it before re-arming"
    printf '%s\n' 'UnityEditor.EditorApplication.ExitPlaymode();' 'return "stop-requested";' > "$WORK/stop-play.edc"
    edc_words "$WORK/stop-play.edc" stop-play >/dev/null
    # Play を抜けるドメインリロードの間は EDC が返らないので、間を空けて最大 3 回武装し直す
    # EDC cannot answer during the domain reload of leaving Play, so retry arming up to 3 times with a pause
    for _ in 1 2 3; do
      sleep 30
      edc_words "$HERE/exhibition-arm.edc" arm >/dev/null
      grep -q 'exhibition-armed' "$WORK/arm.txt" && break
    done
  fi
  grep -q 'exhibition-armed' "$WORK/arm.txt" || { echo "NG: exhibition mode was not armed: $(edc_result arm)"; exit 1; }
  echo "[driver] $(edc_result arm)"
fi

# 3. 録画を流す。winunity は流す前に PlayMode を止め全体スイッチを立て直す
# 3. Run the recording; winunity stops PlayMode and re-applies the global switch before running
"$WINUNITY" play "$BRANCH" "$SCENARIO" --ui-system "$SYSTEM" --name "$NAME" 2>&1 | tee "$WORK/play.txt"
RESULT=${PIPESTATUS[0]}
RUN_DIR=$(grep '^\[mac\] results: ' "$WORK/play.txt" | tail -1 | sed 's/^\[mac\] results: //')

# 4. 数える: 印より後の行で、選んだ系のロケータの行と Web ロケータの行を数える（GUI Editor が書いている途中のログを共有読みで開く）
# 4. Count: after the mark, count the chosen-system locator line and the Web locator lines (open the log the GUI Editor is writing with shared read)
# 探す語は連結で組み、このコード片自身がログへ写っても数えない
# Build the searched words by concatenation so this snippet never counts itself if it is echoed into the log
cat > "$WORK/count.edc" <<EOF
using System.IO;
using System.Linq;
using UnityEngine;

var mark = "[S5Ladder" + "LogMark] " + "$TOKEN";
var locatorLine = "[PlaytestUi] locator reads only " + "the $SYSTEM UI";
var webPrefix = "[PlaytestUi] " + "Web";
var lines = new System.Collections.Generic.List<string>();
using (var stream = new FileStream(Application.consoleLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
using (var reader = new StreamReader(stream))
{
    string read;
    while ((read = reader.ReadLine()) != null) lines.Add(read.Trim());
}
var start = lines.FindLastIndex(line => line.EndsWith(mark));
if (start < 0) return "locator-log mark-missing";
var after = lines.Skip(start + 1).ToList();
return $"locator-log locator={after.Count(line => line.StartsWith(locatorLine))} web={after.Count(line => line.StartsWith(webPrefix))}";
EOF
edc_words "$WORK/count.edc" count >/dev/null
COUNTED=$(edc_result count)
case "$COUNTED" in
  "locator-log mark-missing") echo "LOCATOR_LOG: mark-missing" ;;
  locator-log\ *) echo "LOCATOR_LOG: ${COUNTED#locator-log }" ;;
  *) echo "LOCATOR_LOG: NG (count EDC failed: $COUNTED)" ;;
esac
echo "RUN_DIR: $RUN_DIR"
exit "$RESULT"
