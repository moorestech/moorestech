# 地上データセンター Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** GPU を消費して計算データを出す地上の電気機械「データセンター」を v8 マスタへ追加し、計算データの単位を2倍にして、GPU設計の研究に本線上の意味を持たせる。

**Architecture:** コード（C#）は変えない。データセンターは既存の `ElectricMachine` ブロックタイプの1台として moorestech_master の v8 mod に定義し、GPU LvN 1個 → 計算データの機械レシピを5本足す。計算データの量（衛星の到着成果・研究コスト・スタック上限）を全部2倍にし、ロケット技術・品質Lv1・品質Lv2 に計算データの要求を足す。クライアントには箱を並べたサーバーラック風の仮プレハブを `uloop execute-dynamic-code` で作って Addressable 登録し、本 repo のピンを master 側 PR のコミットへ進める。

**Tech Stack:** moorestech_master（v8 mod の JSON・localization.csv・nodeGraph）、Unity Editor（PrefabUtility・Addressables を execute-dynamic-code で操作）、プレイテストDSL（unity-playmode-recorded-playtest）。

## Requirements

- R1: v8 `blocks.json` に `ElectricMachine` のブロック「データセンター」を足す。受け入れ: `blockType: ElectricMachine`、`requiredPower: 300`、`inputSlotCount: 2`、`outputSlotCount: 2`、`allowQualityModule: false`、`category: 生産`・`subCategory: 電気機械`、`initialUnlocked: false`、`blockPrefabAddressablesPath: Vanilla/Block/DataCenter`。クリーンルーム系のタイプではない（地上に置ける）。
- R2: v8 `machineRecipes.json` にデータセンターのレシピを5本足す。GPU LvN 1個 → 計算データ 5/10/20/40/80（N=1〜5）、`time: 30`、`levelInheritance: None`、`initialUnlocked: false`。受け入れ: 5本が存在し個数がこの表どおり。
- R3: 研究の解放。GPU設計が データセンター（`unlockBlock`）・DCレシピLv1（`unlockMachineRecipe`）・計算データの表示（`unlockItemRecipeView`）を解放する。GPULv2〜5 の研究が DCレシピLvN を解放する。受け入れ: research.json の clearedActions にそれぞれ入っている。
- R4: 計算データの単位を2倍にする。受け入れ（v8 マスタの値）:
  - `items.json` `orbitalPayloads` の GPU衛星 Lv1〜5 の計算データ: 20/40/80/160/320
  - 計算データのスタック表（`tableGuid 01a19d8f-76ce-7471-9806-83551b67b4cb`）の `stackCounts`: `[10000]`
  - 研究の計算データ: 軌道エレベーターの建設 400、反物質爆弾 800、品質モジュールLv3〜5 4,000/40,000/400,000
- R5: 新しく計算データを要求する。受け入れ: ロケット技術 200、品質モジュールLv1 40、品質モジュールLv2 400 を `consumeItems` に追加（既存の消費品はそのまま）。
- R5b: 品質モジュールLv1 の前提を「GPU設計」だけにする（半導体製造を外す）。受け入れ: 品質モジュールLv1 の `prevResearchNodeGuids` が `["01a167f3-bd74-73f8-8c43-1869db833299"]`。nodeGraph の研究間の依存の線も GPU設計 → 品質モジュールLv1 に付け替わっている。
- R6: 表示まわり。受け入れ: `localization.csv` にデータセンターのブロック名の行（日・英・独・韓）があり、GPU設計・ロケット技術の研究説明文が新しい解放・消費に合っている。nodeGraph にデータセンターのノードが GPU設計の解放物として置かれ、master-refine 操作Aでブロックの sortPriority が再計算されている。
- R7: クライアントに仮プレハブ `moorestech_client/Assets/AddressableResources/Block/DataCenter.prefab` を作り、`Vanilla Asset Group` に `Vanilla/Block/DataCenter` で登録する。受け入れ: 黒っぽい箱を並べたサーバーラック風で、外形は blockSize（3×3×5）に収まる。プレハブ・Addressable はエディタ経由（execute-dynamic-code）でだけ作る。
- R8: 本 repo の `.moorestech-external-revisions.json` の moorestech_master ピンを、push 済みの master 側 PR のコミットへ更新する。
- R9: unityプレイ録画テストで、データセンターに GPU Lv1 を入れて給電すると計算データ 5 個が出力欄に出ること、データセンターの仮モデルが画面に出ることを確かめる。既存の tier9-4（衛星の到着成果）・tier9-5（品質Lv2 研究。消費に計算データが増える）も新マスタで通す。
- R10: 両 repo で PR を作る（moorestech はマージしない）。両 PR を相互リンクする。

やらないこと:
- C# のサーバー・クライアントのコード変更（新ブロックタイプ・GPU を挿したまま残す仕組み・寿命）
- セーブ形式の変更とマイグレーション（セーブ済みの計算データの個数は変換しない）
- 専用の本番モデル（今回は仮プレハブだけ）
- テスト用 mod（`Tests.Module/TestMod/ForUnitTest`）の変更

## Global Constraints

- マスタ JSON の編集は scratchpad に書いたスクリプトで一括適用する。スクリプトは repo に置かない（検査CI・一時スクリプトを repo に入れない）。
- 新しい GUID は UUIDv7 で採番する（既存の v8 GUID と同じ形式）。
- Prefab・`.asset`・`.meta` をテキストで編集・作成しない。プレハブと Addressable 登録は `uloop execute-dynamic-code` 経由だけ。
- ピン変更は `git add .moorestech-external-revisions.json` 単独でコミットし、直後に `git show HEAD:.moorestech-external-revisions.json` で値を確かめる（Editor が書き戻すため）。
- `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` は worktree 作成直後から Editor が書き換えた差分がある。本 plan の変更ではないので、どのコミットにも含めない（`git add -A` を使わない）。
- 研究説明文は解放内容ベースの1行（裁定 `2026-08-22-研究説明文は全ノードに解放内容ベースの1行を書く`）。
- Editor は自分の worktree のものだけを使う（`moores-wt status` で確認）。

作業場所:
- 本 repo: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/ground-data-center`（branch `feat/ground-data-center`、origin/master 8764a2fb80 から。Editor 起動済み）
- master 側: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/ground-data-center`（branch `feat/ground-data-center`。Task 1 Step 1 で origin/master から作る）
- 対象 mod: `server_v8/mods/moorestechAlphaMod_8/`

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件 / 仮プレハブを Block レイヤー＋外形 BoxCollider に、nodeGraph のノードを block 型に直した
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱1・第3バケツ0 / SelectRecipe の TryGetComponent の bool 捨て（tier9-5 の写し）が弱1件。別に「前例に倣った」の根拠誤記を直した。検査後に裁定で R5b（品質Lv1 の前提を GPU設計だけに）を Task 1 へ足したが、マスタ値1件と nodeGraph の線の付け替えだけなので再検査はしていない

---

## 既知の値（ピン 1c913db 時点の v8 マスタ）

| 名前 | GUID |
|---|---|
| 計算データ（item） | `01a167cf-af88-74d3-9cd6-7d61b71052bb` |
| GPULv1〜5（item） | `01a167ed-2703-7014-98e4-11bd15f38571`・`01a167ed-2706-7bfd-8361-a430dd96a569`・`01a167ed-2709-74f0-a5c6-25026c7479d9`・`01a167ed-270c-7873-a83a-5cf02ead6277`・`01a167ed-270f-7276-8700-469ab54a26a2` |
| 鋼鉄板・コンピュータ・制御モータ（item） | `019e3a98-cfe5-73eb-bf6a-a564400bdb4f`・`019f0ca4-a544-723e-8ff2-feb15f99c84e`・`019f0ca5-a575-7625-a43e-f43e8471287e` |
| 研究 GPU設計 | `01a167f3-bd74-73f8-8c43-1869db833299` |
| 研究 GPULv2 | `01a19cf1-4847-7758-bb41-4282892021e6`（Lv3〜5 は名前「GPULv3」〜「GPULv5」で引く） |
| 研究 ロケット技術 | `01a167cf-af9d-7f31-817c-3069b4f6e499` |
| 研究 品質モジュールLv1 / Lv2 | `01a167f3-bd6b-70c9-be6e-52e41907b21f` / `01a173a4-cfe2-769c-9b35-eabb655f9a8f` |
| 計算データのスタック表 | `01a19d8f-76ce-7471-9806-83551b67b4cb`（`stackCounts: [5000]`） |
| 前例ブロック 金属3Dプリンタ | `01a167e9-21be-72cd-ae04-e5bbe12b95c2`（ElectricMachine・接続口の形 `shapeGuid 65f063cd-3ae1-4b55-b39d-00e033b7ec98`） |

研究・ブロック・品目を名前で引くスクリプトは、名前が一意であることを assert してから使う。

### Task 1: v8 マスタにデータセンターを足し、計算データの単位を2倍にする

**Files（master 側 worktree からの相対パス）:**
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/blocks.json`
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/machineRecipes.json`
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/research.json`
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/items.json`
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`
- Modify: `server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json`

**Interfaces:**
- Consumes: なし
- Produces: データセンターのブロック GUID（以下 `DC_BLOCK`）、DCレシピ5本の GUID（`DC_RECIPE_LV1`〜`LV5`）、master 側の push 済みコミット（Task 2 のピンの値）。名前「データセンター」は Task 3 のシナリオが `PlaceBlockDirect("データセンター", …)` で引く。

- [ ] **Step 1: master 側の worktree を作る**

```bash
git -C /Users/sakastudio/hermes-agent/data/repos/moorestech_master fetch origin
git -C /Users/sakastudio/hermes-agent/data/repos/moorestech_master worktree add -b feat/ground-data-center /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/ground-data-center origin/master
```

- [ ] **Step 2: 適用スクリプトを scratchpad に書いて実行する**

scratchpad（例 `/private/tmp/.../scratchpad/apply_data_center.py`）に置き、master 側 worktree の `server_v8/mods/moorestechAlphaMod_8/master` を引数で受ける。JSON は既存ファイルのインデント・キー順を保って書き戻す（読み込み時のインデント幅を検出して `json.dump(..., ensure_ascii=False, indent=<検出値>)`、末尾改行は元に合わせる）。

```python
import json, sys, uuid, time, os
M = sys.argv[1]
def load(n): return json.load(open(os.path.join(M, n + '.json'), encoding='utf-8'))
def save(n, d): json.dump(d, open(os.path.join(M, n + '.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=INDENT[n]); open(os.path.join(M, n + '.json'), 'a').write('\n')
def uuid7():
    b = bytearray(int(time.time() * 1000).to_bytes(6, 'big') + os.urandom(10))
    b[6] = (b[6] & 0x0F) | 0x70; b[8] = (b[8] & 0x3F) | 0x80
    return str(uuid.UUID(bytes=bytes(b)))
INDENT = {}  # 各ファイルを開いて先頭の2行目の字下げ幅を数えて埋める
for n in ['blocks', 'machineRecipes', 'research', 'items']:
    second = open(os.path.join(M, n + '.json'), encoding='utf-8').read().split('\n')[1]
    INDENT[n] = len(second) - len(second.lstrip(' '))

items, blocks, recipes, research = load('items'), load('blocks'), load('machineRecipes'), load('research')
def one(seq, key, value):
    hit = [x for x in seq if x[key] == value]; assert len(hit) == 1, (key, value, len(hit)); return hit[0]
item = lambda name: one(items['data'], 'name', name)['itemGuid']
node = lambda name: one(research['data'], 'researchNodeName', name)
DATA = item('計算データ')
GPU = [item(f'GPULv{n}') for n in range(1, 6)]

# 1) ブロック（接続口の置き方は金属3Dプリンタに倣う。入力口は南面、出力口は北面。サイズと費用は独自値）
DC_BLOCK = uuid7()
blocks['data'].append({
    "blockGuid": DC_BLOCK, "name": "データセンター", "blockType": "ElectricMachine",
    "blockSize": [3, 3, 5], "blockPrefabAddressablesPath": "Vanilla/Block/DataCenter", "openable": True,
    "blockParam": {
        "requiredPower": 300, "idlePowerRate": 0.2, "inputSlotCount": 2, "outputSlotCount": 2,
        "inventoryConnectors": {
            "inputConnects": [{"connectorGuid": uuid7(), "offset": [1, 0, 0], "directions": [[0, 0, -1]], "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}],
            "outputConnects": [{"connectorGuid": uuid7(), "offset": [1, 0, 4], "directions": [[0, 0, 1]], "shapeGuid": "65f063cd-3ae1-4b55-b39d-00e033b7ec98"}]},
        "inputTankCount": 0, "outputTankCount": 0, "innerTankCapacity": 0, "moduleSlotCount": 3,
        "fluidInventoryConnectors": {"inflowConnects": [], "outflowConnects": []},
        "maxWireConnectionCount": 2, "connectionRange": 30, "connectionHeightRange": 20, "allowQualityModule": False},
    "initialUnlocked": False,
    "requiredItems": [{"itemGuid": item('鋼鉄板'), "count": 60}, {"itemGuid": item('コンピュータ'), "count": 20}, {"itemGuid": item('制御モータ'), "count": 10}],
    "placementsPerCost": 1, "sortPriority": 0, "category": "生産", "subCategory": "電気機械"})

# 2) レシピ（GPU LvN 1個 → 計算データ 5×2^(N-1)）
DC_RECIPES = []
for n in range(1, 6):
    g = uuid7(); DC_RECIPES.append(g)
    recipes['data'].append({"machineRecipeGuid": g, "blockGuid": DC_BLOCK, "time": 30,
        "inputItems": [{"itemGuid": GPU[n - 1], "count": 1, "isRemain": False}], "inputFluids": [],
        "outputItems": [{"itemGuid": DATA, "count": 5 * 2 ** (n - 1), "percent": 1}], "outputFluids": [],
        "initialUnlocked": False, "levelInheritance": "None"})

# 3) 解放
def action(nd, kind, key):
    hit = [a for a in nd['clearedActions'] if a['gameActionType'] == kind]
    if not hit: nd['clearedActions'].append({"gameActionType": kind, "gameActionParam": {key: []}}); hit = nd['clearedActions'][-1:]
    return hit[0]['gameActionParam'][key]
gpu_design = node('GPU設計')
action(gpu_design, 'unlockBlock', 'unlockBlockGuids').append(DC_BLOCK)
action(gpu_design, 'unlockMachineRecipe', 'unlockMachineRecipeGuids').append(DC_RECIPES[0])
action(gpu_design, 'unlockItemRecipeView', 'unlockItemGuids').append(DATA)
for n in range(2, 6): action(node(f'GPULv{n}'), 'unlockMachineRecipe', 'unlockMachineRecipeGuids').append(DC_RECIPES[n - 1])

# 4) 計算データの単位を2倍（研究コスト・衛星の到着成果・スタック上限）
before = {}
for nd in research['data']:
    for c in nd['consumeItems']:
        if c['itemGuid'] == DATA: before[nd['researchNodeName']] = c['itemCount']; c['itemCount'] *= 2
assert before == {'反物質爆弾': 400, '軌道エレベーターの建設': 200, '品質モジュールLv3': 2000, '品質モジュールLv4': 20000, '品質モジュールLv5': 200000}, before
for p in items['orbitalPayloads']:
    for o in p['arrivalOutputs']:
        if o['itemGuid'] == DATA: o['count'] *= 2
table = one(items['itemStackLevelTables'], 'tableGuid', '01a19d8f-76ce-7471-9806-83551b67b4cb')
assert table['stackCounts'] == [5000]; table['stackCounts'] = [10000]

# 5) 新しい計算データの要求
for name, count in [('ロケット技術', 200), ('品質モジュールLv1', 40), ('品質モジュールLv2', 400)]:
    nd = node(name); assert all(c['itemGuid'] != DATA for c in nd['consumeItems']); nd['consumeItems'].append({"itemGuid": DATA, "itemCount": count})

# 6) 品質モジュールLv1 の前提を GPU設計だけにする
q1 = node('品質モジュールLv1'); assert q1['prevResearchNodeGuids'] == [node('半導体製造')['researchNodeGuid']], q1['prevResearchNodeGuids']
q1['prevResearchNodeGuids'] = [gpu_design['researchNodeGuid']]

for n, d in [('blocks', blocks), ('machineRecipes', recipes), ('research', research), ('items', items)]: save(n, d)
print('DC_BLOCK', DC_BLOCK); print('DC_RECIPES', DC_RECIPES)
```

Run: `python3 -I <scratchpad>/apply_data_center.py /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/ground-data-center/server_v8/mods/moorestechAlphaMod_8/master`
Expected: `DC_BLOCK` と5本の GUID が出る。`git diff --stat` で4ファイルだけが変わり、整形起因の全行差分が無い（差分が全行に及ぶならインデント検出を直してやり直す）。出た GUID は Task 1 のコミットメッセージに書く。

- [ ] **Step 3: 研究説明文とブロック名の翻訳を直す**

`localization.csv`（列: `key,Source,english,japanese,german,korean`）を scratchpad のスクリプトで書き換える（csv モジュールで読み書きし、既存の引用符の付け方を保つ）。
- 追加: `block.<DC_BLOCK>.name,データセンター,Data Center,データセンター,Rechenzentrum,데이터 센터`
- `research.01a167f3-bd74-73f8-8c43-1869db833299.description`（GPU設計）: Source/japanese「ICチップからGPUを作れるようにし、GPUを計算データに変えるデータセンターを解放する」、english「Lets you make GPUs from IC chips and unlocks the Data Center, which turns GPUs into compute data」、german「Ermöglicht die Herstellung von GPUs aus IC-Chips und schaltet das Rechenzentrum frei, das GPUs in Rechendaten umwandelt.」、korean「IC 칩으로 GPU를 만들 수 있게 하고, GPU를 계산 데이터로 바꾸는 데이터 센터를 해금합니다」
- `research.01a167cf-af9d-7f31-817c-3069b4f6e499.description`（ロケット技術）: Source/japanese「ロケット発射台と、ロケットの筐体素材・燃料・GPU衛星のレシピを解放する。GPU衛星を打ち上げると地上より多くの計算データが届く」、english「Unlocks the rocket launch pad and recipes for rocket casings, fuel and GPU satellites. Launched GPU satellites return more compute data than the ground」、german「Schaltet die Raketenstartrampe sowie Rezepte für Raketenhüllen, Treibstoff und GPU-Satelliten frei. Gestartete GPU-Satelliten liefern mehr Rechendaten als der Boden.」、korean「로켓 발사대와 로켓 동체 소재·연료·GPU 위성 레시피를 해금합니다. GPU 위성을 발사하면 지상보다 많은 계산 데이터가 돌아옵니다」
- GPULv2〜5 の研究説明文（`research.<GUID>.description`、現状「GPUとGPU衛星をLvNまで作れるようにする」）: Source/japanese「GPUとGPU衛星をLvNまで作れるようにし、データセンターでLvNのGPUを計算データに変えられるようにする」、english「Lets you make GPUs and GPU satellites up to LvN, and turn LvN GPUs into compute data at the Data Center」、german「Ermöglicht GPUs und GPU-Satelliten bis LvN und die Umwandlung von LvN-GPUs in Rechendaten im Rechenzentrum.」、korean「GPU와 GPU 위성을 LvN까지 만들 수 있게 하고, 데이터 센터에서 LvN GPU를 계산 데이터로 바꿀 수 있게 합니다」（N は 2〜5 に置き換える。既存の english 等が別の文なら、同じ意味の1行に揃える）
- `research.json` の同じノード（GPU設計・ロケット技術・GPULv2〜5）の `researchNodeDescription` も Source と同じ文にする。

- [ ] **Step 4: nodeGraph にデータセンターを置き、操作Aを回す**

- `nodeGraph.v1.json` に、研究が解放するブロックのノードを1つ足す。形式は既存の block ノード（金属3Dプリンタ: `{"id": "node-sync-019e3af5-…-01a167e9-21be-…", "type": "block", "masterGuid": "01a167e9-21be-72cd-ae04-e5bbe12b95c2", …}`）を写し、`type: "block"`・`id: "node-sync-01a167f3-bd74-73f8-8c43-1869db833299-<DC_BLOCK>"`・`masterGuid: <DC_BLOCK>` にする。位置は GPU設計ノードの右下で、GPULv1 ノードの右隣（右へ 100〜300）。既存ノードと重ならないことをスクリプトで確かめる（矩形の重なり0）。研究とブロックの間に線は引かない（前例と同じ）。前例の金属3Dプリンタのブロックノードには placeholder ノードから buildCost の辺が張られている（block ノードの大半が同じ形）ので、その placeholder ノードと buildCost 辺の組も同じ形式で写し、費用は Step 2 の requiredItems に合わせる。
- 研究間の依存の線（半導体製造 → 品質モジュールLv1）を GPU設計 → 品質モジュールLv1 に付け替える（既存の研究間の線と同じ形式。線の種類と向きは既存の 半導体製造 → GPU設計 の線を写す）。品質の枝（品質Lv1〜5 と各モジュール・GPU Lv2〜5・スタック拡張）がまとまって GPU設計より右に来るよう、必要なら区画ごと平行移動する（重なり0・線がノードを横切る箇所0）。
- master-refine スキルの操作B（`recalc_research_positions.py`）で research.json の UIPosition を nodeGraph に合わせて再計算する。
- master-refine スキルの操作A（`.agents/skills/master-refine/scripts/recalc_sort_priority.py`、手順は本 repo の `.agents/skills/master-refine/SKILL.md`）を v8 に対して、master 側 worktree を対象に実行する。
Expected: `blocks.json` のデータセンターの `sortPriority` が 0 以外になり、`git diff` で他ブロック・品目の並べ替えと UIPosition の変化が操作A・B の結果だけである。

- [ ] **Step 5: 値を確かめる**

scratchpad の確認スクリプトで R1〜R5 の値を全件 assert する（ブロックの param、レシピ5本の入出力個数、各研究の clearedActions と consumeItems、orbitalPayloads 20/40/80/160/320、スタック表 [10000]）。
Expected: assert 全通過。

- [ ] **Step 6: master 側をコミット・push する**

```bash
cd /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/ground-data-center
git add server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json
git commit -m "v8: GPU を計算データに変える電気機械データセンターを足し、計算データの単位を2倍にする（DC_BLOCK=<GUID>）"
git push -u origin feat/ground-data-center
```

### Task 2: 仮プレハブを作り、ピンを進めてマスタのロードを確かめる

**Files:**
- Create（Editor 経由）: `moorestech_client/Assets/AddressableResources/Block/DataCenter.prefab`、`moorestech_client/Assets/AddressableResources/Material/DataCenter/DataCenter_Body.mat`・`DataCenter_Lamp.mat`（`.meta` は Unity が作る）
- Modify（Editor 経由）: `moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset`
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: Task 1 の push 済みコミットと `blockPrefabAddressablesPath: Vanilla/Block/DataCenter`
- Produces: Addressable アドレス `Vanilla/Block/DataCenter`、新しいピン

- [ ] **Step 1: プレハブを作って登録する**

前例: `docs/superpowers/plans/2026-08-20-tutorial-label-keycontrol-anchors.md` の StoneAxe 登録（`PrefabUtility.SaveAsPrefabAsset` → `CreateOrMoveEntry`）。既存ブロックのプレハブ（例 `AddressableResources/Block/3D Printer.prefab`）はスクリプトを持たず見た目だけなので、同じく見た目だけにする。ブロックの原点は前例プレハブと同じ「ブロックの最小角」にあるかを先に `3D Printer.prefab` を開いて子の bounds で確かめ、違えば下の配置をその基準に合わせる。

`uloop execute-dynamic-code --project-path ./moorestech_client` で次を実行する:

```csharp
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

var outputPath = "Assets/AddressableResources/Block/DataCenter.prefab";
var root = new GameObject("DataCenter") { layer = LayerMask.NameToLayer("Block") };
var shader = Shader.Find("Universal Render Pipeline/Lit");
var body = new Material(shader) { color = new Color(0.08f, 0.09f, 0.11f) };
var lamp = new Material(shader) { color = new Color(0.1f, 0.8f, 0.4f) };
lamp.EnableKeyword("_EMISSION"); lamp.SetColor("_EmissionColor", new Color(0.1f, 0.8f, 0.4f) * 2f);
AssetDatabase.CreateFolder("Assets/AddressableResources/Material", "DataCenter");
AssetDatabase.CreateAsset(body, "Assets/AddressableResources/Material/DataCenter/DataCenter_Body.mat");
AssetDatabase.CreateAsset(lamp, "Assets/AddressableResources/Material/DataCenter/DataCenter_Lamp.mat");

// クリック用の当たり判定は外形を覆う BoxCollider 1つを Block レイヤーで焼き込む（前例: 3D Printer.prefab は全オブジェクトが Block レイヤー）
var click = root.AddComponent<BoxCollider>();
click.center = new Vector3(1.5f, 1.5f, 2.5f);
click.size = new Vector3(3f, 3f, 5f);

// 3×3×5 の範囲に、幅0.9のラックを x 方向に3列×z 方向に2列並べ、正面にランプの帯を付ける
for (var x = 0; x < 3; x++)
for (var z = 0; z < 2; z++)
{
    var rack = GameObject.CreatePrimitive(PrimitiveType.Cube);
    rack.name = $"Rack_{x}_{z}";
    rack.layer = root.layer;
    Object.DestroyImmediate(rack.GetComponent<BoxCollider>());
    rack.transform.SetParent(root.transform, false);
    rack.transform.localPosition = new Vector3(x + 0.5f, 1.3f, 1.25f + z * 2.5f);
    rack.transform.localScale = new Vector3(0.9f, 2.6f, 2.2f);
    rack.GetComponent<MeshRenderer>().sharedMaterial = body;
    for (var row = 0; row < 5; row++)
    {
        var led = GameObject.CreatePrimitive(PrimitiveType.Cube);
        led.name = $"Lamp_{row}";
        led.layer = root.layer;
        Object.DestroyImmediate(led.GetComponent<BoxCollider>());
        led.transform.SetParent(rack.transform, false);
        led.transform.localPosition = new Vector3(0f, -0.4f + row * 0.2f, -0.51f);
        led.transform.localScale = new Vector3(0.8f, 0.02f, 0.01f);
        led.GetComponent<MeshRenderer>().sharedMaterial = lamp;
    }
}
PrefabUtility.SaveAsPrefabAsset(root, outputPath);
Object.DestroyImmediate(root);

var settings = AddressableAssetSettingsDefaultObject.Settings;
var group = settings.FindGroup("Vanilla Asset Group");
var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(outputPath), group, false, false);
entry.address = "Vanilla/Block/DataCenter";
settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true, true);
AssetDatabase.SaveAssets();
Debug.Log("DataCenter registered: " + entry.address);
```

当たり判定は `BlockGameObjectColliderSetup.IsClickableCollider` が Block レイヤーの Collider を要求する（実行時には付けない）。上のとおり root と全ての子を Block レイヤーにし、外形の BoxCollider を root に1つ持たせる。マテリアルの置き場は CleanRoomMachine の前例（`AddressableResources/Material/CleanRoomMachine/`）に合わせる。
Run 後: `grep -n "Vanilla/Block/DataCenter" "moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset"` が1件、`ls moorestech_client/Assets/AddressableResources/Block/DataCenter.prefab* moorestech_client/Assets/AddressableResources/Material/DataCenter/` で prefab・mat と各 `.meta`（フォルダの `.meta` を含む）がある。`grep -c "m_Layer: 0" moorestech_client/Assets/AddressableResources/Block/DataCenter.prefab` が 0。

- [ ] **Step 2: プレハブをコミットする**

```bash
git add moorestech_client/Assets/AddressableResources/Block/DataCenter.prefab moorestech_client/Assets/AddressableResources/Block/DataCenter.prefab.meta moorestech_client/Assets/AddressableResources/Material/DataCenter moorestech_client/Assets/AddressableResources/Material/DataCenter.meta "moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset"
git commit -m "データセンターの仮プレハブ（箱を並べたサーバーラック）を Vanilla/Block/DataCenter で登録する"
```

`Vanilla Asset Group.asset` の差分がデータセンターの1エントリ追加だけであることを `git show --stat HEAD` と `git show HEAD -- "moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset"` で確かめる。

- [ ] **Step 3: ピンを進める**

`.moorestech-external-revisions.json` の `moorestech_master.commitHash` を Task 1 Step 6 の push 済みコミット（`git -C /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/ground-data-center rev-parse HEAD`）にする。

```bash
git add .moorestech-external-revisions.json
git commit -m "moorestech_master のピンをデータセンター追加・計算データ単位2倍のコミットへ進める"
git show HEAD:.moorestech-external-revisions.json
```
Expected: 表示された commitHash が master 側の HEAD と一致する（Editor の書き戻しで戻っていない）。

- [ ] **Step 4: マスタのロードを確かめる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0。v8 出荷マスタを読む自動テストは今は無い（Shipped Master Gate は eba8cd30f6 で削除済み）ので、v8 のロード確認は Task 3 の録画テスト（出荷マスタで起動する）だけで行う。ここで「テストが無いのでコンパイルだけで PASS」と報告して Task 3 を省かない。

### Task 3: unityプレイ録画テスト

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-8-data-center.cs`

**Interfaces:**
- Consumes: ブロック名「データセンター」、品目名「GPULv1」「計算データ」、研究 GPU設計 `01a167f3-bd74-73f8-8c43-1869db833299`、Task 2 のピン
- Produces: 録画・result.json（worktree の gitignore 下）

- [ ] **Step 1: シナリオを書く**

前例は `scenarios/tier9/tier9-5-compute-research.cs`（機械の設置・給電・レシピ選択・UI撮影）。`#region Internal` のローカル関数は tier9-5 の同名関数と同じ本文で、文言だけ「データセンター」にしてある。200 行未満に収める。

```csharp
// 出荷マスタで GPU設計を研究済みにしてデータセンターを置き、GPU Lv1 を入れると計算データがレシピどおり出ることと、仮モデルが画面に出ることを確かめる
// With the shipped master, mark GPU design done, place a Data Center, feed one GPU Lv1, and check the recipe's compute data appears and the placeholder model shows
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Client.Playtest.Operations.Ui;
using Core.Inventory;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Mooresmaster.Model.MachineRecipesModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var gpuDesignResearch = Guid.Parse("01a167f3-bd74-73f8-8c43-1869db833299"); // GPU設計
return PlaytestRunner.Run("tier9-8-data-center", new PlaytestRunOptions { Record = true }, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    await p.Until(() => p.CurrentUiState == UIStateEnum.GameScreen && Camera.main != null, 15f, "GameScreen とメインカメラに戻った");
    // 地形が機械の手前に湧いて照準と画面を塞ぐので、地形の表示と生成を止める
    // Terrain spawns in front of the machine and blocks aim and view, so stop its display and spawning
    UnityEngine.Object.FindFirstObjectByType<MapObjectGameObjectDatastore>().SetActive(false);
    await PlaytestUiOps.FocusGameViewAndEnableInput(p);
    p.WarpPlayer(new Vector3(1.5f, 32.5f, -8f));

    // GPU設計とその前提を完了扱いにし、データセンターが解放されたことを確かめる
    // Mark GPU design and its prerequisites done and check the Data Center is unlocked
    await p.RunOnServer("GPU設計とその前提を完了扱いにする", () => ServerContext.GetService<IResearchDataStore>().LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(new[] { gpuDesignResearch }).Select(g => g.ToString()).ToList() }));
    await p.WaitForResearch(new[] { gpuDesignResearch }, Array.Empty<Guid>(), 15f, "GPU設計が完了扱いになった");

    // データセンターを置いて給電し、GPU Lv1 のレシピを選ぶ
    // Place and power the Data Center and select the GPU Lv1 recipe
    p.Note("データセンターを置いて給電し、GPU Lv1 → 計算データのレシピを選ぶ");
    var gpuLv1 = PlaytestItemOps.ResolveItemId("GPULv1");
    var computeData = PlaytestItemOps.ResolveItemId("計算データ");
    var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(PlaytestBlockOps.ResolveBlockId("データセンター")).BlockGuid;
    var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.Single(r => r.BlockGuid == blockGuid && r.InputItems.Single().ItemGuid == MasterHolder.ItemMaster.GetItemMaster(gpuLv1).ItemGuid);
    var expected = recipe.OutputItems.Single().Count;
    p.Assert(expected == 5, $"GPU Lv1 1個から計算データ 5 個のレシピである（{expected} 個）");
    var machine = await p.PlaceBlockDirect("データセンター", new Vector3Int(0, 32, 0), BlockDirection.North);
    var generator = await p.PlaceBlockDirect("ガソリンエンジン発電機", new Vector3Int(-12, 32, -20), BlockDirection.North);
    await PowerMachine();
    await SelectRecipe(recipe);
    var machineObject = await p.WaitBlockGameObject(machine.BlockPositionInfo.OriginalPos);
    await p.Until(() => !machineObject.IsPlayingPlaceAnimation, 15f, "データセンターの設置演出が終わった");
    await p.Screenshot("01-data-center-placed");

    // GPU Lv1 を1個入れ、計算データが出力欄に出るまで待つ
    // Insert one GPU Lv1 and wait for compute data in the output slots
    p.Note("GPU Lv1 を1個入れ、計算データ 5 個が出るのを待つ");
    var rest = -1;
    await p.RunOnServer("GPU Lv1 を1個入れる", () => rest = machine.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(ServerContext.ItemStackFactory.Create(gpuLv1, 1)).Count);
    p.Assert(rest == 0, "GPU Lv1 が入った");
    await Refuel();
    await p.Until(() => Count(computeData) == expected, recipe.Time + 30f, $"計算データ {expected} 個が出力欄に出た");
    await p.Until(() => Count(gpuLv1) == 0, 5f, "GPU Lv1 が消費された");
    await ShootMachine("02-compute-data-output");
    p.Note("データセンター 完了");

    #region Internal

    IEnumerable<Guid> WithPrerequisites(IEnumerable<Guid> targets)
    {
        // 前提を辿って閉包を作る（CompleteResearch は素材と前提が揃わないと拒むため使わない）
        // Walk prerequisites into a closure (CompleteResearch refuses without items and prerequisites)
        var (done, pending) = (new HashSet<Guid>(), new Stack<Guid>(targets));
        while (pending.TryPop(out var guid))
            if (done.Add(guid)) foreach (var prev in MasterHolder.ResearchMaster.GetResearch(guid).PrevResearchNodeGuids) pending.Push(prev);
        return done;
    }

    async UniTask SelectRecipe(MachineRecipeMasterElement target)
    {
        machine.ComponentManager.TryGetComponent<IMachineRecipeSelectorComponent>(out var selector);
        var result = MachineRecipeSelectionResult.Success;
        await p.RunOnServer("データセンターのレシピを選ぶ", () => result = selector.SetSelectedRecipe(target, new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 10)));
        p.Assert(result == MachineRecipeSelectionResult.Success, $"select recipe {target.MachineRecipeGuid}");
    }

    async UniTask PowerMachine()
    {
        // 発電機 → 広範囲電柱 → データセンターを配線する（結線で電力網の再構築は自動で予約される）
        // Wire generator -> wide-range pole -> data center (wiring schedules the network rebuild)
        var pole = (await p.PlaceBlockDirect("広範囲電柱", new Vector3Int(-2, 32, -3), BlockDirection.North)).GetComponent<IElectricWireConnector>();
        var free = new ConnectionLineRecord(Guid.Parse("872372d5-2998-4fb7-826c-593ceeafcfb2"), Array.Empty<ConnectToolMaterialCost>());
        var wired = new List<bool>();
        await p.RunOnServer("発電機 → 電柱 → データセンターを配線する", () =>
        {
            wired.Add(ElectricWireSystemUtil.TryConnectBothSides(generator.GetComponent<IElectricWireConnector>(), pole, free));
            wired.Add(ElectricWireSystemUtil.TryConnectBothSides(pole, machine.GetComponent<IElectricWireConnector>(), free));
        });
        p.Assert(wired.Count == 2 && wired.All(w => w), "発電機 → 電柱 → データセンターを配線した");
        await Refuel();
    }

    // 発電機のタンクは数十秒で空になるので、加工を待つ前に満タンまで入れ直す（溢れた分は捨てる）
    // The generator tank empties within tens of seconds, so top it up before waiting on a craft (overflow is dropped)
    async UniTask Refuel() => await p.RunOnServer("発電機を満タンまで給油する", () => generator.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(100000, MasterHolder.FluidMaster.GetFluidId(new Guid("019eded2-7454-76da-8943-84c4e7a59179"))), default));

    async UniTask ShootMachine(string screenshotName)
    {
        // F は射程2mなので南面の1m手前に着地し、Alt のカーソル照準で南面を狙って機械 UI を開き、出力欄が見える状態で撮る
        // F reaches 2 m, so land 1 m south of the face, open the machine UI by aiming at it with the Alt cursor, and shoot with the output slots visible
        var origin = machine.BlockPositionInfo.OriginalPos;
        p.WarpPlayer(new Vector3(origin.x + 1.5f, 32.5f, origin.z - 1f));
        await p.Until(() => Mathf.Abs(p.PlayerPosition.y - 32f) < 0.05f, 5f, "地面(y=32.0)に着地した");
        SemanticInput.KeyDown(Key.LeftAlt);
        await p.AimAt(new Vector3(origin.x + 1.5f, 33f, origin.z));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
        SemanticInput.KeyUp(Key.LeftAlt);
        await p.UntilWebUiElement("block-inventory-close", 10f);
        await p.Screenshot(screenshotName);
        await p.ClickWebUi("block-inventory-close");
        await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    }

    int Count(ItemId id) => machine.GetComponent<IOpenableBlockInventoryComponent>().InventoryItems.Where(s => s.Id == id).Sum(s => s.Count);

    #endregion
});
```

データセンターは 3×3×5 なので `ShootMachine` の照準座標（南面の中央 x=origin.x+1.5）はそのまま使える。

- [ ] **Step 2: 実行する**

unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh` で tier9-8-data-center・tier9-4-rocket-launch・tier9-5-compute-research を実行する（Editor は自分の worktree のもの。第3引数は付けずピンから自動解決させ、解決できなければ `/Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/ground-data-center/server_v8` を第3引数で明示する）。
Expected: 3本とも result.json が成功。tier9-8 の録画とスクショ `01-data-center-placed` にサーバーラック風の仮モデルが見え、`02-compute-data-output` の出力欄に計算データ 5 が見える。tier9-4 は衛星Lv1 1便で計算データ 20 個が届く。録画とスクショは目で見て確かめ、見たものを報告に書く。

- [ ] **Step 3: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-8-data-center.cs
git commit -m "データセンターに GPU を入れると計算データが出る録画シナリオを足す"
```

テストで `Saves/` に作ったワールドは作業の終わりに `list-work-saves.sh` で探して消す。

### Task 4: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。Codex ホストでは `references/codex-host.md` の runner で実行する。master 側 PR の差分（Task 1）もレビュー対象に含める。レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、反映後のバイナリで Task 3 の録画テストを再実施してから完了とする。

### Task 5: PR 作成とセッション終了可能化

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確かめて終える。moorestech_master 側の PR も作り、本 repo の PR と相互リンクし、本 repo のピンが master 側 PR の push 済みコミットを指していることを確かめる。PR 本文の成果物の載せ先は環境ローカル規約（CLAUDE.local.md「PR の成果物の載せ先」）に従う。moorestech の PR はマージしない。

## 判断記録（ADR）

- 設計 ADR: `docs/adr/0095-ground-data-center-consumes-gpu-for-compute-data.md`。ユーザー裁定は `.decisions/2026-11-03-データセンターはGPUを使い切る電気機械にする.md`・`2026-11-03-データセンターはGPU設計で解放しロケット技術と品質Lv1Lv2に計算データを要求する.md`・`2026-11-03-計算データの単位を2倍にしデータセンターは衛星の4分の1にする.md`・`2026-11-03-データセンターの見た目はブロックを並べた仮モデルを作る.md`。
- 品質モジュールLv1 の前提を GPU設計だけにする。出所: ユーザー裁定 2026-11-03 原文「前提をGPU設計だけにする。半導体製造を前提にする意味がない」（`.decisions/2026-11-03-品質モジュールLv1の前提はGPU設計だけにする.md`）。
- tier9-8 の `SelectRecipe` が `TryGetComponent` の戻り値を捨てるのは tier9-5 の写しのまま残す（取れなければ次の行で落ちて気づける）。出所: シミュレーター予測（確信高）→ 前提宣言、ユーザーの異議なし（拒否権つき）。
- 設計インタビュー後の Codex 独立質問（moores-grill-with-docs §1.7）は、ユーザーが課金を承諾しなかったため実施していない（出所: ユーザー裁定 2026-11-03 選択「回さない」）。
- 接続口の置き方（南面中央に入力・北面中央に出力、同じ shapeGuid）・moduleSlotCount 3・idlePowerRate 0.2 は金属3Dプリンタに倣った。ブロックサイズ 3×3×5 と建設費用（鋼鉄板60・コンピュータ20・制御モータ10）は前例に同じ値が無く、ラック2列が入る大きさと金属3Dプリンタ（鋼鉄板60・コンピュータ10・制御モータ10）に近い費用として決めた独自値。出所: agent前提（ユーザーは「建設費用・ブロックサイズ・接続口の位置は前例に合わせて実装側で決める」を含む既定を承諾。値そのものは未裁定）。
- データセンターのレシピは GPU のレベル継承（`InheritUpToUnlocked`）を使わずレベル別に5本にする。出力の計算データはレベルを持たず、継承では出力個数をレベルで変えられないため。前例は GPU衛星Lv1〜5 のレシピ（レベル別・`levelInheritance: None`）。出所: agent前提（5本・各 GPU LvN 研究で解放はユーザーが既定として承諾）。
- 計算データの表示（`unlockItemRecipeView`）を GPU設計にも足す。品質Lv1 とロケット技術が計算データを要求するので、それより前に表示が要る。ロケット技術側の表示は残す。出所: agent前提。
- 録画テストは必須（ブロックの設置・見た目・機械の加工というランタイム挙動に触れる）。新規 tier9-8 と、新マスタで値が変わる tier9-4・tier9-5 を実行する。
- テスト用 mod とサーバーのユニットテストは変えない（C# の変更が無く、テスト用 mod は v8 マスタを読まない）。
