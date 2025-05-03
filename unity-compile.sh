#!/bin/bash
UNITY="/Applications/Unity/Hub/Editor/2022.3.18f1/Unity.app/Contents/MacOS/Unity"
PROJECT="${1:-$(pwd)}"

"$UNITY" \
  -batchmode -nographics -projectPath "$PROJECT" \
  -quit \
  -logFile -            # - を渡すとログが標準出力に流れる
RESULT=$?               # 0 = 成功, 1 = コンパイル失敗

exit $RESULT
