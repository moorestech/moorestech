#!/bin/bash
set -euo pipefail

# moorestech Linux Dedicated Server ビルドスクリプト
# Build moorestech Linux Dedicated Server locally

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/../moorestech_server"
UNITY_EDITOR="/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity"
BUILD_METHOD="BuildPipeline.LinuxDedicatedServerFromGithubAction"
OUTPUT_DIR="$PROJECT_DIR/Output_DedicatedServer_StandaloneLinux64"
LOG_FILE="$SCRIPT_DIR/build.log"

echo "=== moorestech Linux Dedicated Server Build ==="

# Unity Editorの存在確認
# Verify Unity Editor exists
if [ ! -f "$UNITY_EDITOR" ]; then
    echo "ERROR: Unity Editor not found at $UNITY_EDITOR"
    exit 1
fi

# 既存ビルドがあればスキップ（強制リビルドは OUTPUT_DIR を削除してから実行）
# Skip if build already exists (delete OUTPUT_DIR to force rebuild)
if [ -d "$OUTPUT_DIR" ] && [ -f "$OUTPUT_DIR/moorestech_server" ]; then
    echo "Build already exists at $OUTPUT_DIR, skipping build."
    echo "Delete $OUTPUT_DIR to force rebuild."
    exit 0
fi

echo "Building with Unity ($UNITY_EDITOR)..."
echo "Project: $PROJECT_DIR"
echo "Method: $BUILD_METHOD"
echo "Log: $LOG_FILE"

# Unityバッチモードでビルド実行
# Execute Unity batch mode build
"$UNITY_EDITOR" \
    -batchmode \
    -quit \
    -projectPath "$PROJECT_DIR" \
    -executeMethod "$BUILD_METHOD" \
    -buildTarget Linux64 \
    -logFile "$LOG_FILE"

# ビルド結果の確認
# Verify build output
if [ ! -f "$OUTPUT_DIR/moorestech_server" ]; then
    echo "ERROR: Build failed. Check $LOG_FILE for details."
    tail -20 "$LOG_FILE"
    exit 1
fi

echo "Build succeeded: $OUTPUT_DIR/moorestech_server"
