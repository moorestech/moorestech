#!/usr/bin/env bash
# unity_build_and_test_mac.sh
# 使い方: ./unity_build_and_test_mac.sh /path/to/UnityProject

###############################################################################
# 設定
###############################################################################
UNITY="/Applications/Unity/Hub/Editor/2022.3.18f1/Unity.app/Contents/MacOS/Unity"

# プロジェクトパス（引数が無ければカレントディレクトリ）
PROJECT="${1:-$(pwd)}"

# 一時ファイル
LOGFILE="$(mktemp -t unity_build_XXXX).log"
RESULTS_XML="$(mktemp -t unity_test_XXXX).xml"

###############################################################################
# 1) ビルド & EditMode テスト実行
###############################################################################
"$UNITY" \
  -batchmode -nographics \
  -projectPath "$PROJECT" \
  -runTests -testPlatform editmode \
  -testResults "$RESULTS_XML" \
  -quit \
  -logFile "$LOGFILE"
UNITY_EXIT=$?   # 0=ビルド・テスト成功（テストの合否は XML に出る）

###############################################################################
# 2) Unity 自身の異常終了チェック
###############################################################################
if [ $UNITY_EXIT -ne 0 ]; then
  echo "❌  Unity exited with code $UNITY_EXIT"
  cat "$LOGFILE"
  rm -f "$LOGFILE" "$RESULTS_XML"
  exit $UNITY_EXIT
fi

###############################################################################
# 3) テスト結果 XML をパース
###############################################################################
parse_xml() {
  # 失敗数を取得
  if command -v xmllint >/dev/null 2>&1; then
    FAIL_COUNT=$(xmllint --xpath 'string(/test-run/@failed)' "$RESULTS_XML")
  else
    FAIL_COUNT=$(grep -c 'result="Failed"' "$RESULTS_XML")
  fi

  if [ "$FAIL_COUNT" -eq 0 ]; then
    echo "✅  All EditMode tests passed!"
    return 0
  fi

  echo "❌  $FAIL_COUNT EditMode test(s) failed"
  echo "----------- Failed Tests -----------"

  if command -v xmllint >/dev/null 2>&1; then
    # フルネームと失敗メッセージを抽出（XPath で一気に取得 → sed 整形）
    xmllint --xpath '//test-case[@result="Failed"]' "$RESULTS_XML" \
      | sed -nE 's/.*fullname="([^"]*)".*<failure>.*?<message><!\[CDATA\[(.*?)\]\]>.*$/• \1\n    \2/p'
  else
    # grep/awk フォールバック（XML レイアウト依存／簡易）
    awk '
      /<test-case/ {
        if ($0 ~ /result="Failed"/) {
          match($0, /fullname="?([^"]*)"/, a); fullname=a[1]
          getline; while ($0 !~ /<\/failure>/) { msg = msg $0; getline }
          sub(/^.*<message><!\[CDATA\[/, "", msg)
          sub(/\]\]>.*$/, "", msg)
          printf "• %s\n    %s\n", fullname, msg
          msg=""
        }
      }' "$RESULTS_XML"
  fi
  return 1
}

###############################################################################
# 4) 出力 & 終了コード
###############################################################################
parse_xml
PARSE_EXIT=$?

# ログファイルは常に残したい場合はコメントアウト
rm -f "$LOGFILE" "$RESULTS_XML"

exit $PARSE_EXIT
