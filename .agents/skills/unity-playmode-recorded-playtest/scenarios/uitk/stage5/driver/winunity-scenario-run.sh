#!/bin/bash
# Mac から Windows の GUI Editor でシナリオを 1 本流す運転役: （出展モードなら武装の EDC）→ winunity play → （復元の EDC）
# 出展モードの武装は GUI Editor の SessionState にあるので、この 1 回の中で武装・play・復元を続けて流す（間に同じ worktree の compile/test を挟むと Editor ごと消える）
# Driver that runs one scenario on the Windows GUI Editor from the Mac: (arm EDC in exhibition mode) -> winunity play -> (restore EDC)
# Exhibition arming lives in the GUI Editor's SessionState, so arm, play and restore run back to back here (a compile/test on the same worktree in between closes the Editor and drops it)
#
# usage: winunity-scenario-run.sh <branch> <normal|exhibition> <scenario.cs> <winunity-name>
# branch は push 済みであること（winunity が GitHub から取る）。WINUNITY で winunity の実体を差し替えられる
# The branch must be pushed (winunity fetches it from GitHub); WINUNITY overrides the winunity executable
set -u
if [ "$#" -ne 4 ]; then echo "usage: winunity-scenario-run.sh <branch> <normal|exhibition> <scenario.cs> <winunity-name>"; exit 2; fi
BRANCH="$1"; MODE="$2"; SCENARIO="$3"; NAME="$4"
case "$MODE" in normal|exhibition) ;; *) echo "NG: mode must be normal or exhibition (got $MODE)"; exit 2 ;; esac
[ -f "$SCENARIO" ] || { echo "NG: scenario not found: $SCENARIO"; exit 2; }
HERE="$(cd "$(dirname "$0")" && pwd)"
WINUNITY="${WINUNITY:-$HOME/bin/winunity}"
WORK="$(mktemp -d)"
RESULT=0
RUN_DIR=""

# コード片を GUI Editor で流し、出力を見せて残す（語の判定は呼び手が grep で行う。終了コードは EDC が走ったかしか表さない）
# 起こした直後の Editor はコンパイル・リロードで受けた要求を切ることがあるので、uloop が再試行可と言う間は 45 秒おきに最大 3 回流す（どのコード片も何度流しても同じ結果）
# Run a snippet in the GUI Editor, show and keep the output (callers grep for the word; the exit code only says EDC ran)
# A freshly started Editor can drop an accepted request while compiling/reloading, so retry every 45 s up to 3 times while uloop says retryable (every snippet here is idempotent)
edc_words() {
  local attempt
  for attempt in 1 2 3; do
    "$WINUNITY" edc "$BRANCH" "$1" --name "$NAME" > "$WORK/$2.txt" 2>&1
    grep -q '"Retryable": true' "$WORK/$2.txt" || break
    echo "[driver] $2 EDC was dropped by the Editor (attempt $attempt): $(grep -o '"ErrorCode": "[^"]*"' "$WORK/$2.txt" | head -1)" >&2
    [ "$attempt" -lt 3 ] && sleep 45
  done
  cat "$WORK/$2.txt"
}

# EDC の出力から uloop の Result の値だけを取る（無ければ空。失敗応答に載る自分のソースの語を成功と読まないため）
# Take only uloop's Result value from an EDC output (empty when there is none, so source text echoed in a failure response is never read as success)
edc_value() {
  grep -o '"Result"[[:space:]]*:[[:space:]]*"[^"]*"' "$WORK/$1.txt" | tail -1 | sed 's/^"Result"[[:space:]]*:[[:space:]]*"//; s/"$//'
}

# 表示用: Result の値、無ければ出力の最後の行
# For display: the Result value, or the output's last line when there is none
edc_result() {
  local value
  value=$(edc_value "$1")
  if [ -n "$value" ]; then echo "$value"; else tail -1 "$WORK/$1.txt"; fi
}

# 終わり方によらず武装を解き、シナリオが言語を戻せずに落ちていたら選ぶ前の言語へ戻す（出展モードのときだけ張る）
# However this ends, disarm and restore the pre-choice language if the scenario died before doing so (set only in exhibition mode)
on_exit() {
  local code=$?
  echo "[driver] disarm and restore the language"
  edc_words "$HERE/exhibition-disarm.edc" disarm >/dev/null
  echo "[driver] disarm: $(edc_result disarm)"
  case "$(edc_value disarm)" in *language-untouched*|*language-restored*) exit "$code" ;; esac
  echo "ERROR: disarm or language restore failed; language-before.txt kept on Windows: $(edc_result disarm)"
  exit 1
}

# 1. 出展モードなら武装する。PlayMode が残っていたら止めてから武装し直す（武装しないまま通常の起動で流さない）
# 1. In exhibition mode, arm; if a PlayMode is left, stop it and re-arm (never run a normal boot unarmed)
if [ "$MODE" = exhibition ]; then
  trap on_exit EXIT
  edc_words "$HERE/exhibition-arm.edc" arm >/dev/null
  if [ "$(edc_value arm)" != exhibition-armed ] && [[ "$(edc_value arm)" == *'already playing'* ]]; then
    echo "[driver] PlayMode is still running; stopping it before re-arming"
    printf '%s\n' 'UnityEditor.EditorApplication.ExitPlaymode();' 'return "stop-requested";' > "$WORK/stop-play.edc"
    edc_words "$WORK/stop-play.edc" stop-play >/dev/null
    # Play を抜けるドメインリロードの間は EDC が返らないので、間を空けて最大 3 回武装し直す
    # EDC cannot answer during the domain reload of leaving Play, so retry arming up to 3 times with a pause
    for _ in 1 2 3; do
      sleep 30
      edc_words "$HERE/exhibition-arm.edc" arm >/dev/null
      [ "$(edc_value arm)" = exhibition-armed ] && break
    done
  fi
  [ "$(edc_value arm)" = exhibition-armed ] || { echo "NG: exhibition mode was not armed: $(edc_result arm)"; exit 1; }
  echo "[driver] $(edc_result arm)"
fi

# 2. 録画を流す。winunity は流す前に PlayMode を止める
# 2. Run the recording; winunity stops PlayMode before running
"$WINUNITY" play "$BRANCH" "$SCENARIO" --name "$NAME" 2>&1 | tee "$WORK/play.txt"
RESULT=${PIPESTATUS[0]}
RUN_DIR=$(grep '^\[mac\] results: ' "$WORK/play.txt" | tail -1 | sed 's/^\[mac\] results: //')

echo "RUN_DIR: $RUN_DIR"
exit "$RESULT"
