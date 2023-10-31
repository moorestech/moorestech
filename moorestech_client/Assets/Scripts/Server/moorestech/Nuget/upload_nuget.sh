#!/bin/bash

# .csprojファイルのパスを取得
CS_PROJ_FILE="../Server.Starter/Server.Starter.csproj"
NUSPEC_FILE="./ServerMod.nuspec"

echo "Building project..."
# プロジェクトをビルドして出力されたdllファイルを取得
BUILD_OUTPUT=$(dotnet build "$CS_PROJ_FILE" --configuration Release --output ./bin/release)
DLL_FILES=$(find $BUILD_OUTPUT -name "*.dll" -type f)

# nugetパッケージを作成するために必要な情報を取得
PACKAGE_ID=$(grep "<PackageId>" "$CS_PROJ_FILE" | sed -e "s/.*<PackageId>//" -e "s/<\/PackageId>.*//")
VERSION=$(grep "<version>" "$NUSPEC_FILE" | sed -e "s/.*<version>//" -e "s/<\/version>.*//")

echo "Creating nuget package..."
# nugetパッケージを作成
nuget pack $NUSPEC_FILE -BasePath ./bin/release

exit


echo "Uploading nuget package..."
# nugetパッケージをアップロード
nuget push "$PACKAGE_ID.$VERSION.nupkg" $NUGET_TOKEN -Source https://api.nuget.org/v3/index.json

echo "Cleaning up temporary files..."
# 一時ファイルを削除
rm -rf $BUILD_OUTPUT
rm "$PACKAGE_ID.$VERSION.nupkg"

echo "Done."
