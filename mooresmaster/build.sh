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

# RoslynAnalyzer用の.metaファイルを生成する関数
# Generate .meta file for RoslynAnalyzer
generate_meta() {
    local guid=$1
    local output_path=$2
    cat > "$output_path" << 'METAEOF'
fileFormatVersion: 2
guid: GUID_PLACEHOLDER
labels:
- RoslynAnalyzer
PluginImporter:
  externalObjects: {}
  serializedVersion: 3
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
    Any:
      enabled: 0
      settings:
        Exclude Android: 1
        Exclude CloudRendering: 1
        Exclude EmbeddedLinux: 1
        Exclude GameCoreScarlett: 1
        Exclude GameCoreXboxOne: 1
        Exclude Linux64: 1
        Exclude OSXUniversal: 1
        Exclude PS4: 1
        Exclude PS5: 1
        Exclude QNX: 1
        Exclude Switch: 1
        Exclude VisionOS: 1
        Exclude WebGL: 1
        Exclude Win: 1
        Exclude Win64: 1
        Exclude WindowsStoreApps: 1
        Exclude XboxOne: 1
        Exclude iOS: 1
        Exclude tvOS: 1
    Editor:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  userData:
  assetBundleName:
  assetBundleVariant:
METAEOF
    sed -i '' "s/GUID_PLACEHOLDER/$guid/" "$output_path"
}

echo "Copying DLL to Unity projects..."
cp "$DLL_PATH" "$ROOT_DIR/moorestech_client/Assets/Plugins/"
cp "$DLL_PATH" "$ROOT_DIR/moorestech_server/Assets/Plugins/"

echo "Generating .meta files..."
generate_meta "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6" "$ROOT_DIR/moorestech_client/Assets/Plugins/mooresmaster.Generator.dll.meta"
generate_meta "d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a1" "$ROOT_DIR/moorestech_server/Assets/Plugins/mooresmaster.Generator.dll.meta"

echo "Done! mooresmaster.Generator.dll has been deployed."
