---
name: master-refine
description: |
  moorestech mod のマスターデータを nodeGraph.v1.json のビジュアル配置に基づいて整える統合スキル。2つの操作を持つ。
  (A) items.json/blocks.json の sortPriority を research.json の解放順序 + nodeGraph 配置から再計算し、配列も並べ替える。
  (B) research.json の各ノードの UIPosition を依存関係と nodeGraph 配置から再計算して更新する。
  Use When —
  - 「sortPriority を再計算して」「アイテム並び順を整えて」「研究解放順でアイテム並べて」「v7/v8 mod のソート優先度を更新」
  - 「research.json の UIPosition を整理・再配置して」「nodeGraph の配置に合わせて研究ノードの座標を更新して」「新しい研究ノードの位置を自動配置して」
  - 「マスターデータを nodeGraph に合わせて整えて」
---

# Master Refine

## 目的

moorestech mod のマスターデータを `nodeGraph.v1.json` 上のビジュアル配置に基づいて整える。独立した2操作を提供する:

- **操作 A — sortPriority 再計算**: `items.json` / `blocks.json` の `sortPriority` を、`research.json` の解放チェーン (`prevResearchNodeGuids`) と `nodeGraph` 配置から再計算し、両ファイルの配列順も並べ替えて書き戻す。
- **操作 B — research ノード座標の再配置**: `research.json` の各ノードの `graphViewSettings.UIPosition` を、依存関係グラフと `nodeGraph` 配置から再計算して更新する。

2操作は独立して実行できる (A は research.json の UIPosition を読まず、nodeGraph 座標を直接参照する)。両方やる場合は順不同。

## 共通前提

- 対象 mod に `master/research.json`, `master/items.json`, `master/blocks.json`, `.mooreseditor/nodeGraph.v1.json` がすべて揃っていること
- mod ディレクトリのパスは原則 `../moorestech_master/server_vN/mods/moorestechAlphaMod_N` (`N` はバージョン)
- 作業ディレクトリは `moorestech` リポジトリのルート (`pwd` で確認) を想定。本 SKILL のコマンド例はそこからの相対パス
- **nodeGraph 照合キーは `nodes[].masterGuid`** (`id` や `guid` ではない)。research.json の `researchNodeGuid` / items.json の `itemGuid` と一致する。`type: "note"` 等 `masterGuid` を持たないノードはスキップする

### mod バージョンの確定

ユーザー指示から mod バージョンを抽出する (例「v8 mod」→ `server_v8/mods/moorestechAlphaMod_8`)。`vN` と `_N` が両方バージョン依存なので、ユーザーが `v7 mod` と言ったら両方とも `7` に揃える。`moorestech_master` リポジトリのパスを `pwd` で確認した上で組み立てる。

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

#### Step A-1. 並び替え対象外アイテムの確認

研究で解放されず、`initialUnlocked` でも無く、nodeGraph にも登録されていない「孤立アイテム」が存在する場合は、`末尾に既存 sortPriority 順で配置` が既定。違う扱い (個別 research 紐付け等) が必要であれば、スクリプト実行前にユーザーに確認する。

#### Step A-2. 再計算スクリプトを実行

`moorestech` リポジトリのルートから:

```bash
python3 .claude/skills/master-refine/scripts/recalc_sort_priority.py \
  --mod-dir "$(cd ../moorestech_master/server_vN/mods/moorestechAlphaMod_N && pwd)"
```

`--mod-dir` は絶対パスを期待する (相対パスでも動くが、シェル展開の混乱を避けるため `pwd` で絶対化する)。

オプション:
- `--quiet`: 最終順序の一覧出力を省略

#### Step A-3. 差分の最終確認

```bash
git -C ../moorestech_master diff --stat server_vN/mods/moorestechAlphaMod_N/master/{items,blocks}.json
tail -c 1 ../moorestech_master/server_vN/mods/moorestechAlphaMod_N/master/items.json | xxd | head -1
```

`items.json` / `blocks.json` の末尾が `}` (= `0x7d`、改行なし) になっていることを確認する。`0x0a` (改行) になっていたら write 時の `+ "\n"` の混入を疑う。

`--quiet` を外した出力には depth ごと・research ごとの最終並びが出る。以下は同一 research 内の並びが昇順になっているか目視確認する際の参考チェーン (スクリプトは自動検証しない)。崩れている場合は閾値ではなく **nodeGraph 上の配置** をまず疑うこと。

- `鉄鉱石 → 鉄鉱石の粉 → 大きな木の歯車 → 鉄インゴット`
- `鉄板 → 鉄のロッド → 鉄のワイヤー → 複合鉄板 → 鉄のフレーム`
- `鉄の歯車ベルトコンベア → 上り → 下り → 分岐機`
- `銅のワイヤー → 銅板 → 電柱 → 回転発電機 → 電気炉`

---

## 操作 B — research ノード座標の再配置

`research.json` の各研究ノードの `graphViewSettings.UIPosition` を、依存関係グラフと nodeGraph のビジュアル配置に基づいて計算・更新する。現状この操作はインラインの Python 手順 (スクリプト無し) で行う。

### Step B-1. 依存グラフを research.json から構築

各ノードの `prevResearchNodeGuids` から依存関係グラフを構築する。

```python
# 各ノードの depth (依存チェーンの最長パス) を計算
def compute_depth(guid, nodes, memo={}):
    if guid in memo: return memo[guid]
    node = nodes[guid]
    if not node['prevResearchNodeGuids']:
        memo[guid] = 0
        return 0
    d = max(compute_depth(p, nodes, memo) for p in node['prevResearchNodeGuids']) + 1
    memo[guid] = d
    return d
```

### Step B-2. nodeGraph からビジュアルグループを判定

nodeGraph の `type: "research"` ノードの位置を読み取り、各研究ノードが「上の段 / メインチェーン / 下の段」のどのグループかを判定する (照合は `masterGuid`)。

判定基準 (nodeGraph の y 座標で分類):
- **y < -100**: 上の段
- **-100 <= y <= 150**: メインチェーン (nodeGraph ではメインチェーンの y は 0〜80 程度にばらつく)
- **y > 150**: 下の段

同一グループ内は nodeGraph の x 座標でソートし、左→右の順序を決定する。

### Step B-3. 座標を計算

レイアウトルール:
- **X 座標**: `next_x` 方式で深さ順に配置 (`depth * X_SPACING` ではない)
  - `X_SPACING = 500` (ノード幅約 300px + 余白 200px で重ならない距離)
  - 各 depth のノードを配置後、`next_x = そのdepthで使った最大x + X_SPACING` に更新
  - 次の depth は `next_x` から開始する
  - **これにより、同 depth の分岐ノードが横に展開されても、後続のメインチェーンは必ずその右側に配置される**
- **Y 座標**:
  - メインチェーン (1本の依存パスを辿る最長経路): y = 0
  - 分岐ノード: nodeGraph のグループ (上の段/下の段) に基づいて配置
  - 上の段: y = 200、下の段: y = -200
- **同一 depth・同一グループで複数ノードがある場合**: nodeGraph の x 座標順で左→右に配置。base_x = `next_x`、以降 `base_x + n * X_SPACING` (n=0,1,2...)
- **孤立ノード (依存なし・被依存なし)**: x=0, y=400, 800, ... と下方にオフセット
- **最低間隔**: 上下で 200 以上

### Step B-4. 検証してから適用

適用前にチェック:
1. 全ノードに位置が割り当てられているか
2. 位置の重複がないか

```python
pos_set = set()
for guid, pos in positions.items():
    key = tuple(pos)
    assert key not in pos_set, f"DUPLICATE: {pos}"
    pos_set.add(key)
```

### Step B-5. research.json へ書き戻し

各ノードの `graphViewSettings.UIPosition` を更新し、`json.dump(data, f, indent=2, ensure_ascii=False)` で書き出す。

### 操作 B のキー制約

- 依存元 (prevResearch) は左 (X 小)、依存先は右 (X 大)
- 依存関係がないルートノードは x=0
- メインチェーン (最長依存パス) は y=0
- 同一 depth・同一グループの複数ノードは横に展開 (X_SPACING 間隔)
- **分岐ノードの横展開後に、後続のメインチェーンが配置される (next_x 方式)。`depth * X_SPACING` は使わない**
- nodeGraph に存在しないノード (孤立) はルート位置にオフセット配置

---

## 共通 Gotchas

- **mod バージョンを混同しない**: `server_v8/mods/moorestechAlphaMod_8` のように `vN` と `_N` が両方バージョン依存。ユーザーが `v7 mod` と言ったら両方とも `7` に揃える。
- **nodeGraph 照合は `masterGuid`**: `id` や `guid` ではない。`type: "note"` のノードは `masterGuid` を持たずスキップする (スクリプト側で対応済み)。
- **解放アクションは `unlockItemRecipeView` のみ** (操作 A): `giveItem` 等の他の `gameActionType` はアイテム解放扱いしない (実機でレシピ可視化と直結しないため)。
- **`blocks.json` の sortPriority は item と連動** (操作 A): 現状 `sortPriority` を持つ block は 2 件のみで、それぞれ紐づく item の sortPriority に再代入する。block 側にだけ存在する sortPriority を独立で扱わない。
- **同 depth で複数 research がある場合の行判定** (操作 A): depth 14 のように y が `-360, -260, -260, 580, 620, 640, 640` のとき、`-360` と `-260` の差はちょうど 100 = 閾値ジャストで「同じ行」になる。差が 100 を超えると行が分かれるので、レイアウト変更時はこの境界に注意する。
- **クラスタリング前に y / x でソートする** (操作 A): `cluster_sorted` はソート済み入力を仮定している。行クラスタは y 昇順、列クラスタは x 昇順で渡すこと。
- **マスタファイル末尾の改行**: 既存マスタファイル群は `}` で終わっており改行なし。`write_text` で `+ "\n"` を足すと git diff に余計な行が出るので付けない。
- **`prev` が空の research が複数あるケース**: depth=0 が複数あっても問題ないが、孤立 root が増えると視認しにくくなる。depth ごとの一覧を `--quiet` を外して出力すると確認できる。
- **再実行は冪等**: 入力 (research.json, nodeGraph.v1.json) が同じなら何度実行しても同じ結果。ただし入力側を編集した後の再実行は順序が変わる可能性があるので、事前に `git diff` で確認。
- **絶対パスを使う**: スクリプトは `cwd` 依存しないが、シェル引数で `..` を含む相対パスを使うと `cd` 後に意味が変わる。`--mod-dir` には絶対パスを渡す。

## Available scripts

- `scripts/recalc_sort_priority.py` (操作 A) — sortPriority の再計算と items/blocks の並べ替え。実行: `python3 scripts/recalc_sort_priority.py --mod-dir <mod-root> [--quiet]`
