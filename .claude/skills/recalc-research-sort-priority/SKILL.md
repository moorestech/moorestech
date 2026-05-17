---
name: recalc-research-sort-priority
description: moorestech mod の items.json/blocks.json の sortPriority を、research.json の解放順序と nodeGraph.v1.json 上のアイテム配置から再計算し、配列も sortPriority 順に並べ替える。Use When — 「sortPriority を再計算して」「アイテム並び順を整えて」「研究解放順でアイテム並べて」「v7/v8 mod のソート優先度を更新」と言われた場合。
---

# Recalculate Research Sort Priority

## 目的

moorestech mod の `items.json` / `blocks.json` の `sortPriority` を、`research.json` の解放チェーン (`prevResearchNodeGuids`) と `nodeGraph.v1.json` 上のビジュアル配置に基づいて再計算する。さらに両ファイルの配列順も `sortPriority` 昇順 (blocks は紐づく item の sortPriority 順) に並び替えて書き戻す。

## 前提条件

- 対象 mod の `master/research.json`, `master/items.json`, `master/blocks.json`, `.mooreseditor/nodeGraph.v1.json` がすべて揃っていること
- `nodeGraph.v1.json` の `nodes[].masterGuid` が research.json / items.json の `*Guid` と一致していること (照合キー)
- mod ディレクトリのパスは原則 `../moorestech_master/server_vN/mods/moorestechAlphaMod_N` (`N` はバージョン)
- 作業ディレクトリは `moorestech` リポジトリのルート (`pwd` で確認) を想定。本 SKILL のコマンド例はそこからの相対パス

## 優先度ルール

最終的な sortPriority は `100` から `10` 刻みで以下の順に割り当てる:

1. **initialUnlocked アイテム** を先頭固定 (現状の sortPriority 昇順を維持)
2. **research 解放対象アイテム**
   - research の解放 depth (`prevResearchNodeGuids` を辿った最長パス) が小さい順
   - 同 depth 内: nodeGraph の y で行クラスタリング → **上の行 (y 小) が先**、行内は **x 昇順**
   - 各 research 内: nodeGraph の x で列クラスタリング → **左の列 (x 小) が先**、列内は **y 昇順 (上→下)**
3. **孤立アイテム** (どの research でも解放されず、`initialUnlocked` でもないもの) を末尾に、現状 sortPriority の相対順序を維持して配置

クラスタリング閾値はどちらも `100` (nodeGraph 座標単位)。差が閾値以下なら同じ行 / 列とみなす。

### 4 パターンによる検証ゲート

スクリプトは以下の 4 つを「同一 research 内の最終並び」として検証する。一つでも昇順にならなければ `SystemExit` で停止する。閾値変更時の回帰防止にも使う。

- `鉄鉱石 → 鉄鉱石の粉 → 大きな木の歯車 → 鉄インゴット`
- `鉄板 → 鉄のロッド → 鉄のワイヤー → 複合鉄板 → 鉄のフレーム`
- `鉄の歯車ベルトコンベア → 上り → 下り → 分岐機`
- `銅のワイヤー → 銅板 → 電柱 → 回転発電機 → 電気炉`

これらが OK にならない場合、まず X_COLUMN_THRESHOLD / Y_ROW_THRESHOLD ではなく **nodeGraph 上の配置** を疑うこと。閾値はゲーム本体の歴代座標に合わせて 100 に決め打ちしてある。

## 手順

### Step 1. mod ディレクトリを確定する

ユーザー指示から mod バージョンを抽出 (例「v8 mod」→ `server_v8/mods/moorestechAlphaMod_8`)。`moorestech_master` リポジトリのパスを `pwd` で確認した上で組み立てる。

### Step 2. 状態確認

```bash
git -C ../moorestech_master status -s server_vN/mods/moorestechAlphaMod_N/
```

`research.json` や `nodeGraph.v1.json` がワーキングツリーで更新されている場合、その状態で計算する。`items.json`/`blocks.json` に既存の未コミット変更がある場合は事前にユーザーに確認する。

### Step 3. 並び替え対象外アイテムの確認

研究で解放されず、`initialUnlocked` でも無く、nodeGraph にも登録されていない「孤立アイテム」が存在する場合は、`末尾に既存 sortPriority 順で配置` が既定。違う扱い (個別 research 紐付け等) が必要であれば、Step 4 を実行する前にユーザーに確認する。

### Step 4. 再計算スクリプトを実行

`moorestech` リポジトリのルートから:

```bash
python3 .claude/skills/recalc-research-sort-priority/scripts/recalc_sort_priority.py \
  --mod-dir "$(cd ../moorestech_master/server_vN/mods/moorestechAlphaMod_N && pwd)"
```

`--mod-dir` は絶対パスを期待する (相対パスでも動くが、シェル展開の混乱を避けるため `pwd` で絶対化する)。

オプション:
- `--no-validate`: 4 パターンの検証を省略 (パターン中のアイテム名を mod 側で変えた場合)
- `--quiet`: 最終順序の一覧出力を省略

### Step 5. 差分の最終確認

```bash
git -C ../moorestech_master diff --stat server_vN/mods/moorestechAlphaMod_N/master/{items,blocks}.json
tail -c 1 ../moorestech_master/server_vN/mods/moorestechAlphaMod_N/master/items.json | xxd | head -1
```

`items.json` / `blocks.json` の末尾が `}` (= `0x7d`、改行なし) になっていることを確認する。`0x0a` (改行) になっていたら write 時の `+ "\n"` の混入を疑う。

## Gotchas

- **mod バージョンを混同しない**: `server_v8/mods/moorestechAlphaMod_8` のように `vN` と `_N` が両方バージョン依存。`--mod-dir` 引数で一括指定する設計なのでミスマッチは起きにくいが、ユーザーが `v7 mod` と言ったら両方とも `7` に揃える。
- **nodeGraph 照合は `masterGuid`**: `id` や `guid` ではない。`type: "note"` のノードは `masterGuid` を持たずスキップする (スクリプト側で対応済み)。
- **同 depth で複数 research がある場合の行判定**: depth 14 のように y が `-360, -260, -260, 580, 620, 640, 640` のとき、`-360` と `-260` の差はちょうど 100 = 閾値ジャストで「同じ行」になる。差が 100 を超えると行が分かれるので、レイアウト変更時はこの境界に注意する。
- **クラスタリング前に y / x でソートする**: `cluster_sorted` はソート済み入力を仮定している。行クラスタは y 昇順、列クラスタは x 昇順で渡すこと。
- **`items.json` 末尾の改行**: 既存マスタファイル群は `}` で終わっており改行なし。`write_text` で `+ "\n"` を足すと git diff に余計な行が出るので付けない。
- **`blocks.json` の sortPriority は item と連動**: 現状 `sortPriority` を持つ block は 2 件のみで、それぞれ紐づく item の sortPriority に再代入する。block 側にだけ存在する sortPriority を独立で扱わない。
- **解放アクションは `unlockItemRecipeView` のみ**: `giveItem` 等の他の `gameActionType` はアイテム解放扱いしない (実機でレシピ可視化と直結しないため)。
- **`prev` が空の research が複数あるケース**: depth=0 が複数あっても問題ないが、孤立 root が増えると視認しにくくなる。depth ごとの一覧を `--quiet` を外して出力すると確認できる。
- **再実行は冪等**: 入力 (research.json, nodeGraph.v1.json) が同じなら何度実行しても同じ結果。ただし入力側を編集した後の再実行は順序が変わる可能性があるので、事前に `git diff` で確認。
- **絶対パスを使う**: スクリプトは `cwd` 依存しないが、シェル引数で `..` を含む相対パスを使うと `cd` 後に意味が変わる。`--mod-dir` には絶対パスを渡す。

## Available scripts

- `scripts/recalc_sort_priority.py` — sortPriority の再計算と items/blocks の並べ替え。実行: `python3 scripts/recalc_sort_priority.py --mod-dir <mod-root> [--no-validate] [--quiet]`
