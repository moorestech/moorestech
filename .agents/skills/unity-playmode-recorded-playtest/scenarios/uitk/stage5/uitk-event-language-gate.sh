#!/bin/bash
# uitk-event-language-gate.cs の運転役: 全体スイッチを GameUi にし、次の起動 1 回だけを出展モードにしてから run-scenario.sh を前景で流す
# 出展モードの環境変数は Editor のプロセスに要るので、Unity の中の起動口（PlaytestEventLanguageGateBoot）が Play の間だけ立てる
# Driver for uitk-event-language-gate.cs: switch to GameUi, arm exhibition mode for the next boot only, then run run-scenario.sh in the foreground
# The exhibition env vars must live in the Editor process, so the in-Unity boot entry (PlaytestEventLanguageGateBoot) sets them only during Play
#
# usage: uitk-event-language-gate.sh <unity-project-path>
set -u
PROJECT="$1"
HERE="$(cd "$(dirname "$0")" && pwd)"
SKILL_DIR="$(cd "$HERE/../../.." && pwd)"
SCRATCH="$PROJECT/PlaytestResults/scratch/event-language-gate"
mkdir -p "$SCRATCH"

# 終わり方によらず武装を解き、シナリオが言語を戻せずに落ちていたら選ぶ前の言語へ戻す
# However this ends, disarm; if the scenario died before restoring the language, put the pre-choice language back
on_exit() {
  uloop execute-dynamic-code --project-path "$PROJECT" --code 'return Client.Playtest.Boot.PlaytestEventLanguageGateBoot.Disarm();' >/dev/null \
    || echo "ERROR: disarm failed; run Client.Playtest.Boot.PlaytestEventLanguageGateBoot.Disarm() via execute-dynamic-code"
  [ -f "$SCRATCH/language-before.txt" ] || return 0
  local code
  code=$(cat "$SCRATCH/language-before.txt")
  echo "WARN: the scenario left the language changed; restoring $code"
  # 戻せたかは EDC の戻り値の語で見る（終了コードは EDC が走ったかだけを表す）。戻せたときだけ控えを消す
  # Judge the restore by the word EDC returns (the exit code only says EDC ran); delete the note only on success
  local restored
  restored=$(uloop execute-dynamic-code --project-path "$PROJECT" --code "return Client.Localization.Localize.TrySetChosenLanguage(\"$code\") ? \"language-restored\" : \"language-restore-failed\";")
  if echo "$restored" | grep -q "language-restored"; then
    rm -f "$SCRATCH/language-before.txt"
  else
    echo "ERROR: language restore to $code failed; language-before.txt kept ($SCRATCH): $restored"
  fi
}
trap on_exit EXIT

# 武装できたかは EDC の戻り値の語で見る（終了コードは EDC が走ったかだけ。Play 中なら ArmForNextBoot は "ERROR: already playing" を返す）
# Judge arming by the word EDC returns (the exit code only says EDC ran; during Play, ArmForNextBoot returns "ERROR: already playing")
armed=$(uloop execute-dynamic-code --project-path "$PROJECT" --code 'Client.Starter.Initialization.Boot.UiSystemEditorSwitch.Set(Client.Input.UiSystem.GameUi); return Client.Playtest.Boot.PlaytestEventLanguageGateBoot.ArmForNextBoot();') \
  || { echo "NG: could not switch to GameUi or arm exhibition mode (EDC failed)"; exit 1; }
echo "$armed" | grep -q "exhibition-armed" \
  || { echo "NG: exhibition mode was not armed: $armed"; exit 1; }
bash "$SKILL_DIR/scripts/run-scenario.sh" "$PROJECT" "$HERE/uitk-event-language-gate.cs"
