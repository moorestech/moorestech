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

"$UNITY" \
  -batchmode -nographics \
  -projectPath "$PROJECT" \
  -executeMethod CliTestRunner.Run \
  -testRegex "$REGEX" \
  -quit
RET=$?

if [ $RET -eq 0 ]; then
  echo "✅  All matching tests passed"
else
  echo "❌  Some matching tests failed"
fi
exit $RET
