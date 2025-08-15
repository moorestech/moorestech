#!/usr/bin/env bash
# unity_test.sh
# 使い方: ./unity_test.sh <UnityProjectPath> 'Regex'  (または -testRegex 'Regex')

UNITY="/Applications/Unity/Hub/Editor/6000.1.6f1/Unity.app/Contents/MacOS/Unity"

###############################################################################
# 引数パース
###############################################################################
if [ $# -lt 2 ]; then
  echo "Usage: $0 <UnityProjectPath> '<Regex>'"
  exit 1
fi

PROJECT="$1"; shift
case "$1" in -testRegex|-r|--regex) shift ;; esac
REGEX="$1"

###############################################################################
# Unity 実行
###############################################################################
LOGFILE="$(mktemp -t unity_cli_XXXX).log"

"$UNITY" \
  -batchmode -nographics \
  -projectPath "$PROJECT" \
  -executeMethod CliTestRunner.Run \
  -testRegex "$REGEX" \
  -logFile "$LOGFILE"
RET=$?

###############################################################################
# 出力処理
###############################################################################
if [ $RET -eq 2 ] || grep -q "Scripts have compiler errors" "$LOGFILE"; then
  # --- ❶ コンパイルエラー行を抽出 ------------------------------------------
  echo "❌ Compile errors detected"
  #   Unity が出力する例: Assets/Scripts/Foo.cs(12,18): error CS1002: ; expected
  #   error CS でも error CSxxxx でも取得
  grep -E "error CS[0-9]{4}:" "$LOGFILE" | sed 's/^/    /'
  echo "❌ Compilation failed — tests were not executed"
  RET=1        # CI で失敗扱いにしたいので必ず 1
else
  # --- ❷ [CliTest] 行 (テスト結果) だけ表示 -------------------------------
  grep '\[CliTest\]' "$LOGFILE" | sed 's/\[CliTest\][[:space:]]*//'
fi

###############################################################################
# 最終メッセージ
###############################################################################
if [ $RET -eq 0 ]; then
  echo "✅  All matching tests passed"
else
  echo "❌  Some matching tests failed or compilation failed"
fi

rm -f "$LOGFILE"
exit $RET
