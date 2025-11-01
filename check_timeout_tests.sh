#!/usr/bin/env bash
# タイムアウトするテストを検出するスクリプト

INPUT_FILE="all_tests_checklist.txt"
OUTPUT_FILE="all_tests_checklist.txt.tmp"
PROJECT_PATH="moorestech_server"
TIMEOUT_PREFIX="🔁 TIMEOUT"

# 出力ファイルを初期化
> "$OUTPUT_FILE"

# カウンター
TOTAL=0
CHECKED=0
TIMEOUT_COUNT=0

echo "Starting timeout test check..."
echo "================================"

# ファイルを1行ずつ読み込む
while IFS= read -r line; do
    # 既にチェック済みまたはタイムアウトマーク済みの行はスキップ
    if [[ "$line" =~ ^-\ \[x\] ]] || [[ "$line" =~ ^$TIMEOUT_PREFIX ]]; then
        echo "$line" >> "$OUTPUT_FILE"
        continue
    fi

    # 未チェックのテスト行を処理
    if [[ "$line" =~ ^-\ \[\ \]\ (.+)$ ]]; then
        TEST_NAME="${BASH_REMATCH[1]}"
        TOTAL=$((TOTAL + 1))

        echo ""
        echo "[$TOTAL] Testing: $TEST_NAME"
        echo "  Step 1: Running with 5s timeout..."

        # エスケープされた正規表現を作成
        REGEX="^${TEST_NAME}$"

        # 5秒タイムアウトで実行
        if ./tools/unity-test.sh "$PROJECT_PATH" "$REGEX" -t 5 > /dev/null 2>&1; then
            # 成功
            echo "  ✅ Passed (5s)"
            echo "- [x] $TEST_NAME" >> "$OUTPUT_FILE"
            CHECKED=$((CHECKED + 1))
        else
            EXIT_CODE=$?
            if [ $EXIT_CODE -eq 124 ]; then
                # タイムアウト: 60秒で2回実行
                echo "  ⏱️  Timeout (5s) - Retrying with 60s timeout..."

                TIMEOUT_60S_COUNT=0

                # 1回目
                echo "  Step 2-1: Running with 60s timeout (1st attempt)..."
                if ! ./tools/unity-test.sh "$PROJECT_PATH" "$REGEX" -t 60 > /dev/null 2>&1; then
                    if [ $? -eq 124 ]; then
                        echo "  ⏱️  Timeout (60s, 1st)"
                        TIMEOUT_60S_COUNT=$((TIMEOUT_60S_COUNT + 1))
                    else
                        echo "  ❌ Failed (60s, 1st)"
                    fi
                else
                    echo "  ✅ Passed (60s, 1st)"
                fi

                # 2回目
                echo "  Step 2-2: Running with 60s timeout (2nd attempt)..."
                if ! ./tools/unity-test.sh "$PROJECT_PATH" "$REGEX" -t 60 > /dev/null 2>&1; then
                    if [ $? -eq 124 ]; then
                        echo "  ⏱️  Timeout (60s, 2nd)"
                        TIMEOUT_60S_COUNT=$((TIMEOUT_60S_COUNT + 1))
                    else
                        echo "  ❌ Failed (60s, 2nd)"
                    fi
                else
                    echo "  ✅ Passed (60s, 2nd)"
                fi

                # 結果を記録
                if [ $TIMEOUT_60S_COUNT -eq 2 ]; then
                    echo "  🔁 TIMEOUT DETECTED - Both 60s attempts timed out"
                    echo "$TIMEOUT_PREFIX - [ ] $TEST_NAME" >> "$OUTPUT_FILE"
                    TIMEOUT_COUNT=$((TIMEOUT_COUNT + 1))
                else
                    echo "  ✅ Eventually passed"
                    echo "- [x] $TEST_NAME" >> "$OUTPUT_FILE"
                    CHECKED=$((CHECKED + 1))
                fi
            else
                # コンパイルエラーやその他の失敗
                echo "  ❌ Failed (non-timeout)"
                echo "- [x] $TEST_NAME" >> "$OUTPUT_FILE"
                CHECKED=$((CHECKED + 1))
            fi
        fi
    else
        # そのままコピー
        echo "$line" >> "$OUTPUT_FILE"
    fi
done < "$INPUT_FILE"

# 結果をバックアップして置き換え
mv "$INPUT_FILE" "${INPUT_FILE}.backup"
mv "$OUTPUT_FILE" "$INPUT_FILE"

echo ""
echo "================================"
echo "Summary:"
echo "  Total tests: $TOTAL"
echo "  Checked: $CHECKED"
echo "  Timeout (60s x2): $TIMEOUT_COUNT"
echo ""
echo "Backup saved to: ${INPUT_FILE}.backup"
echo "Results saved to: $INPUT_FILE"
