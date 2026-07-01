---
name: recipe-chain-machine-count
description: Use when the user wants a moorestech item produced at a target throughput — whether they ask "how many machines" explicitly OR just state a desired production rate (per minute or per second) without mentioning machine count. The rate may be phrased per second (個/秒・毎秒N個) — convert to per minute (×60) before computing. Triggers even when the user only says they "want to be able to produce N/sec" (「〜を毎秒N個作れるようにしたい」「〜を生産できるようにしたい」) or names a specific machine type to build it on (「電気機械で〜を作りたい」「歯車機械で量産したい」), because the answer is still the machine-count breakdown. Examples: "鉄のフレームを分間5個作るには何台必要?", "X個/分作るとき機械いくつ?", "電気機械で毎秒50個の鉄板を生産できるようにしたい", "鉄板を秒間20個ラインで流したい". Walks the recipe DAG from the target item all the way down to raw ores/logs, computes per-step machine counts at base RPM / rated electric power, deduplicates shared intermediates, and outputs a hierarchical tree plus per-machine-type totals and raw-material throughput. Triggers on phrases like 「機械何個」「何台必要」「ライン設計」「中間素材含めて」「分間N個作りたい」「毎秒N個」「秒間N個」「生産できるようにしたい」「量産したい」.
---

# recipe-chain-machine-count

目標アイテム名と「分あたりの目標生産量」から、moorestech の v8 mod のレシピDAGを再帰展開し、各工程の機械台数・原料投入量を算出して階層ツリーで提示する。

## 前提条件

- マスタデータの場所: `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/`
  - 見つからない場合は `ls /Users/katsumi/moorestech_master/` で最新の `server_v*` を確認し、その中の `mods/<mod名>/master/` を使う
  - `items.json` — itemGuid と name の対応
  - `machineRecipes.json` — `time` (秒), `inputItems[]`, `outputItems[]`, `blockGuid`
  - `blocks.json` — blockGuid と name, blockType, gearConsumption.baseRpm 等
  - `fluids.json` — fluidGuid と name の対応（液体レシピで参照）
- 機械稼働の前提:
  - **GearMachine は `gearConsumption.baseRpm` で定格動作**することを仮定（機械ごとに異なるので blocks.json から読む。動力不足でRPMが下がるとレシピ時間が伸びる）
  - **ElectricMachine は `requiredPower` を満たして定格動作**することを仮定。`requiredPower: 0` の機械（石窯など）は電力供給不要
- 1台あたり生産量 = `60 / time × outputCount`（個/分）
- 必要台数 = `ceil(需要/分 ÷ 1台あたり/分)`

## 手順

### Step 1. 目標アイテムを itemGuid で特定

ユーザー指定のアイテム名を `items.json` から逆引きし itemGuid を取得する。

```bash
grep -n -B2 -A2 "\"name\": \"<アイテム名>\"" <master>/items.json
```

### Step 2. 出力レシピを検索

itemGuid が `outputItems[].itemGuid` に登場するレシピを `machineRecipes.json` で検索する。

```bash
grep -n -B5 -A2 "\"itemGuid\": \"<itemGuid>\"" <master>/machineRecipes.json
```

複数レシピがヒットした場合：

- ユーザーが特定の機械を指定していない限り、**最も基本的な機械（原始的な〜系）のレシピを採用**
- 出力側 (`outputItems[]`) に該当 itemGuid を含むものだけがそのアイテムの作成レシピ。入力側 (`inputItems[]`) に登場するものは別アイテムを作るときに当該アイテムを消費する側なので無視する

### Step 3. レシピ周辺を読み込んで `time` / `outputCount` / `inputItems[]` / `blockGuid` を確定

`Read` で前後 30〜50 行を取り、`time`、出力 count、入力 itemGuid と count、blockGuid を抜き出す。

`isRemain: true` の入力（鋳型など）は**消費されない**ので、需要計算からは除外する。ただし「機械セット時に1個必要」なのでスキル末尾の注意書きに残す。`isRemain` フィールドが未指定なら **false（消費される）扱い**。

### Step 4. blockGuid から機械種別を取得

`blocks.json` で blockGuid を引き、`name` と `blockType` を確認する。

```bash
grep -n -B1 -A3 "<blockGuid>" <master>/blocks.json
```

主要な `blockType`:
- `GearMachine` — 歯車動力。`gearConsumption.baseRpm` で定格
- `ElectricMachine` — 電力。`requiredPower` で定格

### Step 5. レート計算

```
1台あたり/分 = 60 / time × outputCount
必要台数 = ceil(需要/分 / 1台あたり/分)
親アイテム消費/分 → 各 inputItems の count × (需要/分 / outputCount)
```

### Step 6. 入力アイテムを Step 1〜5 に再帰

各 `inputItems[]` を新たな目標として再帰。`isRemain: true` は再帰しない（消費されないため）。**採取系の生原料（鉄鉱石・原木など）には採掘機の台数も含めて計算する**（採掘機レートは blocks.json の GearMiner / ElectricMiner ブロックの `mineSettings[].time` から `60 / time` 個/分）。

同じ中間素材が複数の親から消費される場合の扱いは、出力する2種類のビュー（完全展開ツリー / アイテム合計表）で異なる：
- **完全展開ツリー**: 合算しない。消費先ごとに**完全に独立したサブツリー**を展開する（鉄板用の鉄インゴットと鉄ロッド用の鉄インゴットは別ノード、原木も消費先ごとに別ノード）
- **アイテム種ごとの合計表**: 全消費先の需要を合算してから台数を計算する

### Step 7. 終端の判定

`grep` で `outputItems` に該当 itemGuid を持つレシピが見つからなければ、それは**採取系の生原料**（鉄鉱石・原木・青銅の鉱石など）。採掘機の台数を `ceil(需要/分 / (60/採掘time))` で計算してツリーに含める。

### Step 8. Pythonで計算と検算

レシピDB（dict）を組み立て、Pythonで再帰DFSを2回走らせる。**手計算は禁止、必ずPython実行**：

1. **完全展開DFS**: 子に`is_remain=False`の入力を辿る際、合算せず各経路ごとに独立にツリーを再帰。各ノードで `machines = ceil(demand/(60/time*output))` を計算し、機械種別合計に加算
2. **合算DFS**: 各アイテムの需要を全消費先で合計し、その合算需要から台数を計算
3. **検算**: 機械種別合計（ツリー展開ベース）と合算ベースの値が、共有素材を持たない部分（採掘機・加工機を除く可能性あり）で一致することを確認。差分は端数切り上げ累積によるものと明示

### Step 9. 出力（**4パート固定構成**）

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

### grep の前後行数が足りないと recipe ブロックが切れる
`grep -A2` ではレシピが切れる。`-B5 -A40` を取るか、ヒット位置を `Read` で読み直すこと。

### blockGuid → 機械名の引き直しは省略しない
blockGuid の hex だけで判断しない。必ず `blocks.json` で `name` を確認する。同じ系列でも tier 違い（原始的な加工機 vs 加工機）で `time` が異なるレシピが別々に登録されている。

### inputFluids / outputFluids の扱い
レシピは液体を入出力することがある（`fluidGuid` で参照）。液体の生産チェーンは固体と独立してたどる必要がある。今回の鉄フレーム鎖には液体は無いが、化学プラント等が絡む目標アイテムでは液体源（採掘・蒸気源・抽出機）まで遡ること。`fluids.json` に名前マッピングがある。
