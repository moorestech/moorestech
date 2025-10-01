#!/usr/bin/env bash
# unity_test.sh
# 使い方:
#   ./unity_test.sh <UnityProjectPath> 'Regex' [isGui] [-requiredString 'string']
#     または ./unity_test.sh <UnityProjectPath> -testRegex 'Regex' [isGui] [-requiredString 'string']
#   ※ デフォルトでバッチモード(-batchmode)で起動。isGui を付けるとGUIモードで起動します
#   ※ -requiredString を指定すると、出力に指定した文字列が含まれていない場合エラーとします

UNITY="/Applications/Unity/Hub/Editor/6000.1.6f1/Unity.app/Contents/MacOS/Unity"

###############################################################################
# 引数パース
###############################################################################
if [ $# -lt 2 ]; then
  echo "Usage: $0 <UnityProjectPath> '<Regex>' [isGui] [-requiredString 'string']"
  exit 1
fi

PROJECT="$1"; shift
case "$1" in -testRegex|-r|--regex) shift ;; esac
REGEX="$1"; shift

# ### 変更: isGui の有無を検出してフラグ化（デフォルトはバッチモード）
IS_GUI=0
REQUIRED_STRING=""

while [ $# -gt 0 ]; do
  case "$1" in
    -requiredString|-rs|--required-string)
      if [ -n "$2" ] && [ "${2#-}" = "$2" ]; then
        REQUIRED_STRING="$2"
        shift 2
      else
        echo "Error: $1 requires an argument"
        exit 1
      fi
      ;;
    isGui|-g|--gui)
      IS_GUI=1
      shift
      ;;
    *)
      shift
      ;;
  esac
done

# ### 変更: バッチモードのオプションを組み立て（デフォルトでバッチモード）
BATCH_OPTS="-batchmode"
if [ $IS_GUI -eq 1 ]; then
  BATCH_OPTS=""
fi

###############################################################################
# Unity 実行
###############################################################################
LOGFILE="$(mktemp -t unity_cli_XXXX).log"

"$UNITY" \
  -projectPath "$PROJECT" \
  -executeMethod CliTestRunner.Run \
  -testRegex "$REGEX" \
  -isFromShellScript \
  -logFile "$LOGFILE" \
  $BATCH_OPTS
RET=$?

###############################################################################
# 出力処理
###############################################################################
# EndWrite before BeginWrite エラーのチェック
if grep -q "Unhandled log message: '\[Assert\] Calling EndWrite before BeginWrite'" "$LOGFILE"; then
  echo "不明なエラーが発生したため、テスト結果が出力できませんでした。テストを再実行してください。"
  RET=1
elif [ $RET -eq 2 ] || \
     grep -q "Scripts have compiler errors" "$LOGFILE" || \
     grep -qE "error CS[0-9]{4}:" "$LOGFILE" || \
     grep -q "Safe Mode" "$LOGFILE"
then
  # --- ❶ コンパイルエラー行を抽出 ------------------------------------------
  echo "❌ Compile errors detected"
  #   Unity が出力する例: Assets/Scripts/Foo.cs(12,18): error CS1002: ; expected
  #   error CSxxxx を取得
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
# EndWrite before BeginWrite エラーの場合は最終メッセージを出力しない
if grep -q "Unhandled log message: '\[Assert\] Calling EndWrite before BeginWrite'" "$LOGFILE" || \
     grep -q "No tests matched" "$LOGFILE"
then
  : # 何も出力しない
elif [ $RET -eq 0 ]; then
  echo "✅  All matching tests passed"
else
  echo "❌  Some matching tests failed or compilation failed"
fi

###############################################################################
# 必須文字列チェック
###############################################################################
if [ -n "$REQUIRED_STRING" ]; then
  if ! grep -qF "$REQUIRED_STRING" "$LOGFILE"; then
    echo "規定のテストが実行できませんでした。テストを再実行してください。"
    RET=1
  fi
fi

rm -f "$LOGFILE"
exit $RET
