# 電気精密組立機 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 入力4口の電気機械「電気精密組立機」を v8 マスタへ足して半導体製造で解放し、電気汎用工作装置にあった入力3〜4種のレシピ12件をそちらへ移す。

**Architecture:** コード（C#）は変えない。新機械は既存の `ElectricMachine` ブロックタイプの1台として moorestech_master の v8 mod に定義し、12件のレシピの `blockGuid` だけを付け替える（レシピGUID・時間・入出力はそのまま）。クライアントには 7×4×5 の箱に入出力口の目印を付けた仮プレハブを `uloop execute-dynamic-code` で作って Addressable 登録し、本 repo のピンを master 側の push 済みコミットへ進める。

**Tech Stack:** moorestech_master（v8 mod の JSON・localization.csv・nodeGraph）、Unity Editor（PrefabUtility・Addressables を execute-dynamic-code で操作）、プレイテストDSL（unity-playmode-recorded-playtest）。

## Requirements

- R1: v8 `blocks.json` に `ElectricMachine` のブロック「電気精密組立機」を足す。受け入れ: `blockSize: [7, 4, 5]`、`blockPrefabAddressablesPath: Vanilla/Block/ElectricPrecisionAssembler`、`openable: true`、`requiredPower: 40`、`idlePowerRate: 0.2`、`inputSlotCount: 4`、`outputSlotCount: 2`、`moduleSlotCount: 4`、`allowQualityModule: false`、タンク0・液体コネクタ無し、配線パラメータ（`maxWireConnectionCount 2`・`connectionRange 30`・`connectionHeightRange 20`）は電気汎用工作装置と同じ、`initialUnlocked: false`、`category: 生産`・`subCategory: 電気機械`、`placementsPerCost: 1`、建設コスト 鋼鉄フレーム10・発展電子回路5・制御モータ4。
- R2: 入出力口。受け入れ: 入力4口が `offset` `[0,0,0]`・`[2,0,0]`・`[4,0,0]`・`[6,0,0]`、各 `directions: [[0,0,-1]]`（背面）。出力1口が `offset [3,0,4]`・`directions: [[0,0,1]]`（正面中央）。全口 `shapeGuid 65f063cd-3ae1-4b55-b39d-00e033b7ec98`、`connectorGuid` は新規 UUIDv7。
- R3: 下表12件の機械レシピの `blockGuid` を新ブロックへ付け替える。受け入れ: 12件の `blockGuid` が新ブロック GUID、`machineRecipeGuid`・`time`・`inputItems`・`outputItems`・`initialUnlocked`・`levelInheritance` は変化なし。電気汎用工作装置に入力3種以上のレシピが0件。
- R4: 研究「半導体製造」（`019e3ad9-46e2-73fd-9555-8c1d0245d26d`）の `unlockBlock` に新ブロックを足す。受け入れ: clearedActions の `unlockBlockGuids` に含まれる。12件のレシピ解放先は変えない。
- R5: 表示まわり。受け入れ: `localization.csv` に新ブロック名の行（日・英・独・韓）があり、半導体製造の研究説明文（`researchNodeDescription` と localization の同キー）が「電気精密組立機」の解放を含む。nodeGraph に新ブロックのノードが半導体製造の解放物として置かれ（既存ノードと重なり0）、master-refine 操作A でブロックの sortPriority が再計算されている。
- R6: クライアントに仮プレハブ `moorestech_client/Assets/AddressableResources/Block/ElectricPrecisionAssembler.prefab` を作り、`Vanilla Asset Group` に `Vanilla/Block/ElectricPrecisionAssembler` で登録する。受け入れ: 外形 7×4×5 の箱で、背面の入力4口と正面の出力1口の位置に色の違う目印がある。root と全子が Block レイヤーで、外形を覆う BoxCollider を root に1つ持つ。プレハブ・Addressable はエディタ経由（execute-dynamic-code）でだけ作る。
- R7: 本 repo の `.moorestech-external-revisions.json` の moorestech_master ピンを、Task 1 の push 済みコミットへ更新する。
- R8: unityプレイ録画テストで、電気精密組立機の背面4口にそれぞれベルトコンベアで別々の素材を流すと GPULv1 が1個出ることと、仮モデルが画面に出ることを確かめる。電気汎用工作装置で12件のレシピを使っていた既存シナリオ（tier9-2・tier9-7）を新機械へ書き換えて新マスタで通す。
- R9: moorestech の PR を作る（マージしない）。moorestech_master は master へ直接 push する。

やらないこと:
- C# のサーバー・クライアントのコード変更
- セーブ形式の変更とマイグレーション（セーブ済みの電気汎用工作装置が12件のどれかを選んでいても変換しない）
- 本番の3Dモデル（今回は仮プレハブだけ）
- 12件の加工時間・入出力の変更、2種以下のレシピの移動・複製
- テスト用 mod（`Tests.Module/TestMod/ForUnitTest`）の変更

## Global Constraints

- マスタ JSON の編集は scratchpad に書いたスクリプトで一括適用する。スクリプトは repo に置かない。
- JSON は既存ファイルのインデント・キー順・末尾改行を保って書き戻す（整形起因の全行差分を出さない）。
- 新しい GUID は UUIDv7 で採番する。
- Prefab・`.asset`・`.meta` をテキストで編集・作成しない。プレハブと Addressable 登録は `uloop execute-dynamic-code` 経由だけ。
- ピン変更は `git add .moorestech-external-revisions.json` 単独でコミットし、直後に `git show HEAD:.moorestech-external-revisions.json` で値を確かめる（Editor が書き戻すため）。
- `git add -A` を使わない（Editor が書き換える無関係ファイルを混ぜない）。
- 研究説明文は解放内容ベースの1行（裁定 `.decisions/2026-08-22-研究説明文は全ノードに解放内容ベースの1行を書く.md`）。
- Editor は自分の worktree のものだけを使う（`moores-wt status` で確認）。Editor は未起動なので Task 2 の前に `uloop launch` で起動する。

作業場所:
- 本 repo: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/electric-precision-assembler`（branch `feat/electric-precision-assembler`、origin/master 1259666c86 から）
- master 側: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/electric-precision-assembler`（Task 1 Step 1 で origin/master から detached で作り、master へ直接 push する）
- 対象 mod: `server_v8/mods/moorestechAlphaMod_8/`

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反3件・修正3件（修正後の再検査で追加違反2件・修正2件） / ベルト投入を分岐器経由に、nodeGraph を前例の3点の組（費用は書かない）に直し、ブループリント行を含む死活表を足した
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / 発火なし（ADR 0096 との食い違いも無し）

---

## 既知の値（moorestech_master origin/master a3d4208 時点の v8 マスタ）

| 名前 | GUID |
|---|---|
| 電気汎用工作装置（block） | `019e1225-f49b-7283-8835-428d903852f1` |
| 研究 半導体製造 | `019e3ad9-46e2-73fd-9555-8c1d0245d26d` |
| 鋼鉄フレーム・発展電子回路・制御モータ（item） | 名前で引く（名前が一意であることを assert） |
| 接続口の形 | `shapeGuid 65f063cd-3ae1-4b55-b39d-00e033b7ec98` |

移す12件（すべて現在は電気汎用工作装置）:

| 出力 | machineRecipeGuid | 入力種数 | time |
|---|---|---|---|
| クリーンルームフィルター | `01a17349-9327-763a-9219-5a23c8d8956b` | 3 | 4 |
| 品質モジュールLv1 | `01a167f3-bd88-747d-b30e-13f23f169ce4` | 3 | 10 |
| 速度モジュールLv1 | `01a167f3-bd8b-7732-badd-8c1a011f7c26` | 3 | 10 |
| 省エネモジュールLv1 | `01a167f3-bd8e-7525-af33-5cce09108374` | 3 | 10 |
| 生産性モジュールLv1 | `01a167f3-bd90-7307-91a6-e45243f99379` | 3 | 15 |
| GPULv1 | `01a167f3-bd94-76d2-ada6-617fdbf3d9b3` | 4 | 20 |
| GPU衛星Lv1 | `01a17396-e37c-707c-b04b-1e4e4e873be8` | 3 | 30 |
| GPU衛星Lv2 | `01a17396-e37c-707c-b04b-1e4f7ec3397f` | 3 | 30 |
| GPU衛星Lv3 | `01a17396-e37c-707c-b04b-1e50c2de1cc7` | 3 | 30 |
| GPU衛星Lv4 | `01a17396-e37c-707c-b04b-1e511be6c091` | 3 | 30 |
| GPU衛星Lv5 | `01a17396-e37c-707c-b04b-1e52e90163e1` | 3 | 30 |
| 反物質爆弾 | `01a167e9-21e2-7c4e-91be-8ad747466bde` | 4 | 60 |

### Task 1: v8 マスタに電気精密組立機を足し、12件のレシピを移す

**Files（master 側 worktree からの相対パス）:**
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/blocks.json`
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/machineRecipes.json`
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/research.json`
- Modify: `server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`
- Modify: `server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json`

**Interfaces:**
- Consumes: なし
- Produces: 新ブロック GUID（以下 `EPA_BLOCK`）、ブロック名「電気精密組立機」（Task 3 のシナリオが `PlaceBlockDirect("電気精密組立機", …)` で引く）、master の push 済みコミット（Task 2 のピンの値）。

- [ ] **Step 1: master 側の worktree を作る**

```bash
git -C /Users/sakastudio/hermes-agent/data/repos/moorestech_master fetch origin
git -C /Users/sakastudio/hermes-agent/data/repos/moorestech_master worktree add --detach /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/electric-precision-assembler origin/master
```

- [ ] **Step 2: 適用スクリプトを scratchpad に書いて実行する**

scratchpad（例 `<scratchpad>/apply_precision_assembler.py`）に置き、`server_v8/mods/moorestechAlphaMod_8/master` を引数で受ける。

```python
import json, sys, uuid, time, os, re
M = sys.argv[1]
def path(n): return os.path.join(M, n + '.json')
RAW = {n: open(path(n), encoding='utf-8').read() for n in ['blocks', 'machineRecipes', 'research', 'items']}
def indent(n): return len(re.match(r'\{\n( *)', RAW[n]).group(1))
def save(n, d):
    out = json.dumps(d, ensure_ascii=False, indent=indent(n)) + ('\n' if RAW[n].endswith('\n') else '')
    open(path(n), 'w', encoding='utf-8').write(out)
def uuid7():
    b = bytearray(int(time.time() * 1000).to_bytes(6, 'big') + os.urandom(10))
    b[6] = (b[6] & 0x0F) | 0x70; b[8] = (b[8] & 0x3F) | 0x80
    return str(uuid.UUID(bytes=bytes(b)))
items, blocks, recipes, research = (json.loads(RAW[n]) for n in ['items', 'blocks', 'machineRecipes', 'research'])
def one(seq, key, value):
    hit = [x for x in seq if x[key] == value]; assert len(hit) == 1, (key, value, len(hit)); return hit[0]
item = lambda name: one(items['data'], 'name', name)['itemGuid']
CRAFTER = '019e1225-f49b-7283-8835-428d903852f1'
SEMI = '019e3ad9-46e2-73fd-9555-8c1d0245d26d'
SHAPE = '65f063cd-3ae1-4b55-b39d-00e033b7ec98'
MOVE = ['01a17349-9327-763a-9219-5a23c8d8956b', '01a167f3-bd88-747d-b30e-13f23f169ce4', '01a167f3-bd8b-7732-badd-8c1a011f7c26',
        '01a167f3-bd8e-7525-af33-5cce09108374', '01a167f3-bd90-7307-91a6-e45243f99379', '01a167f3-bd94-76d2-ada6-617fdbf3d9b3',
        '01a17396-e37c-707c-b04b-1e4e4e873be8', '01a17396-e37c-707c-b04b-1e4f7ec3397f', '01a17396-e37c-707c-b04b-1e50c2de1cc7',
        '01a17396-e37c-707c-b04b-1e511be6c091', '01a17396-e37c-707c-b04b-1e52e90163e1', '01a167e9-21e2-7c4e-91be-8ad747466bde']

# 1) ブロック（電気汎用工作装置の param を写し、口と大きさ・電力・費用だけ変える）
crafter = one(blocks['data'], 'blockGuid', CRAFTER)
EPA_BLOCK = uuid7()
param = json.loads(json.dumps(crafter['blockParam']))
param['requiredPower'] = 40
param['inventoryConnectors'] = {
    "inputConnects": [{"connectorGuid": uuid7(), "offset": [x, 0, 0], "directions": [[0, 0, -1]], "shapeGuid": SHAPE} for x in (0, 2, 4, 6)],
    "outputConnects": [{"connectorGuid": uuid7(), "offset": [3, 0, 4], "directions": [[0, 0, 1]], "shapeGuid": SHAPE}]}
assert param['inputSlotCount'] == 4 and param['outputSlotCount'] == 2 and param['moduleSlotCount'] == 4 and param['allowQualityModule'] is False
blocks['data'].append({
    "blockGuid": EPA_BLOCK, "name": "電気精密組立機", "blockType": "ElectricMachine",
    "blockSize": [7, 4, 5], "blockPrefabAddressablesPath": "Vanilla/Block/ElectricPrecisionAssembler", "openable": True,
    "blockParam": param, "initialUnlocked": False,
    "requiredItems": [{"itemGuid": item('鋼鉄フレーム'), "count": 10}, {"itemGuid": item('発展電子回路'), "count": 5}, {"itemGuid": item('制御モータ'), "count": 4}],
    "placementsPerCost": 1, "sortPriority": crafter['sortPriority'], "category": "生産", "subCategory": "電気機械"})

# 2) 12件の blockGuid を付け替える
for g in MOVE:
    r = one(recipes['data'], 'machineRecipeGuid', g)
    assert r['blockGuid'] == CRAFTER and len(r['inputItems']) >= 3, g
    r['blockGuid'] = EPA_BLOCK
assert not [r for r in recipes['data'] if r['blockGuid'] == CRAFTER and len(r['inputItems']) >= 3]

# 3) 半導体製造でブロックを解放
semi = one(research['data'], 'researchNodeGuid', SEMI)
unlock = [a for a in semi['clearedActions'] if a['gameActionType'] == 'unlockBlock']
assert len(unlock) == 1
unlock[0]['gameActionParam']['unlockBlockGuids'].append(EPA_BLOCK)

for n, d in [('blocks', blocks), ('machineRecipes', recipes), ('research', research)]: save(n, d)
print('EPA_BLOCK', EPA_BLOCK)
```

Run: `python3 -I <scratchpad>/apply_precision_assembler.py /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/electric-precision-assembler/server_v8/mods/moorestechAlphaMod_8/master`
Expected: `EPA_BLOCK` が出る。`git diff --stat` で3ファイルだけが変わり、machineRecipes.json の差分が12行（blockGuid の行だけ）。整形起因の全行差分があればインデント検出を直してやり直す。

- [ ] **Step 3: 名前と研究説明文を書く**

`localization.csv`（列: `key,Source,english,japanese,german,korean`）を scratchpad のスクリプトで書き換える（csv モジュールで読み書きし、既存行の引用符の付け方を保つ。差分が対象行だけになることを `git diff` で確かめる）。
- 追加: `block.<EPA_BLOCK>.name,電気精密組立機,Electric Precision Assembler,電気精密組立機,Elektrischer Präzisionsmonteur,전기 정밀 조립기`
- `research.019e3ad9-46e2-73fd-9555-8c1d0245d26d.description`（半導体製造）: 現在の文（「シリコンウェハからICチップを作るクリーンルームと半導体製造装置を解放する」）の末尾を Source/japanese「シリコンウェハからICチップを作るクリーンルームと半導体製造装置、入力4口の電気精密組立機を解放する」にし、english・german・korean は現行訳の末尾（english の現行は「Unlocks the clean room and the chip line: wafer slicing, EUV, dicing and packaging」で日本語と構造が違う）に、english「, and the 4-input Electric Precision Assembler」、german「 sowie den elektrischen Präzisionsmonteur mit 4 Eingängen」、korean「 및 입력 4구의 전기 정밀 조립기」を句点の前へ足す（文末の句読点は現行のまま残す）。
- `research.json` の半導体製造の `researchNodeDescription` も Source と同じ文にする。

- [ ] **Step 4: nodeGraph にノードを置き、操作Aを回す**

- `nodeGraph.v1.json`（ノードのリスト）に、前例（データセンターの master コミット `90ed453`、plan `docs/superpowers/plans/2026-11-03-ground-data-center.md` Task 1 Step 4。半導体製造の既存 block ノード `…-0f251db2-…` も同じ組）と同じ3点の組を足す: (a) block ノード `{"id": "node-sync-019e3ad9-46e2-73fd-9555-8c1d0245d26d-<EPA_BLOCK>", "position": …, "type": "block", "masterGuid": "<EPA_BLOCK>"}`、(b) `placeholder-for-node-sync-…` の placeholder ノード、(c) `edge-placeholder-node-sync-…` の buildCost 辺。(b)(c) の形は前例の組をそのまま写す（placeholder は `{"type":"placeholder","text":""}`、辺は `{id, source, target, edgeType:"buildCost"}` だけ。費用は nodeGraph に書かない）。位置は半導体製造の block ノードの縦列（x=21960、y=440〜840）の直下に y を40刻みで空きを探して置き、placeholder はデータセンターの3点組と同じ相対位置（block の x−240）に置く。placeholder ノードも含めて既存ノードと矩形が重ならないことをスクリプトで確かめる（ノードの大きさは同じ type の既存ノード間の最小間隔から求める）。
- master-refine スキルの操作A（手順は本 repo の `.agents/skills/master-refine/SKILL.md`）を v8 に対して、master 側 worktree を対象に実行する。
Expected: 新ブロックの `sortPriority` が操作Aの結果で決まり、`git diff` で他ブロック・品目の変化が操作Aの並べ替えだけである。

- [ ] **Step 5: 値を確かめる**

scratchpad の確認スクリプトで R1〜R5 の値を全件 assert する（ブロックの全 param と口の offset・direction、12件の blockGuid と他フィールド不変（origin/master の値と比較）、工作装置に入力3種以上が0件、半導体製造の unlockBlockGuids、localization の行）。
Expected: assert 全通過。

- [ ] **Step 6: master へ直接 push する**

```bash
cd /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/electric-precision-assembler
git add server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json
git commit -m "v8: 入力4口の電気精密組立機を半導体製造で解放し、入力3〜4種のレシピ12件を電気汎用工作装置から移す（EPA_BLOCK=<GUID>）"
git fetch origin && git rebase origin/master && git push origin HEAD:master
```
push 済みコミットの SHA を控える（Task 2 Step 3 で使う）。

### Task 2: 仮プレハブを作り、ピンを進める

**Files:**
- Create（Editor 経由）: `moorestech_client/Assets/AddressableResources/Block/ElectricPrecisionAssembler.prefab`、`moorestech_client/Assets/AddressableResources/Material/ElectricPrecisionAssembler/EPA_Body.mat`・`EPA_Input.mat`・`EPA_Output.mat`（`.meta` は Unity が作る）
- Modify（Editor 経由）: `moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset`
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: Task 1 の push 済みコミットと `blockPrefabAddressablesPath: Vanilla/Block/ElectricPrecisionAssembler`
- Produces: Addressable アドレス `Vanilla/Block/ElectricPrecisionAssembler`、新しいピン

- [ ] **Step 1: Editor を起動し疎通を確かめる**

Run: `uloop launch /Users/sakastudio/hermes-agent/data/worktrees/moorestech/electric-precision-assembler/moorestech_client` → `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0。

- [ ] **Step 2: プレハブを作って登録する**

前例: データセンターの仮プレハブ（コミット `45af3db96d`、`AddressableResources/Block/DataCenter.prefab`。plan `docs/superpowers/plans/2026-11-03-ground-data-center.md` Task 2）。ブロックの原点が「ブロックの最小角」であることを DataCenter.prefab の BoxCollider（center 1.5,1.5,2.5 / size 3,3,5）で確かめてから、同じ基準で置く。

`uloop execute-dynamic-code --project-path ./moorestech_client` で次を実行する:

```csharp
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

var outputPath = "Assets/AddressableResources/Block/ElectricPrecisionAssembler.prefab";
var root = new GameObject("ElectricPrecisionAssembler") { layer = LayerMask.NameToLayer("Block") };
var shader = Shader.Find("Universal Render Pipeline/Lit");
var body = new Material(shader) { color = new Color(0.55f, 0.58f, 0.62f) };
var input = new Material(shader) { color = new Color(0.15f, 0.45f, 0.9f) };
var output = new Material(shader) { color = new Color(0.95f, 0.55f, 0.1f) };
AssetDatabase.CreateFolder("Assets/AddressableResources/Material", "ElectricPrecisionAssembler");
AssetDatabase.CreateAsset(body, "Assets/AddressableResources/Material/ElectricPrecisionAssembler/EPA_Body.mat");
AssetDatabase.CreateAsset(input, "Assets/AddressableResources/Material/ElectricPrecisionAssembler/EPA_Input.mat");
AssetDatabase.CreateAsset(output, "Assets/AddressableResources/Material/ElectricPrecisionAssembler/EPA_Output.mat");

// クリック用の当たり判定は外形を覆う BoxCollider 1つを Block レイヤーで焼き込む（前例 DataCenter.prefab）
var click = root.AddComponent<BoxCollider>();
click.center = new Vector3(3.5f, 2f, 2.5f);
click.size = new Vector3(7f, 4f, 5f);

GameObject Box(string name, Vector3 center, Vector3 size, Material material)
{
    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
    box.name = name;
    box.layer = root.layer;
    Object.DestroyImmediate(box.GetComponent<BoxCollider>());
    box.transform.SetParent(root.transform, false);
    box.transform.localPosition = center;
    box.transform.localScale = size;
    box.GetComponent<MeshRenderer>().sharedMaterial = material;
    return box;
}

// 本体は外形より少し内側の箱。背面 z=0 の x=0,2,4,6 に入力口、正面 z=5 の x=3 に出力口の目印を出す
Box("Body", new Vector3(3.5f, 1.9f, 2.5f), new Vector3(6.8f, 3.8f, 4.6f), body);
foreach (var x in new[] { 0, 2, 4, 6 }) Box($"Input_{x}", new Vector3(x + 0.5f, 0.5f, 0.1f), new Vector3(0.8f, 0.8f, 0.2f), input);
Box("Output", new Vector3(3.5f, 0.5f, 4.9f), new Vector3(0.8f, 0.8f, 0.2f), output);

PrefabUtility.SaveAsPrefabAsset(root, outputPath);
Object.DestroyImmediate(root);

var settings = AddressableAssetSettingsDefaultObject.Settings;
var group = settings.FindGroup("Vanilla Asset Group");
var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(outputPath), group, false, false);
entry.address = "Vanilla/Block/ElectricPrecisionAssembler";
settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true, true);
AssetDatabase.SaveAssets();
Debug.Log("ElectricPrecisionAssembler registered: " + entry.address);
```

Run 後: `grep -n "Vanilla/Block/ElectricPrecisionAssembler" "moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset"` が1件、prefab・mat 3つと各 `.meta`（フォルダの `.meta` を含む）がある。`grep -c "m_Layer: 0" moorestech_client/Assets/AddressableResources/Block/ElectricPrecisionAssembler.prefab` が 0。

- [ ] **Step 3: プレハブをコミットする**

```bash
git add moorestech_client/Assets/AddressableResources/Block/ElectricPrecisionAssembler.prefab moorestech_client/Assets/AddressableResources/Block/ElectricPrecisionAssembler.prefab.meta moorestech_client/Assets/AddressableResources/Material/ElectricPrecisionAssembler moorestech_client/Assets/AddressableResources/Material/ElectricPrecisionAssembler.meta "moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset"
git commit -m "電気精密組立機の仮プレハブ（入出力口の目印付きの箱）を Vanilla/Block/ElectricPrecisionAssembler で登録する"
```
`Vanilla Asset Group.asset` の差分が1エントリ追加だけであることを `git show HEAD -- "moorestech_client/Assets/AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset"` で確かめる。

- [ ] **Step 4: ピンを進める**

`.moorestech-external-revisions.json` の `moorestech_master.commitHash` を Task 1 Step 6 の push 済み SHA にする。

```bash
git add .moorestech-external-revisions.json
git commit -m "moorestech_master のピンを電気精密組立機の追加コミットへ進める"
git show HEAD:.moorestech-external-revisions.json
```
Expected: commitHash が Task 1 の SHA と一致する。続けて `uloop compile --project-path ./moorestech_client` が ErrorCount 0。v8 出荷マスタを読む自動テストは無いので、v8 のロード確認は Task 3 の録画テストで行う（ここでコンパイルだけで完了扱いにしない）。

### Task 3: unityプレイ録画テスト

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-9-precision-assembler.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-2-modules-gpu.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-7-antimatter-final-research.cs`

**Interfaces:**
- Consumes: ブロック名「電気精密組立機」「ベルトコンベア」、品目名「ICチップLv1」「コンピュータ」「鋼鉄フレーム」「被覆銅線」「GPULv1」、研究 半導体製造 `019e3ad9-46e2-73fd-9555-8c1d0245d26d`・GPU設計 `01a167f3-bd74-73f8-8c43-1869db833299`、Task 2 のピン
- Produces: 録画・result.json（worktree の gitignore 下）

- [ ] **Step 1: 新シナリオを書く**

前例は `scenarios/tier9/tier9-8-data-center.cs`（研究の閉包・機械設置・給電・レシピ選択・UI撮影）と `scenarios/tier9/tier9-1b-mixed-level-sorting.cs`（ベルトへの直接投入は tick 内でしか受け付けないため、上流に置いた `フィルター分岐器` へ `InsertItem(stack, InsertItemContext.Empty)` で入れ、分岐器がブロック更新でベルトへ押す。`InsertIntoSplitter` が戻り値0まで入れ直す）。tier9-8 のファイルを写して次だけを変える。200 行未満に収める。

- シナリオ名 `tier9-9-precision-assembler`。研究は GPU設計（前提閉包で半導体製造も入る）を完了扱いにし、`WaitForResearch` で確かめる。
- 機械は `PlaceBlockDirect("電気精密組立機", new Vector3Int(0, 32, 0), BlockDirection.North)`。レシピは `MasterHolder.MachineRecipesMaster.MachineRecipes.Data.Single(r => r.MachineRecipeGuid == Guid.Parse("01a167f3-bd94-76d2-ada6-617fdbf3d9b3"))`（GPULv1）で、`r.BlockGuid` が電気精密組立機の GUID であることを Assert する。
- 背面の4口へ素材を流す: x = 0,2,4,6 の各列に、`new Vector3Int(x, 32, -2)` へ `PlaceBlockDirect("フィルター分岐器", …, BlockDirection.North)`、`new Vector3Int(x, 32, -1)` へ `PlaceBlockDirect("ベルトコンベア", …, BlockDirection.North)` を置く（分岐器 → ベルト → 機械の背面口）。列 i の分岐器にはレシピの `InputItems[i]` の品目を `Count` 個、1個ずつ tier9-1b の `InsertIntoSplitter` と同じ形（`RunOnServer` 内で `splitter.InsertItem(ServerContext.ItemStackFactory.Create(id, 1), InsertItemContext.Empty)`、戻り値の Count が 0 になるまで tick 末尾で入れ直す。IPA の補充部分は不要なので外す）で入れる。ベルトへ直接 InsertItem しない（`BeltTransportDatastore` が tick 外の押し込みを拒否する）。
- `p.Until(() => Count(gpuLv1) == 1, recipe.Time + 60f, "GPULv1 が1個出た")` で出力を待つ。待つ前と途中で発電機を `Refuel()` する。
- スクショ `01-precision-assembler-placed`（設置演出後、背面のベルト4本と機械が画面に入る位置から）と `02-gpu-output`（tier9-8 の `ShootMachine` を写し、照準は正面中央 `origin.x + 3.5f, 33f, origin.z + 5f` を北側から狙う形に変える。ワープ先は `origin.z + 6f`）。

- [ ] **Step 2: 既存シナリオを新機械へ書き換える**

- `tier9-2-modules-gpu.cs`: 「電気汎用工作装置」を5台置いてモジュール4種と GPULv1 のレシピを選んでいる箇所（58〜62行付近）を「電気精密組立機」に変え、配置間隔を `i * 4` から `i * 8` にする（幅7のため）。コメント・Note の機械名も直す。給電の配線距離が足りなければ電柱の位置を中央へ寄せる。
- `tier9-7-antimatter-final-research.cs`: 機械は56行付近で `GetBlockMaster(bombRecipe.BlockGuid).Name` によりレシピから引いているので、マスタの変更だけで電気精密組立機に追従する。名前の直書きに置き換えない。機械の呼び名（コメント・Note・RunOnServer の見出しと確認文・変数名 `workbench` 等、ファイル内の「工作装置」すべて）を電気精密組立機に合わせて直す（名前は実処理と一致させる規約）。配置位置 (8,32,0) は 7×4×5 でも反物質製造装置（x 2〜4）と重ならないので動かさない（実行して重なりで設置に失敗したら、そのときだけ x を増やす）。
- 両ファイルで、12件以外のレシピ（入力2種以下）を工作装置で作っている箇所があれば工作装置のまま残す。
- `scenarios/tier9/task31-r3/t31r3b-cable-output-space-acceptance.cs` も「電気汎用工作装置」を使っている。そこで選ぶレシピが12件に入っていないことを確かめ、入っていなければ変えない（入っていれば同様に書き換える）。

- [ ] **Step 3: 実行する**

unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh` で tier9-9-precision-assembler・tier9-2-modules-gpu・tier9-7-antimatter-final-research を実行する（Editor は自分の worktree のもの。第3引数は付けずピンから自動解決させ、解決できなければ master 側 worktree の `server_v8` を明示する）。
Expected: 3本とも result.json が成功。tier9-9 の録画で、仮モデルの背面の青い目印4つにベルトが1本ずつ刺さり、出力欄に GPULv1 1個が見える。録画とスクショは目で見て確かめ、見たものを報告に書く。

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-9-precision-assembler.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-2-modules-gpu.cs .agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-7-antimatter-final-research.cs
git commit -m "電気精密組立機の背面4口にベルトで素材を流すと GPU が出る録画シナリオを足し、既存の tier9 シナリオを新機械へ移す"
```
テストで `Saves/` に作ったワールドは作業の終わりに `list-work-saves.sh` で探して消す。

### Task 4: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。Codex ホストでは `references/codex-host.md` の runner で実行する。master 側のコミット（Task 1）の差分もレビュー対象に含める。レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、反映後のバイナリで Task 3 の録画テストを再実施してから完了とする。master 側の修正が要れば master へ直接 push し、ピンを進め直す。

### Task 5: PR 作成とセッション終了可能化

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確かめて終える。本 repo のピンが moorestech_master の master 上の push 済みコミットを指していることを確かめ、PR 本文に master 側コミットの URL を書く。PR 本文の成果物の載せ先は環境ローカル規約（CLAUDE.local.md「PR の成果物の載せ先」）に従う。moorestech の PR はマージしない。master 側の worktree を `git worktree remove` で片付ける。

## 死活表（Phase 2.5。レシピ12件の機械移行）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 電気汎用工作装置で12件を作る | 死ぬ（意図どおり） | ユーザー裁定 `.decisions/2026-11-04-入力3〜4種のレシピは半導体製造で解放する電気精密組立機へ移す.md` |
| 研究の進行（12件を作れるようになる時期） | 生きる | 12件の解放研究はすべて半導体製造かその下流。新機械も半導体製造で解放 |
| 12件を選んだ工作装置を含む旧セーブのロード | 生きる | `MachineSaveRestorer` の復元は BlockGuid を照合しない（ADR 0096 の agent 前提どおり、選び直すまで工作装置で作れる） |
| 旧セーブの工作装置で同じレシピを選び直す | 不可（意図どおり） | 選択時の `MachineRecipeSelectionUtil` が `RecipeBlockMismatch` で拒否 |
| 12件を選んだ工作装置を含むブループリントの貼り付け | 退化（未選択で設置。意図どおり） | 12件を移す裁定の帰結。`MachineRecipeBlueprintSettingsComponent` → `MachineRecipeSelectionUtil` が `RecipeBlockMismatch` で弾く |
| 既存シナリオ tier9-1a・1b・3・4・6、misc の cleanroom 系、t31r3b | 生きる | 産物を直接入れている／入力1種レシピで12件に含まれない |
| tier9-2・tier9-7 | 書き換えで生きる | Task 3 Step 2 |

## 判断記録（ADR）

- 設計 ADR: `docs/adr/0096-electric-precision-assembler-for-three-to-four-inputs.md`。ユーザー裁定は `.decisions/2026-11-04-入力3〜4種のレシピは半導体製造で解放する電気精密組立機へ移す.md`。
- moorestech_master はブランチ・PR を作らず master へ直接 push する。出所: ユーザー裁定 2026-11-04 原文「masterに直push更新でok」（同セッションのクリーンルーム関連のマスタ変更の指示で出たもの。本機能も同じバランス調整の続きとして当てはめた）。moorestech 本体は仮プレハブとシナリオを含むので PR を作る（AGENTS.md・plan 必須の閉じタスク）。
- 録画テストは必須（ブロックの設置・見た目・ベルトからの搬入・機械の加工というランタイム挙動に触れる）。新規 tier9-9 と、12件のレシピを工作装置で作っていた tier9-2・tier9-7 を実行する。
- 仮プレハブは DataCenter と同じ作り方（execute-dynamic-code・Block レイヤー・外形 BoxCollider）。入出力口に色違いの目印を付けるのは、仮の箱でも口の位置が見えないとベルトを繋げないため。出所: agent前提（ユーザー裁定「仮の箱で先に出す」の具体化）。
- 旧セーブで12件を選んだままの電気汎用工作装置は変換しない（C# を変えない）。出所: ユーザー裁定 2026-11-04 選択「何もしない」（対案「ロード時に選択を外す」を棄却。user-simulator review 判事の予測は的中、preanswer 判事の予測は外れ）。
- user-simulator review の Warning 2件（english 説明文の足し方、nodeGraph の置き場所）は plan に適用済み。出所: シミュレーター予測（適用は agent 判断）。
- 新ブロックの param は電気汎用工作装置の `blockParam` を写して `requiredPower` と `inventoryConnectors` だけ変える。出所: agent前提（ADR 0096 の「残りの値は工作装置に揃える」）。
