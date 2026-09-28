---
name: recipe-chain-machine-count
description: Use when the user wants a moorestech item produced at a target throughput — whether they ask "how many machines" explicitly OR just state a desired production rate (per minute or per second) without mentioning machine count. The rate may be phrased per second (個/秒・毎秒N個) — convert to per minute (×60) before computing. Triggers even when the user only says they "want to be able to produce N/sec" (「〜を毎秒N個作れるようにしたい」「〜を生産できるようにしたい」) or names a specific machine type to build it on (「電気機械で〜を作りたい」「歯車機械で量産したい」), because the answer is still the machine-count breakdown. Examples: "鉄のフレームを分間5個作るには何台必要?", "X個/分作るとき機械いくつ?", "電気機械で毎秒50個の鉄板を生産できるようにしたい", "鉄板を秒間20個ラインで流したい". Walks the recipe DAG from the target item all the way down to raw ores/logs, computes per-step machine counts at base RPM / rated electric power, deduplicates shared intermediates, and outputs a hierarchical tree plus per-machine-type totals and raw-material throughput. Triggers on phrases like 「機械何個」「何台必要」「ライン設計」「中間素材含めて」「分間N個作りたい」「毎秒N個」「秒間N個」「生産できるようにしたい」「量産したい」.
---

# recipe-chain-machine-count

目標アイテム名と「分あたりの目標生産量」から、moorestech の v8 mod のレシピDAGを再帰展開し、各工程の機械台数・原料投入量を算出して階層ツリーで提示する。

## 前提条件

- マスタデータの場所: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/`
  - 見つからない場合は `ls ../moorestech_master/` で最新の `server_v*` を確認し、その中の `mods/<mod名>/master/` を使う
  - `items.json` — itemGuid と name の対応
  - `machineRecipes.json` — `time` (秒), `inputItems[]`, `outputItems[]`, `blockGuid`
  - `blocks.json` — blockGuid と name, blockType, gearConsumption.baseRpm 等
  - `fluids.json` — fluidGuid と name の対応（液体レシピで参照）
- 機械稼働の前提:
  - **GearMachine は `gearConsumption.baseRpm` で定格動作**することを仮定（機械ごとに異なるので blocks.json から読む。動力不足でRPMが下がるとレシピ時間が伸びる）
  - **ElectricMachine は `requiredPower` を満たして定格動作**することを仮定。`requiredPower: 0` の機械（石窯など）は電力供給不要

## 手順

### Step 1. Python でレシピDBを組み、DAG を展開する

手計算・grep での手繰りはしない。Python で master の JSON を読み、次を組み立てて計算する：

- `items.json` / `fluids.json` から名前 ↔ Guid の対応
- `machineRecipes.json` から「出力 itemGuid → レシピ（`time`・出力 count・`inputItems[]`・`blockGuid`）」。入力側に登場するだけのレシピはそのアイテムの作成レシピではない
- `blocks.json` から blockGuid → `name`・`blockType`（hex で判断しない。同系列でも tier 違いで `time` の違うレシピが別登録されている）

計算規則：

- 1台あたり/分 = `60 / time × outputCount`、必要台数 = `ceil(需要/分 ÷ 1台あたり/分)`、子の需要/分 = 入力 count × (親の需要/分 ÷ 親の outputCount)
- 作成レシピが複数あれば、ユーザーが機械を指定していない限り最も基本的な機械（原始的な〜系）を採る
- `isRemain: true` の入力（鋳型など）は消費されないので需要にも再帰にも入れない。未指定は false（消費される）
- 作成レシピが無いアイテムは採取系の生原料。採掘機（GearMiner / ElectricMiner の `mineSettings[].time`、`60 / time` 個/分）の台数まで出す
- 液体（`inputFluids` / `outputFluids`）が絡む場合は液体源（採掘・蒸気源・抽出機）まで遡る

DFS は2回走らせる：

1. **完全展開DFS**: 同じ中間素材でも消費先ごとに独立したサブツリーとして展開し、各ノードの台数を機械種別合計に加算する
2. **合算DFS**: 各アイテムの需要を全消費先で合計してから台数を計算する
3. 両者の機械合計の差は端数切り上げの累積として出力で明示する

### Step 2. 出力（**4パート固定構成**）

以下の4パートを必ずこの順序で出力する。1つでも欠けてはいけない：

#### パート①: 完全展開ツリー（合算なし）

罫線（`├─ └─ │`）で階層を表現する。同じ素材が複数経路で消費される場合も**合算せず**、各経路で独立に展開する。フォーマット：

```
<目標アイテム> <需要/分>/分 (<機械名> <time>s/<output>, <1台/分>/分) → <台数>台
├─ <子アイテム> <需要/分>/分 (<機械名> <time>s/<output>, <1台/分>/分) → <台数>台
│   └─ ...
│       └─ <生原料> <需要/分>/分 (採掘機 <time>s/<output>, <1台/分>/分) → 採掘機<台数>台
│       └─ ※<isRemain素材>×<count> isRemain × <親機械の台数>台
└─ ...
```

#### パート②: 機械種別合計（ツリー展開ベース）

表形式：

| 機械 | 種別 | 台数 |
|---|---|---|
| <機械名> | <GearMachine/ElectricMachine/GearMiner/...> | **N台** |
| ... | ... | ... |
| **総計** | — | **N台** |

#### パート③: アイテム種ごとの合計必要機械数（全消費先合算ベース）

**列構成は固定**。順序は依存順（最終目標 → 中間素材 → 生原料）。**生原料行は除外**（生原料はパート④に分離）：

| アイテム | 機械名 | 台数 | 需要/分 |
|---|---|---|---|
| <アイテム名> | <機械名> | N台 | <需要> |
| ... | ... | ... | ... |

#### パート④: 原材料の種類と採掘機要求数（採取系のみ）

採取系の生原料（鉄鉱石・原木・青銅の鉱石など、レシピで作れず採掘で得るもの）を**必ず全列挙**する。**列構成は固定**：

| 原材料 | 採掘機 | 台数 | 需要/分 |
|---|---|---|---|
| <原材料名> | <採掘機名> | N台 | <需要> |
| ... | ... | ... | ... |

需要はチェーン全体での合算量（ツリーで分離した複数経路を全て足した値）。1原材料につき1行。

#### 末尾の注意書き

- `isRemain: true` のツールは N 台分セットが必要
- GearMachine は baseRpm 定格動作前提。歯車動力でRPMが下がると台数は実質増える
- パート①とパート③で機械合計が一致しない場合、その差分はライン分離による端数切り上げ累積であることを明記

## Gotchas

### 「最終工程の機械数だけ」ではユーザーの期待を満たさない
最初の質問が単に「N個作るには何台?」でも、**全ての中間素材を含めた合計を聞いている可能性が高い**。最終工程だけを答えると「違う、中間素材も含めて」と差し戻される。**初回から全展開で答える**こと。

### ツリーと表の使い分け
- **パート①完全展開ツリー**: `├─ └─ │` の罫線で表現。フラット表で代用してはいけない（親子関係が消える）
- **パート②機械種別合計 / パート③アイテム種ごとの合計 / パート④原材料・採掘機**: 表形式で出す。罫線ツリーで代用してはいけない（一覧性が消える）
- パート③の表は **必ず4列固定**: `| アイテム | 機械名 | 台数 | 需要/分 |`。生原料は含めない（パート④に分離）
- パート④の表も **必ず4列固定**: `| 原材料 | 採掘機 | 台数 | 需要/分 |`。採取系のみを全列挙、1原材料1行
- 列の順序や名称を勝手に変えない

### `isRemain: true` の扱い
`isRemain: true` の入力アイテム（鋳型など）はクラフトのたびに消費されず、機械にセットされ続ける。需要には**カウントしない**が、「機械N台分のツールを最初にセットせよ」という注意書きを最後に出す。

### 同じ中間素材が複数経路から要求される時のビュー差
パート①完全展開ツリーは**合算せず分離**、パート③合計表は**合算する**。両方を出すことが必須で、片方だけにしない。
例: 鉄インゴットが鉄板用（20/分）と鉄ロッド用（8/分）に分かれる場合、ツリーでは「鉄板配下に鉄インゴ20/分→石窯7台」「鉄ロッド配下に鉄インゴ8/分→石窯3台」と独立展開し、合計表では「鉄インゴット 28/分 → 石窯10台」と1行に集約する。
ツリー展開と合算で機械合計が一致しないことがある（端数切り上げ累積）。これは仕様で、必ず注記する。

### baseRpm 前提の明記
GearMachine の計算は「定格RPMで動いている」前提。ユーザーが歯車動力に余裕がない場合、実際の台数は増える。回答末尾で必ず「歯車動力でRPMが下がるとGearMachineの台数は増える」と注意する。
