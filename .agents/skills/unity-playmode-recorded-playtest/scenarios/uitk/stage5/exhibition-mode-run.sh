#!/bin/bash
# Mac の Editor で出展モードのシナリオを流す運転役（uloop）: 全体スイッチを立て、次の起動 1 回だけを出展モードにしてから run-scenario.sh を前景で流す
# 武装と後始末の C# は Windows の運転役と同じ driver/*.edc を --code-file で読む（C# は 1 か所）
# Mac driver (uloop) for exhibition-mode scenarios: set the global switch, arm exhibition mode for the next boot only, then run run-scenario.sh in the foreground
# Arming and cleanup read the same driver/*.edc via --code-file as the Windows driver (the C# lives in one place)
#
# usage: exhibition-mode-run.sh <unity-project-path> <GameUi|Web> <scenario.cs>
set -u
if [ "$#" -ne 3 ]; then echo "usage: exhibition-mode-run.sh <unity-project-path> <GameUi|Web> <scenario.cs>"; exit 2; fi
PROJECT="$1"; SYSTEM="$2"; SCENARIO="$3"
case "$SYSTEM" in GameUi|Web) ;; *) echo "NG: UI system must be GameUi or Web (got $SYSTEM)"; exit 2 ;; esac
[ -d "$PROJECT" ] || { echo "NG: project not found: $PROJECT"; exit 2; }
[ -f "$SCENARIO" ] || { echo "NG: scenario not found: $SCENARIO"; exit 2; }
HERE="$(cd "$(dirname "$0")" && pwd)"
SKILL_DIR="$(cd "$HERE/../../.." && pwd)"

# uloop の出力から Result の値だけを取る（無ければ空。失敗応答に載る自分のソースの語を成功と読まないため）
# Take only the Result value from uloop output (empty when none, so source text echoed in a failure response is never read as success)
result_value() {
  echo "$1" | grep -o '"Result"[[:space:]]*:[[:space:]]*"[^"]*"' | tail -1 | sed 's/^"Result"[[:space:]]*:[[:space:]]*"//; s/"$//'
}

# 終わり方によらず武装を解き、シナリオが言語を戻せずに落ちていたら選ぶ前の言語へ戻す。戻せたかは EDC の戻り値の語で見る
# However this ends, disarm and restore the pre-choice language if the scenario died first; judge it by the word EDC returns
on_exit() {
  local code=$? restored
  restored=$(uloop execute-dynamic-code --project-path "$PROJECT" --code-file "$HERE/driver/exhibition-disarm.edc" 2>&1)
  case "$(result_value "$restored")" in *language-untouched*|*language-restored*) exit "$code" ;; esac
  echo "ERROR: disarm or language restore failed; language-before.txt kept ($PROJECT/PlaytestResults/scratch/event-language-gate): $restored"
  exit 1
}
trap on_exit EXIT

# 武装できたかは EDC の戻り値の語で見る（終了コードは EDC が走ったかだけ。Play 中なら ArmForNextBoot は "ERROR: already playing" を返す）
# Judge arming by the word EDC returns (the exit code only says EDC ran; during Play, ArmForNextBoot returns "ERROR: already playing")
switched=$(uloop execute-dynamic-code --project-path "$PROJECT" --code "Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.$SYSTEM); return \"switched\";" 2>&1)
[ "$(result_value "$switched")" = switched ] || { echo "NG: could not switch to $SYSTEM: $switched"; exit 1; }
armed=$(uloop execute-dynamic-code --project-path "$PROJECT" --code-file "$HERE/driver/exhibition-arm.edc" 2>&1)
[ "$(result_value "$armed")" = exhibition-armed ] || { echo "NG: exhibition mode was not armed: $armed"; exit 1; }
bash "$SKILL_DIR/scripts/run-scenario.sh" "$PROJECT" "$SCENARIO"
