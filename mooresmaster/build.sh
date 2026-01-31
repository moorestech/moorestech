#!/bin/bash
# mooresmaster.Generator をビルドして Unity プロジェクトに配置
# Build mooresmaster.Generator and deploy to Unity projects

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

echo "Building mooresmaster.Generator..."
dotnet build "$SCRIPT_DIR/mooresmaster.Generator" -c Release

DLL_PATH="$SCRIPT_DIR/mooresmaster.Generator/bin/Release/netstandard2.0/mooresmaster.Generator.dll"

if [ ! -f "$DLL_PATH" ]; then
    echo "Error: DLL not found at $DLL_PATH"
    exit 1
fi

echo "Copying DLL to Unity projects..."
cp "$DLL_PATH" "$ROOT_DIR/moorestech_client/Assets/Packages/mooresmaster.Generator.local/analyzers/dotnet/cs/"
cp "$DLL_PATH" "$ROOT_DIR/moorestech_server/Assets/Packages/mooresmaster.Generator.local/analyzers/dotnet/cs/"

echo "Done! mooresmaster.Generator.dll has been deployed."
