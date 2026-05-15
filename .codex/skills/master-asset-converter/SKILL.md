---
name: master-asset-converter
description: moorestech_masterリポジトリ内のPNGファイルを見つけてJPEGに変換し、アセット画像フォーマットを統一する。Use when: (1) moorestech_masterにPNG画像が混在している時 (2) 「PNGをJPEGに変換して」「画像フォーマットを統一して」と依頼された時 (3) 新しいアセット画像を追加した後にフォーマットを揃えたい時
---

# Master Asset Converter

moorestech_masterリポジトリ内のアセット画像はすべてJPEG(.jpeg)で統一する。PNGファイルが混在していた場合、`sips`コマンド（macOS専用）で変換し元ファイルを削除する。

## 手順

1. moorestech_masterリポジトリ内のPNGファイルを検索する
2. 変換スクリプトを実行、またはsipsで手動変換する
3. JSON等でファイル名参照がある場合は拡張子を更新する

## スクリプト実行

```bash
bash scripts/convert_png_to_jpeg.sh [target_directory]
```

引数省略時は `../moorestech_master` を対象とする。

## 手動変換（個別ファイル）

```bash
sips -s format jpeg "input.png" --out "output.jpeg"
rm "input.png"
```

## 変換後の確認

- JSON等で `.png` 拡張子の参照が残っていないか `grep` で確認する
- 残っていれば `.jpeg` に更新する
