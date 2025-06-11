#!/usr/bin/env bash
# unity_compile_mac.sh
# usage: ./unity_compile_mac.sh /path/to/UnityProject

UNITY="/Applications/Unity/Hub/Editor/6000.1.6f1/Unity.app/Contents/MacOS/Unity"

# プロジェクトパス（引数が無ければカレントディレクトリ）
PROJECT="${1:-$(pwd)}"

# 一時ログファイル
LOGFILE="$(mktemp -t unity_compile_XXXX).log"

# --- コンパイル実行 -----------------------------------------------------------
"$UNITY" \
  -batchmode -nographics \
  -projectPath "$PROJECT" \
  -quit \
  -logFile "$LOGFILE"
RESULT=$?        # 0=成功 / 1=コンパイルエラー

# --- 結果判定と出力 -----------------------------------------------------------
if [ $RESULT -eq 0 ]; then
  echo "Success compile"
else
  # "## Output:" 行があればそれ以降を、無ければ全文
  if grep -q '^## Output:' "$LOGFILE"; then
    awk '/^## Output:/{flag=1;next} flag' "$LOGFILE"
  else
    cat "$LOGFILE"
  fi
fi

# --- 後片付け -----------------------------------------------------------------
rm -f "$LOGFILE"
exit $RESULT
