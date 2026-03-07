---
name: research-node-position
description: |
  研究ノード(research.json)のUI座標をnodeGraph.v1.jsonの配置データに基づいて自動計算・更新するスキル。
  Use when:
  1. research.jsonのUIPositionを整理・再配置したい時
  2. nodeGraphの配置に合わせてresearch.jsonの座標を更新したい時
  3. 新しい研究ノードを追加した後に位置を自動配置したい時
---

# Research Node Position Layout

## Overview

research.jsonの各研究ノードの`graphViewSettings.UIPosition`を、依存関係グラフとnodeGraph.v1.jsonのビジュアル配置に基づいて計算・更新する。

## File Paths

- **research.json**: `moorestech_master/server_v7/mods/moorestechAlphaMod_7/master/research.json`
- **nodeGraph.v1.json**: `moorestech_master/server_v7/mods/moorestechAlphaMod_7/.mooreseditor/nodeGraph.v1.json`

## Procedure

### Step 1: Parse dependency graph from research.json

各ノードの`prevResearchNodeGuids`から依存関係グラフを構築する。

```python
# 各ノードのdepth(依存チェーンの最長パス)を計算
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

### Step 2: Read nodeGraph.v1.json for visual grouping

nodeGraphの`type: "research"`ノードの位置を読み取り、研究ノードが「上の段」「下の段」「メインチェーン」のどのグループに属するかを判定する。

判定基準 (nodeGraphのy座標で分類):
- **y < -100**: 上の段
- **-100 <= y <= 150**: メインチェーン (nodeGraphではメインチェーンのyは0〜80程度にばらつく)
- **y > 150**: 下の段

同一グループ内はnodeGraphのx座標でソートし、左→右の順序を決定する。

### Step 3: Calculate positions

レイアウトルール:
- **X座標**: `depth * X_SPACING`
  - `X_SPACING = 500` (ノード幅約300px + 余白200pxで重ならない距離)
- **Y座標**:
  - メインチェーン(1本の依存パスを辿る最長経路): y = 0
  - 分岐ノード: nodeGraphのグループ(上の段/下の段)に基づいて配置
  - 上の段: y = 200 (nodeGraphで上にあるノード)
  - 下の段: y = -200 (nodeGraphで下にあるノード)
- **同一depth・同一グループで複数ノードがある場合**:
  - nodeGraphのx座標順で左→右に配置
  - base_x = `depth * X_SPACING`、以降 `base_x + n * X_SPACING` (n=0,1,2...)
- **孤立ノード(依存なし・被依存なし)**: x=0, y=400, 800, ... と下方にオフセット
- **最低間隔**: 上下で200以上

### Step 4: Verify and apply

適用前にチェック:
1. 全ノードに位置が割り当てられているか
2. 位置の重複がないか

```python
# 重複チェック
pos_set = set()
for guid, pos in positions.items():
    key = tuple(pos)
    assert key not in pos_set, f"DUPLICATE: {pos}"
    pos_set.add(key)
```

### Step 5: Write back to research.json

各ノードの`graphViewSettings.UIPosition`を更新し、`json.dump(data, f, indent=2, ensure_ascii=False)`で書き出す。

## Key Constraints

- 依存元(prevResearch)は左(X小)、依存先は右(X大)
- 依存関係がないルートノードはx=0
- メインチェーン(最長依存パス)はy=0に配置
- 同一depthで同一グループの複数ノードは横に展開(X_SPACING間隔)
- nodeGraphに存在しないノード(孤立)はルート位置にオフセット配置
