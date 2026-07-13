---
name: registering-heightmaps
description: |
  指定フォルダの画像をMicroVerse ContentCollection .assetのcontents配列にスタンプとして一括登録する。

  Use When:
  - MicroVerseのContent Browserにスタンプを追加したい
  - ハイトマップ画像をContentCollectionに一括登録したい
  - 新しい画像フォルダをMicroVerseプリセットとして登録したい
---

# registering-heightmaps

指定フォルダ配下の画像ファイル（png, jpg, jpeg, tga, exr, tif, tiff）をMicroVerse ContentCollection .assetファイルのcontents配列にスタンプとして一括登録する。

## 引数

ユーザーから以下の2つの情報を取得する：

1. **画像フォルダパス**: 登録したい画像が含まれるフォルダ（Assetsからの相対パスまたは絶対パス）
2. **対象の.assetファイルパス**: 登録先のContentCollection .assetファイル

情報が不足している場合はAskUserQuestionで確認する。

## ワークフロー

### 1. 対象の.assetファイルを読み取る

Readツールで.assetファイルを読み込み、以下を確認する：
- `m_Script`のguidが`10ede25ccd6fd4e8e98a828e1f7c552d`（ContentCollection）であること
- `m_Name`, `packName`, `id`, `author`, `contentType`のメタ情報を記録する
- 既存の`contents`が空でない場合、上書きしてよいかユーザーに確認する

### 2. GUIDを一括取得してYAMLエントリを生成する

Bashツールで全画像の.metaファイルからGUIDを取得し、YAMLエントリを生成する。

```bash
cd "<プロジェクトルート>" && find "<画像フォルダ>" -type f \( -name "*.png.meta" -o -name "*.jpg.meta" -o -name "*.jpeg.meta" -o -name "*.tga.meta" -o -name "*.exr.meta" -o -name "*.tif.meta" -o -name "*.tiff.meta" \) -print0 | sort -z | while IFS= read -r -d '' meta; do
  guid=$(grep '^guid:' "$meta" | awk '{print $2}')
  if [ -z "$guid" ]; then
    echo "WARNING: No GUID found in $meta" >&2
    continue
  fi
  echo "  - prefab: {fileID: 0}"
  echo "    childPrefab: {fileID: 0}"
  echo "    previewImage: {fileID: 0}"
  echo "    previewAsset: {fileID: 0}"
  echo "    stamp: $guid"
  echo "    previewGradient: {fileID: 0}"
  echo "    description: "
done > /tmp/content_entries.yaml
```

**重要**: パスにスペースが含まれる場合があるため、`find ... -print0`と`read -d ''`を必ず使用する。

### 3. .assetファイルを組み立てる

元の.assetファイルのヘッダー情報（`m_Name`, `author`, `packName`, `id`, `contentType`等）を保持しつつ、contents配列を新しいエントリで置き換える。

```bash
cat > /tmp/asset_output.yaml << 'HEADER'
<ステップ1で読み取ったヘッダー部分をcontents:行まで出力>
HEADER
cat /tmp/content_entries.yaml >> /tmp/asset_output.yaml
echo "  systemConfig: {fileID: 0}" >> /tmp/asset_output.yaml
cp /tmp/asset_output.yaml "<対象.assetファイルパス>"
```

### 4. 結果を報告する

登録した画像の総数と、含まれるサブフォルダの一覧を報告する。

## 注意事項

- .metaファイルが存在しない画像は登録できない（Unityでインポート済みである必要がある）。
- contentType: 0 はHeight（ハイトマップ）を意味する。テクスチャの場合はcontentTypeが異なる可能性がある。
