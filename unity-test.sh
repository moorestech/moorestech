#!/usr/bin/env bash
# unity_run_tests.sh
# 使い方: ./unity_run_tests.sh <UnityProjectPath> '<RegexForTests>'

UNITY="/Applications/Unity/Hub/Editor/2022.3.18f1/Unity.app/Contents/MacOS/Unity"
PROJECT="$1"
REGEX="$2"

if [ -z "$PROJECT" ] || [ -z "$REGEX" ]; then
  echo "Usage: $0 /path/to/UnityProject 'TestNameRegex'"
  exit 1
fi

LOGFILE="$(mktemp -t unity_cli_XXXX).log"

"$UNITY" \
  -batchmode -nographics \
  -projectPath "$PROJECT" \
  -executeMethod CliTestRunner.Run \
  -testRegex "$REGEX" \
  -logFile "$LOGFILE" \
  -quit
RET=$?

###############################################################################
# [CliTest] 行だけ出力し、タグを取り除く
###############################################################################
if grep -q '\[CliTest\]' "$LOGFILE"; then
  # 行頭・行中どこにあっても OK。最初のタグと後続スペースを削除。
  grep '\[CliTest\]' "$LOGFILE" | sed 's/\[CliTest\][[:space:]]*//'
fi

###############################################################################
# 成否メッセージ
###############################################################################
if [ $RET -eq 0 ]; then
  echo "✅  All matching tests passed"
else
  echo "❌  Some matching tests failed"
fi

rm -f "$LOGFILE"
exit $RET
