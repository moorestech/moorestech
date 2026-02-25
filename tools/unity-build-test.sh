#!/usr/bin/env bash
# unity-build-test.sh
# 使い方: ./unity-build-test.sh <UnityProjectPath> [BuildOutputPath] [--dedicated-server]
# 例: ./unity-build-test.sh moorestech_client
# 例: ./unity-build-test.sh moorestech_server
# 例: ./unity-build-test.sh moorestech_server --dedicated-server
# 例: ./unity-build-test.sh moorestech_server /path/to/output --dedicated-server

UNITY="/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity"

###############################################################################
# 引数パース
###############################################################################
if [ $# -lt 1 ]; then
  echo "Usage: $0 <UnityProjectPath> [BuildOutputPath] [--dedicated-server]"
  echo "Example: $0 moorestech_client"
  echo "Example: $0 moorestech_server"
  echo "Example: $0 moorestech_server --dedicated-server"
  exit 1
fi

PROJECT="$1"
shift

# オプション解析
# Parse options
DEDICATED_SERVER=false
OUTPUT_PATH=""
while [ $# -gt 0 ]; do
  case "$1" in
    --dedicated-server)
      DEDICATED_SERVER=true
      ;;
    *)
      OUTPUT_PATH="$1"
      ;;
  esac
  shift
done

if [ -z "$OUTPUT_PATH" ]; then
  OUTPUT_PATH="$PROJECT/Library/ShellScriptBuild"
fi

# プロジェクトパスが存在するか確認
if [ ! -d "$PROJECT" ]; then
  echo "❌ Error: Unity project directory not found: $PROJECT"
  exit 1
fi

# 出力ディレクトリを作成（存在しない場合）
mkdir -p "$OUTPUT_PATH"

###############################################################################
# サーバーかクライアントかを判定
###############################################################################
IS_SERVER=false
if echo "$PROJECT" | grep -qi "server"; then
  IS_SERVER=true
fi

###############################################################################
# ビルドターゲットを判定（MacまたはWindows）
###############################################################################
if [[ "$OSTYPE" == "darwin"* ]]; then
  BUILD_TARGET="StandaloneOSX"
  if [ "$IS_SERVER" = true ]; then
    if [ "$DEDICATED_SERVER" = true ]; then
      # macOS Dedicated Serverは非対応、Linuxにフォールバック
      # macOS Dedicated Server is not supported, falling back to Linux
      echo "⚠️  macOS Dedicated Server is not supported. Falling back to Linux Dedicated Server."
      BUILD_TARGET="StandaloneLinux64"
      BUILD_METHOD="BuildPipeline.LinuxDedicatedServerFromGithubAction"
      EXECUTABLE_NAME="moorestech_server"
    else
      BUILD_METHOD="BuildPipeline.MacOsBuildFromGithubAction"
      EXECUTABLE_NAME="moorestech_server"
    fi
  else
    BUILD_METHOD="BuildPipeline.MacOsBuildFromGithubAction"
    EXECUTABLE_NAME="moorestech"
  fi
elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]] || [[ "$OSTYPE" == "win32" ]]; then
  BUILD_TARGET="StandaloneWindows64"
  if [ "$IS_SERVER" = true ]; then
    if [ "$DEDICATED_SERVER" = true ]; then
      BUILD_METHOD="BuildPipeline.WindowsDedicatedServerFromGithubAction"
      EXECUTABLE_NAME="moorestech_server.exe"
    else
      BUILD_METHOD="BuildPipeline.WindowsBuildFromGithubAction"
      EXECUTABLE_NAME="moorestech_server.exe"
    fi
  else
    BUILD_METHOD="BuildPipeline.WindowsBuildFromGithubAction"
    EXECUTABLE_NAME="moorestech.exe"
  fi
else
  # Linux or other
  BUILD_TARGET="StandaloneLinux64"
  if [ "$IS_SERVER" = true ]; then
    if [ "$DEDICATED_SERVER" = true ]; then
      BUILD_METHOD="BuildPipeline.LinuxDedicatedServerFromGithubAction"
      EXECUTABLE_NAME="moorestech_server"
    else
      BUILD_METHOD="BuildPipeline.LinuxBuildFromGithubAction"
      EXECUTABLE_NAME="moorestech_server"
    fi
  else
    BUILD_METHOD="BuildPipeline.LinuxBuildFromGithubAction"
    EXECUTABLE_NAME="moorestech"
  fi
fi

# Dedicated Serverの場合、出力ディレクトリ名のプレフィックスが異なる
# Dedicated Server uses different output directory prefix
if [ "$DEDICATED_SERVER" = true ]; then
  OUTPUT_DIR_PREFIX="Output_DedicatedServer_"
else
  OUTPUT_DIR_PREFIX="Output_"
fi

echo "🔨 Starting Unity build..."
echo "  Project: $PROJECT"
echo "  Target: $BUILD_TARGET"
echo "  Server: $IS_SERVER"
echo "  Dedicated Server: $DEDICATED_SERVER"
echo "  Build Method: $BUILD_METHOD"
echo "  Output: $OUTPUT_PATH"
echo ""

###############################################################################
# Unity ビルド実行
###############################################################################
LOGFILE="$(mktemp -t unity_build_XXXX).log"

echo "📝 Build log: $LOGFILE"
echo "⏳ Building... (this may take several minutes)"
echo ""

# Unityビルド実行
"$UNITY" \
  -projectPath "$PROJECT" \
  -executeMethod "$BUILD_METHOD" \
  -buildTarget "$BUILD_TARGET" \
  -batchmode \
  -quit \
  -logFile "$LOGFILE" 2>&1

RET=$?

###############################################################################
# ビルド結果の処理
###############################################################################

# ビルド出力先を特定（BuildPipeline.csのロジックに基づく）
BUILD_OUTPUT="$PROJECT/${OUTPUT_DIR_PREFIX}$BUILD_TARGET"

# Unityのログから実際のビルド結果を確認
BUILD_RESULT=$(grep "Build Result :" "$LOGFILE" | tail -1 | sed 's/.*Build Result ://')

if [ "$BUILD_RESULT" = "Succeeded" ]; then
  # ビルドが成功した場合
  echo "✅ Build completed successfully!"
  echo ""

  if [ -d "$BUILD_OUTPUT" ]; then
    echo "📦 Build output:"
    echo "  Directory: $BUILD_OUTPUT"
    if [ -d "$BUILD_OUTPUT/moorestech_server.app" ]; then
      echo "  Application: $BUILD_OUTPUT/moorestech_server.app"
    elif [ -d "$BUILD_OUTPUT/moorestech.app" ]; then
      echo "  Application: $BUILD_OUTPUT/moorestech.app"
    else
      echo "  Executable: $BUILD_OUTPUT/$EXECUTABLE_NAME"
    fi

    # 指定された出力パスにコピー（デフォルトパスと異なる場合）
    if [ "$OUTPUT_PATH" != "$BUILD_OUTPUT" ]; then
      echo ""
      echo "📋 Copying to specified output path..."
      rm -rf "$OUTPUT_PATH"
      cp -r "$BUILD_OUTPUT" "$OUTPUT_PATH"
      echo "  Copied to: $OUTPUT_PATH"
    fi

    # ビルドサイズを表示
    echo ""
    echo "📊 Build statistics:"
    if [[ "$OSTYPE" == "darwin"* ]]; then
      SIZE=$(du -sh "$BUILD_OUTPUT" | cut -f1)
    else
      SIZE=$(du -sh "$BUILD_OUTPUT" | cut -f1)
    fi
    echo "  Total size: $SIZE"
  else
    echo "⚠️  Build output directory not found: $BUILD_OUTPUT"
  fi
  RET=0
else
  # ビルドが失敗した場合
  echo "❌ Build failed"
  echo "  Unity reported: $BUILD_RESULT"
  echo ""
  RET=1

  # コンパイルエラーを抽出して表示
  if grep -q "Scripts have compiler errors" "$LOGFILE"; then
    echo "🔴 Compilation errors detected:"
    echo ""
    # Unity が出力するエラー形式: Assets/Scripts/Foo.cs(12,18): error CS1002: ; expected
    grep -E "error CS[0-9]{4}:" "$LOGFILE" | head -20 | sed 's/^/    /'

    ERROR_COUNT=$(grep -c "error CS[0-9]{4}:" "$LOGFILE")
    if [ "$ERROR_COUNT" -gt 20 ]; then
      echo ""
      echo "    ... and $((ERROR_COUNT - 20)) more errors"
    fi
  fi

  # Unityコンソールログを表示（エラーと警告のみ）
  echo ""
  echo "📋 Unity Console Errors and Warnings:"
  echo "────────────────────────────────────────"

  # エラーメッセージを抽出
  ERROR_MESSAGES=$(grep -A1 "UnityEngine\.Debug:LogError" "$LOGFILE" | grep -v "UnityEngine\.Debug:LogError" | grep -v "^--$" | grep -v "^\s*$")
  if [ -n "$ERROR_MESSAGES" ]; then
    echo "$ERROR_MESSAGES" | head -10 | while IFS= read -r line; do
      if [ -n "$line" ]; then
        # スタックトレースを除外し、メインメッセージのみ表示
        if ! echo "$line" | grep -q "at \|\.cs:"; then
          echo "  ❌ $line"
        fi
      fi
    done
  fi

  # 警告メッセージを抽出
  WARNING_MESSAGES=$(grep -A1 "UnityEngine\.Debug:LogWarning" "$LOGFILE" | grep -v "UnityEngine\.Debug:LogWarning" | grep -v "^--$" | grep -v "^\s*$")
  if [ -n "$WARNING_MESSAGES" ]; then
    echo "$WARNING_MESSAGES" | head -5 | while IFS= read -r line; do
      if [ -n "$line" ]; then
        if ! echo "$line" | grep -q "at \|\.cs:"; then
          echo "  ⚠️  $line"
        fi
      fi
    done
  fi

  # Exception を表示
  if grep -q "Exception:" "$LOGFILE"; then
    grep "Exception:" "$LOGFILE" | head -5 | while IFS= read -r line; do
      echo "  ❌ $line"
    done
  fi

  # メッセージが見つからない場合
  if [ -z "$ERROR_MESSAGES" ] && [ -z "$WARNING_MESSAGES" ] && ! grep -q "Exception:" "$LOGFILE"; then
    echo "  (No console errors or warnings detected)"
  fi
  echo "────────────────────────────────────────"

  # その他のビルドエラーも表示
  echo ""
  echo "🔴 Build errors:"
  echo ""
  grep -E "(BuildFailedException|Error building Player|Build failed|Cannot recognize file type|Failed to build)" "$LOGFILE" | head -10 | sed 's/^/    /'

  echo ""
  echo "💡 For full details, check the log file: $LOGFILE"
fi

###############################################################################
# クリーンアップと終了
###############################################################################
echo ""
echo "────────────────────────────────────────"

if [ $RET -eq 0 ]; then
  echo "✅ Build process completed successfully"
  # 成功時はログファイルを削除
  rm -f "$LOGFILE"
else
  echo "❌ Build process failed"
  echo "📝 Log file preserved for debugging: $LOGFILE"
fi

exit $RET
