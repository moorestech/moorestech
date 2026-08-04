#!/bin/bash
# mooresmaster のGeneratorと共通DLLをUnityプロジェクトに配置
# Deploy the mooresmaster generator and shared DLL to Unity projects

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

echo "Building mooresmaster.LocalizationCsv..."
dotnet build "$SCRIPT_DIR/mooresmaster.LocalizationCsv" -c Release

echo "Building mooresmaster.Generator..."
dotnet build "$SCRIPT_DIR/mooresmaster.Generator" -c Release

GENERATOR_DLL_PATH="$SCRIPT_DIR/mooresmaster.Generator/bin/Release/netstandard2.0/mooresmaster.Generator.dll"
LOCALIZATION_DLL_PATH="$SCRIPT_DIR/mooresmaster.LocalizationCsv/bin/Release/netstandard2.0/mooresmaster.LocalizationCsv.dll"

if [ ! -f "$GENERATOR_DLL_PATH" ]; then
    echo "Error: DLL not found at $GENERATOR_DLL_PATH"
    exit 1
fi

if [ ! -f "$LOCALIZATION_DLL_PATH" ]; then
    echo "Error: DLL not found at $LOCALIZATION_DLL_PATH"
    exit 1
fi

echo "Copying DLL to Unity projects..."
cp "$GENERATOR_DLL_PATH" "$ROOT_DIR/moorestech_client/Assets/Plugins/"
cp "$GENERATOR_DLL_PATH" "$ROOT_DIR/moorestech_server/Assets/Plugins/"
cp "$LOCALIZATION_DLL_PATH" "$ROOT_DIR/moorestech_client/Assets/Plugins/"
cp "$LOCALIZATION_DLL_PATH" "$ROOT_DIR/moorestech_server/Assets/Plugins/"

echo "Done! mooresmaster DLLs have been deployed."
