---
name: master-asset-converter
description: >-
  moorestech_masterリポジトリ内のPNGファイルを見つけてJPEGに変換し、アセット画像フォーマットを統一する。Use when: (1) moorestech_masterにPNG画像が混在している時 (2) 「PNGをJPEGに変換して」「画像フォーマットを統一して」と依頼された時 (3) 新しいアセット画像を追加した後にフォーマットを揃えたい時
---

# Master Asset Converter

moorestech_masterリポジトリ内のアセット画像はすべてJPEG(.jpeg)で統一する。PNGファイルが混在していた場合、`sips`コマンド（macOS専用）で変換し元ファイルを削除する。

## JPEGの加工方法（フォーマット仕様）

「他のjpegと同じフォーマットで圧縮」とは、既存アセットの規格に合わせることを指す。

- **フォーマット**: JPEG (`.jpeg`)。`sips -s format jpeg` で変換する
- **解像度**: 長辺 **500px**。`sips -Z 500` でアスペクト比を保ったまま縮小する（正方形素材なら 500×500）。アイテム画像の既存アセットはほぼ全て 500×500
- **品質**: `sips` のデフォルト品質をそのまま使う（既存アセットは概ね 26〜80KB に収まる）。品質オプションは指定しない
- 変換元PNGは概ね 1000px超・1〜2MB あるため、必ず縮小工程を通して肥大化を防ぐ

変換前に既存JPEGの標準寸法を確認しておくとよい:

```bash
# 既存jpegの寸法分布（500 が標準のはず）
cd "$TARGET_DIR/assets/item"
for f in *.jpeg; do sips -g pixelWidth "$f" 2>/dev/null | awk '/pixelWidth/{print $2}'; done | sort -n | uniq -c
```

## 手順

1. moorestech_masterリポジトリ内のPNGファイルを検索する
2. 変換スクリプトを実行、またはsipsで手動変換する（長辺500pxへ縮小しつつJPEG化）
3. JSON等でファイル名参照がある場合は拡張子を更新する

## スクリプト実行

```bash
bash scripts/convert_png_to_jpeg.sh [target_directory]
```

引数省略時は `../moorestech_master` を対象とする。

## 手動変換（個別ファイル）

```bash
# 長辺500pxへ縮小しつつJPEG化（-Z はアスペクト比保持）
sips -s format jpeg -Z 500 "input.png" --out "output.jpeg"
rm "input.png"
```

## 変換後の確認

- JSON等で `.png` 拡張子の参照が残っていないか `grep` で確認する
- 残っていれば `.jpeg` に更新する
- 変換後ファイルが 500×500（長辺500px）になっているか `sips -g pixelWidth -g pixelHeight` で確認する
