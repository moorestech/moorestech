---
name: master-refine
description: |
  moorestech mod のマスターデータを整える統合スキル。3つの操作を持つ。
  (A) items.json/blocks.json の sortPriority を research.json の解放順序 + nodeGraph 配置から再計算し、配列も並べ替える。
  (B) research.json の各ノードの UIPosition を依存関係と nodeGraph 配置から再計算して更新する。
  (C) moorestech_master 内の PNG アセット画像を長辺500pxの JPEG に変換しフォーマットを統一する。
  操作 A/B は nodeGraph.v1.json のビジュアル配置を基準に計算する。「マスターデータを整えて」のような包括的な指示では操作 A・B・C をすべて実行し (C の変換スクリプトは PNG が 1 件も無ければ何もしない冪等動作)、対象 mod は v8 (server_v8/moorestechAlphaMod_8) とする。ユーザーが特定操作・別バージョンを明示した場合のみそれに従う。
  Use When —
  - 「sortPriority を再計算して」「アイテム並び順を整えて」「研究解放順でアイテム並べて」「v7/v8 mod のソート優先度を更新」
  - 「research.json の UIPosition を整理・再配置して」「nodeGraph の配置に合わせて研究ノードの座標を更新して」「新しい研究ノードの位置を自動配置して」
  - 「マスターデータを nodeGraph に合わせて整えて」
  - 「PNG を JPEG に変換して」「画像フォーマットを統一して」「アセット画像を揃えて」「新しいアセット画像を追加したのでフォーマットを統一」
---

# Master Refine

## 目的

moorestech mod のマスターデータを整える。独立した3操作を提供する:

- **操作 A — sortPriority 再計算**: `items.json` / `blocks.json` の `sortPriority` を、`research.json` の解放チェーン (`prevResearchNodeGuids`) と `nodeGraph` 配置から再計算し、両ファイルの配列順も並べ替えて書き戻す。
- **操作 B — research ノード座標の再配置**: `research.json` の各ノードの `graphViewSettings.UIPosition` を、依存関係グラフと `nodeGraph` 配置から再計算して更新する。
- **操作 C — アセット画像フォーマット統一**: `moorestech_master` 内の PNG 画像を長辺500pxの JPEG に変換し、元ファイルを削除してフォーマットを統一する。

操作 A / B は `nodeGraph.v1.json` 上のビジュアル配置に基づいて計算する。操作 C は画像変換専用で、A/B とはデータ系統が独立している。

### デフォルト実行

「マスターデータを整えて」のような包括的な指示では A・B・C をすべて実行する（順不同。C は PNG が無ければ no-op なので毎回走らせ、新規 PNG の取りこぼしを防ぐ）。対象 mod は特別な指示がない限り v8（`server_v8/mods/moorestechAlphaMod_8`）。`v7` 等が明示されたら `vN` と `_N` の両方をそのバージョンに揃える。

## 共通前提

- 対象 mod に `master/research.json`, `master/items.json`, `master/blocks.json`, `.mooreseditor/nodeGraph.v1.json` がすべて揃っていること
- mod ディレクトリのパスは原則 `../moorestech_master/server_vN/mods/moorestechAlphaMod_N` (`N` はバージョン)
- 作業ディレクトリは `moorestech` リポジトリのルート (`pwd` で確認) を想定。本 SKILL のコマンド例はそこからの相対パス
- **nodeGraph 照合キーは `nodes[].masterGuid`** (`id` や `guid` ではない)。research.json の `researchNodeGuid` / items.json の `itemGuid` と一致する。`type: "note"` 等 `masterGuid` を持たないノードはスキップする

### 状態確認 (両操作共通)

```bash
git -C ../moorestech_master status -s server_vN/mods/moorestechAlphaMod_N/
```

`research.json` や `nodeGraph.v1.json` がワーキングツリーで更新されている場合、その状態で計算する。書き戻し対象 (操作 A なら items/blocks、操作 B なら research.json) に既存の未コミット変更がある場合は事前にユーザーに確認する。

---

## 操作 A — sortPriority 再計算

### 優先度ルール

最終的な sortPriority は `100` から `10` 刻みで以下の順に割り当てる:

1. **initialUnlocked アイテム** を先頭固定 (現状の sortPriority 昇順を維持)
2. **research 解放対象アイテム**
   - research の解放 depth (`prevResearchNodeGuids` を辿った最長パス) が小さい順
   - 同 depth 内: nodeGraph の y で行クラスタリング → **上の行 (y 小) が先**、行内は **x 昇順**
   - 各 research 内: nodeGraph の x で列クラスタリング → **左の列 (x 小) が先**、列内は **y 昇順 (上→下)**
3. **孤立アイテム** (どの research でも解放されず、`initialUnlocked` でもないもの) を末尾に、現状 sortPriority の相対順序を維持して配置

クラスタリング閾値はどちらも `100` (nodeGraph 座標単位)。差が閾値以下なら同じ行 / 列とみなす。閾値はゲーム本体の歴代座標に合わせて 100 に決め打ちしてある。

### 手順

1. 研究で解放されず `initialUnlocked` でも nodeGraph 登録も無い孤立アイテムは既定で末尾。別扱いが要るならスクリプト実行前にユーザーに確認する
2. `moorestech` リポジトリのルートから実行する（`--quiet` で最終順序の一覧を省略）:

```bash
python3 .claude/skills/master-refine/scripts/recalc_sort_priority.py \
  --mod-dir "$(cd ../moorestech_master/server_vN/mods/moorestechAlphaMod_N && pwd)"
```

3. `git -C ../moorestech_master diff --stat` で items/blocks だけが変わったことを確認する。同一 research 内の並びが崩れていたら、閾値ではなく nodeGraph 上の配置をまず疑う

---

## 操作 B — research ノード座標の再配置

`moorestech` リポジトリのルートから:

```bash
python3 .claude/skills/master-refine/scripts/recalc_research_positions.py \
  --mod-dir "$(cd ../moorestech_master/server_vN/mods/moorestechAlphaMod_N && pwd)"
```

配置アルゴリズム（depth・グループ判定・next_x 方式・孤立ノードのオフセット・重複検証）はスクリプト冒頭の docstring が正本。実行後は `git diff -U0 .../research.json` で UIPosition の数値行以外が変化していないことを確認する。

レイアウトの要点:
- 依存元は左・依存先は右。ルートノードは x=0、メインチェーン（最長依存パス）は y=0
- X は `depth * X_SPACING` ではなく next_x 方式。同 depth の分岐が横に展開されても後続のメインチェーンは必ずその右に来る
- `X_SPACING = 170`（旧500。研究ツリーUI上で間延びしたため1/3に短縮 — 2026-07-22 裁定）

---

## 操作 C — アセット画像フォーマット統一

`moorestech_master` リポジトリ内のアセット画像はすべて JPEG (`.jpeg`) で統一する。PNG ファイルが混在していた場合、`sips` コマンド (macOS 専用) で変換し元ファイルを削除する。操作 A / B とはデータ系統が独立しており、nodeGraph には依存しない。

### フォーマット仕様

「他の jpeg と同じフォーマットで圧縮」とは、既存アセットの規格に合わせることを指す。

- **フォーマット**: JPEG (`.jpeg`)。`sips -s format jpeg` で変換する
- **解像度**: 長辺 **500px**。`sips -Z 500` でアスペクト比を保ったまま縮小する (正方形素材なら 500×500)。アイテム画像の既存アセットはほぼ全て 500×500
- **品質**: `sips` のデフォルト品質をそのまま使う (既存アセットは概ね 26〜80KB に収まる)。品質オプションは指定しない
- 変換元 PNG は概ね 1000px超・1〜2MB あるため、必ず縮小工程を通して肥大化を防ぐ

変換前に既存 JPEG の標準寸法を確認しておくとよい:

```bash
# 既存jpegの寸法分布（500 が標準のはず）
cd "$TARGET_DIR/assets/item"
for f in *.jpeg; do sips -g pixelWidth "$f" 2>/dev/null | awk '/pixelWidth/{print $2}'; done | sort -n | uniq -c
```

### Step C-1. 変換スクリプトを実行

`moorestech` リポジトリのルートから:

```bash
bash .claude/skills/master-refine/scripts/convert_png_to_jpeg.sh [target_directory]
```

引数省略時は `../moorestech_master` を対象とする。スクリプトは PNG を再帰検索し、長辺500pxへ縮小しつつ JPEG 化して元 PNG を削除する。

個別ファイルを手動変換する場合:

```bash
sips -s format jpeg -Z 500 "input.png" --out "output.jpeg"   # -Z はアスペクト比保持
rm "input.png"
```

### Step C-2. 変換後の確認

- JSON 等で `.png` 拡張子の参照が残っていないか `grep` で確認し、残っていれば `.jpeg` に更新する
- 変換後ファイルが長辺500px (正方形なら 500×500) になっているか確認する

```bash
sips -g pixelWidth -g pixelHeight "output.jpeg"
```

---

## 共通 Gotchas

- **解放アクションは `unlockItemRecipeView` のみ** (操作 A): `giveItem` 等の他の `gameActionType` はアイテム解放扱いしない (実機でレシピ可視化と直結しないため)。
- **`blocks.json` の sortPriority は item と連動** (操作 A): 各 block の sortPriority は紐づく item (`itemGuid`) の値を再代入する。block 側だけで独立した値を持たせない。
- **行判定の境界** (操作 A): 差がちょうど 100 は「同じ行」、100 を超えると行が分かれる。レイアウト変更で y が境界付近に来たら並びが跳ぶ。
- **マスタファイル末尾の改行**: 既存マスタファイル群は `}` で終わっており改行なし。スクリプトで書き戻すときに `\n` を足さない。
- **JSON書き戻し前に無変更往復**: 操作A/Bの原文を実際の設定で往復し、完全一致を確認。不一致ならindent・エスケープ・末尾改行を合わせる。
- **変更対象外の保持を検証**: `object_pairs_hook` で重複キーを検出→書き戻し停止、キーとファイルを報告。前後を `parse_float=Decimal` で読み、対象プロパティ・配列順だけ除外して照合。
- **再実行は冪等**: 入力 (research.json, nodeGraph.v1.json) が同じなら何度実行しても同じ結果。
- **`--mod-dir` は絶対パス**: `..` を含む相対パスはシェルの `cd` 後に意味が変わる。

## Available scripts

- `scripts/recalc_sort_priority.py` (操作 A) — sortPriority の再計算と items/blocks の並べ替え
- `scripts/recalc_research_positions.py` (操作 B) — research.json の UIPosition 再計算と書き戻し
- `scripts/convert_png_to_jpeg.sh` (操作 C) — PNG を長辺500pxの JPEG へ変換し元 PNG を削除（引数省略時 `../moorestech_master`）
