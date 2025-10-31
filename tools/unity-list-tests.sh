#!/usr/bin/env bash
# unity-list-tests.sh
# 使い方:
#   ./unity-list-tests.sh <UnityProjectPath>
#
# 説明:
#   指定されたUnityプロジェクト内の全テスト名をリストアップします

UNITY="/Applications/Unity/Hub/Editor/6000.2.6f2/Unity.app/Contents/MacOS/Unity"

###############################################################################
# 引数パース
###############################################################################
if [ $# -lt 1 ]; then
  echo "Usage: $0 <UnityProjectPath>"
  exit 1
fi

PROJECT="$1"

###############################################################################
# Unity 実行
###############################################################################
LOGFILE="$(mktemp -t unity_list_tests_XXXX).log"

"$UNITY" \
  -projectPath "$PROJECT" \
  -executeMethod CliTestRunner.ListAllTests \
  -batchmode \
  -logFile "$LOGFILE"
RET=$?

###############################################################################
# 出力処理
###############################################################################
# コンパイルエラーのチェック
if [ $RET -eq 2 ] || \
   grep -q "Scripts have compiler errors" "$LOGFILE" || \
   grep -qE "error CS[0-9]{4}:" "$LOGFILE" || \
   grep -q "Safe Mode" "$LOGFILE"
then
  echo "❌ Compile errors detected"
  grep -E "error CS[0-9]{4}:" "$LOGFILE" | sed 's/^/    /'
  echo "❌ Compilation failed — tests list could not be retrieved"
  RET=1
else
  # [CliTest] 行（テスト名リスト）だけ表示
  grep '\[CliTest\]' "$LOGFILE" | sed 's/\[CliTest\][[:space:]]*//'
fi

rm -f "$LOGFILE"
exit $RET
