# 液体数値のSatisfactory比率合わせ Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** v8 マスタの液体まわりの数値を、原油系を水・蒸気の桁へ縮めてパイプ律速の比率にそろえ、moorestech_master のPRと本repoのピン更新PRを出す。

**Architecture:** コード・スキーマ・セーブ形式は変えない。`../moorestech_master-wt-fluid`（branch `feat/fluid-rates-satisfactory-ratio`、土台 origin/master 74a447e＋手元調整コミット c7ba2d7）の `blocks.json`・`machineRecipes.json`・`train.json` を、GUIDで要素を特定するPythonスクリプト1本で書き換える。JSONは `json.dumps(ensure_ascii=False, indent=2)` で元ファイルと1バイト違わず往復することを実測済み（末尾改行なし）なので、差分は値の行だけになる。本repo（`../moorestech-wt-fluid`、branch `feat/fluid-rates-satisfactory-ratio`）は `.moorestech-external-revisions.json` の moorestech_master ピンだけを更新する。

**Tech Stack:** Python 3（`-I`）、jq、moorestech_master JSON、uloop、unity-playmode-recorded-playtest、gh

## Requirements

1. 石油蒸留機レシピ（`019ee38b-1c1a-71ca-9b2e-b35d2a6f9a23`）は 原油10 → ナフサ2 / ガソリン3 / 残油5、time 5。受け入れ: machineRecipes.json の該当要素がこの値
2. 油井（block `019ee389-bf73-73ef-979c-ca7e25697558`）の generateFluid は 原油 amount 2 / generateTime 0.5
3. 鉄のパイプ（`7d35321c-98b7-49db-8fc7-6f23854dd1c7`）の全接続口 flowCapacity 10、鋼鉄のパイプ（`019f0d29-e72e-779a-893a-144cf32f4521`）は 30
4. 液体の接続口を持つ機械（ボイラー・蒸気機関・歯車ポンプ・油井・石油蒸留機・化学プラント・ガソリンエンジン発電機・液体タンク・液体プラットフォーム・クリーンルームパイプコネクタ）の全接続口 flowCapacity 60。ボイドパイプ（1000）は変えない
5. 貨車（trainCar `f998f54b-7619-491c-a100-ee88955f9058`）fluidCapacity 5000（手元の10000を上書き）、液体プラットフォーム capacity 5000、液体タンク capacity 5000 据え置き
6. ガソリンエンジン発電機の fuelFluids はガソリン amount 1 / time 4 / power 1000
7. 化学プラントのレシピは time と出力を据え置き、入力液体量だけ: ゴム ナフサ0.5、プラスチック ナフサ0.5、アスファルト 残油2、固形燃料 残油1
8. 内部タンク: 油井・石油蒸留機・化学プラントの innerTankCapacity 30、ガソリンエンジン発電機 fuelFluidTankCapacity 20。パイプ容量50・ボイラー・歯車ポンプのタンクは変えない
9. 手元調整コミット c7ba2d7（液体プラットフォームのプレハブ Station_Fluid_Cargo 化・建設コスト変更・パイプ placementsPerCost 1→3）をPRに含める。nodeGraph の視点変更は含めない（このブランチには入っていない）
10. moorestech_master の変更は push して PR を作り、本repoのピンはそのPRが指す push 済みコミットを指す。受け入れ: 両PRが作成済みでマージ可能
11. v8 マスタが Unity でエラーなくロードされ、MasterHolder 上の値が上記と一致する（unityプレイ録画テストで確認）

やらないこと:
- 蒸気機関の蒸気消費（0.1/秒）は変えない（別タスクに積み済み）
- 既存セーブの換算マイグレーションは書かない。CurrentVersion も上げない（在庫は数量のまま。超過分はロード時の切り詰めに従う）
- C# コード・VanillaSchema は触らない。貨車容量0のバグ moorestech-qwz1 のコード側根治もしない
- 列車の往復時間・積み下ろし速度（loadingSpeed）は変えない

## Global Constraints

- 対象 mod は `server_v8/mods/moorestechAlphaMod_8/master/` のみ
- JSON の書き換えは下記スクリプトで行い、手で整形し直さない（`json.dumps(..., ensure_ascii=False, indent=2)`、末尾改行なし）
- 整数で決まった値は int、小数は float で書く（0.5 は `0.5`、2 は `2`）
- 別repoの変更も push して PR を作る。ピンは push 済みコミットを指す（AGENTS.md）
- PR は通常のマージコミット前提（Squash しない）
- 新規 worktree で Unity を起動するときは、メインworktree（`/Users/katsumi/moorestech/moorestech_client/Library`）から Library をコピーする。Library は削除しない

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 型・メンバー・機構の新設なし（マスタ数値のみ）。死活表: 既存の液体操作はすべて存続
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / 発火なし（型・API新設なし、ADR 0084と全値一致）

---

### Task 1: v8 マスタの液体数値を書き換える

**Files:**
- Modify: `../moorestech_master-wt-fluid/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`
- Modify: `../moorestech_master-wt-fluid/server_v8/mods/moorestechAlphaMod_8/master/machineRecipes.json`
- Modify: `../moorestech_master-wt-fluid/server_v8/mods/moorestechAlphaMod_8/master/train.json`

**Interfaces:**
- Consumes: なし
- Produces: moorestech_master のコミット（Task 2 で push・ピンに使う）

- [ ] **Step 1: 作業場所を確認する**

```bash
cd /Users/katsumi/moorestech_master-wt-fluid && pwd && git branch --show-current && git log --oneline -2
```
Expected: branch `feat/fluid-rates-satisfactory-ratio`、先頭が `c7ba2d7 chore: 液体プラットフォームのプレハブ…`、その下が `74a447e`。違えば止めて報告する

- [ ] **Step 2: 書き換えスクリプトを実行する**

```bash
cd /Users/katsumi/moorestech_master-wt-fluid/server_v8/mods/moorestechAlphaMod_8/master && python3 -I - <<'EOF'
import json

def load(path):
    with open(path, encoding='utf-8') as f:
        return json.load(f)

def save(path, data):
    with open(path, 'w', encoding='utf-8') as f:
        f.write(json.dumps(data, ensure_ascii=False, indent=2))

CRUDE = '41407950-bb67-4d7a-a718-a7c5c04a5358'
NAPHTHA = '019eded2-657c-74b9-a5b8-4ebc3ca56a9b'
GASOLINE = '019eded2-7454-76da-8943-84c4e7a59179'
RESIDUE = '019eded2-7ffb-7070-82e9-1cb10463a68c'

BOILER = 'eec173df-b64e-4311-8066-f90446bc5f82'
STEAM_ENGINE = '532b0907-ab1a-46e6-b4d7-ed6363544e34'
IRON_PIPE = '7d35321c-98b7-49db-8fc7-6f23854dd1c7'
GEAR_PUMP = '46725c68-888b-4346-a650-db7738cbc9e1'
OIL_WELL = '019ee389-bf73-73ef-979c-ca7e25697558'
DISTILLER = '019ee389-d868-73f5-9c30-815712be325e'
CHEM_PLANT = '019ee880-7f68-705e-b846-7b05036e2ca8'
GAS_GENERATOR = '019eeaa6-71c4-739f-8eb8-509ea9af13d3'
FLUID_TANK = '019eeaa5-6ee3-7079-b3a6-42d9e3f03e9b'
FLUID_PLATFORM = '019f0d13-4d21-7099-b0b1-8893376d22a5'
STEEL_PIPE = '019f0d29-e72e-779a-893a-144cf32f4521'
CLEANROOM_HATCH = '8b90269f-8cbc-4613-959d-279986f8ba5f'
WAGON = 'f998f54b-7619-491c-a100-ee88955f9058'

FLOW = {IRON_PIPE: 10, STEEL_PIPE: 30}
for g in (BOILER, STEAM_ENGINE, GEAR_PUMP, OIL_WELL, DISTILLER, CHEM_PLANT,
          GAS_GENERATOR, FLUID_TANK, FLUID_PLATFORM, CLEANROOM_HATCH):
    FLOW[g] = 60

# blocks.json
blocks = load('blocks.json')
by_guid = {b['blockGuid']: b for b in blocks['data']}
for guid, flow in FLOW.items():
    connectors = by_guid[guid]['blockParam']['fluidInventoryConnectors']
    ports = connectors['inflowConnects'] + connectors['outflowConnects']
    assert ports, guid
    for port in ports:
        port['option']['flowCapacity'] = flow

well = by_guid[OIL_WELL]['blockParam']
assert [x['fluidGuid'] for x in well['generateFluid']] == [CRUDE]
well['generateFluid'][0]['amount'] = 2
well['generateFluid'][0]['generateTime'] = 0.5
well['innerTankCapacity'] = 30
by_guid[DISTILLER]['blockParam']['innerTankCapacity'] = 30
by_guid[CHEM_PLANT]['blockParam']['innerTankCapacity'] = 30

gen = by_guid[GAS_GENERATOR]['blockParam']
assert [x['fluidGuid'] for x in gen['fuelFluids']] == [GASOLINE]
gen['fuelFluids'][0].update({'amount': 1, 'time': 4, 'power': 1000})
gen['fuelFluidTankCapacity'] = 20

by_guid[FLUID_PLATFORM]['blockParam']['capacity'] = 5000
assert by_guid[FLUID_TANK]['blockParam']['capacity'] == 5000
save('blocks.json', blocks)

# machineRecipes.json
recipes = load('machineRecipes.json')
by_recipe = {r['machineRecipeGuid']: r for r in recipes['data']}

distill = by_recipe['019ee38b-1c1a-71ca-9b2e-b35d2a6f9a23']
distill['time'] = 5
assert [x['fluidGuid'] for x in distill['inputFluids']] == [CRUDE]
distill['inputFluids'][0]['amount'] = 10
outputs = {NAPHTHA: 2, GASOLINE: 3, RESIDUE: 5}
assert sorted(x['fluidGuid'] for x in distill['outputFluids']) == sorted(outputs)
for x in distill['outputFluids']:
    x['amount'] = outputs[x['fluidGuid']]

# 化学プラント: time と出力は据え置き、入力液体量だけ変える
chem_inputs = {
    '019ee849-5e17-75d4-bdd3-c23840a4b110': (NAPHTHA, 0.5),  # ゴム
    '019ee865-a643-766f-8475-7496c563d15d': (NAPHTHA, 0.5),  # プラスチック
    '019ee86a-9769-716c-bc3f-488c558a6f44': (RESIDUE, 2),    # アスファルト
    '019ee87f-8957-740d-a226-e1f94edd594c': (RESIDUE, 1),    # 固形燃料
}
for guid, (fluid, amount) in chem_inputs.items():
    r = by_recipe[guid]
    assert [x['fluidGuid'] for x in r['inputFluids']] == [fluid], guid
    r['inputFluids'][0]['amount'] = amount
save('machineRecipes.json', recipes)

# train.json
train = load('train.json')
wagon = [c for c in train['trainCars'] if c['trainCarGuid'] == WAGON]
assert len(wagon) == 1
wagon[0]['fluidCapacity'] = 5000
save('train.json', train)
print('ok')
EOF
```
Expected: `ok`（assert で落ちたら、マスタが想定と違うので止めて報告する）

- [ ] **Step 3: 差分が値の行だけであることを確認する**

```bash
cd /Users/katsumi/moorestech_master-wt-fluid && git diff --stat && git diff | grep '^[-+] ' | grep -vE '"(flowCapacity|amount|generateTime|innerTankCapacity|fuelFluidTankCapacity|capacity|time|power|fluidCapacity)":' 
```
Expected: 3ファイルのみ変更、2つ目のコマンドの出力は空（値以外の行が変わっていない）

- [ ] **Step 4: 比率を検算する**

```bash
cd /Users/katsumi/moorestech_master-wt-fluid/server_v8/mods/moorestechAlphaMod_8/master && python3 -I - <<'EOF'
import json
b = {x['blockGuid']: x['blockParam'] for x in json.load(open('blocks.json', encoding='utf-8'))['data']}
r = {x['machineRecipeGuid']: x for x in json.load(open('machineRecipes.json', encoding='utf-8'))['data']}
d = r['019ee38b-1c1a-71ca-9b2e-b35d2a6f9a23']
crude = d['inputFluids'][0]['amount'] / d['time']
out = {x['fluidGuid']: x['amount'] / d['time'] for x in d['outputFluids']}
well = b['019ee389-bf73-73ef-979c-ca7e25697558']['generateFluid'][0]
gen = b['019eeaa6-71c4-739f-8eb8-509ea9af13d3']['fuelFluids'][0]
print('distiller per well', (well['amount'] / well['generateTime']) / crude)        # 2.0
print('distiller per iron pipe', 10 / crude, 'per steel pipe', 30 / crude)          # 5.0 / 15.0
print('generators per distiller', out['019eded2-7454-76da-8943-84c4e7a59179'] / (gen['amount'] / gen['time']))  # 2.4
for g, name in [('019ee849-5e17-75d4-bdd3-c23840a4b110', 'rubber'), ('019ee865-a643-766f-8475-7496c563d15d', 'plastic'),
                ('019ee86a-9769-716c-bc3f-488c558a6f44', 'asphalt'), ('019ee87f-8957-740d-a226-e1f94edd594c', 'solid fuel')]:
    x = r[g]; f = x['inputFluids'][0]
    print(name, 'plants per distiller', out[f['fluidGuid']] / (f['amount'] / x['time']))  # 4.0 / 3.2 / 2.5 / 2.0
EOF
```
Expected: `2.0` / `5.0 15.0` / `2.4` / rubber 4.0・plastic 3.2・asphalt 2.5・solid fuel 2.0。続けて recipe-chain-machine-count スキルで「プラスチックを毎秒1個」を引き、蒸留機・化学プラント・油井の台数が上の比（化学プラント 1/0.5=2台、ナフサ0.25/秒＝蒸留機0.625台、油井0.3125台）と一致することを確かめる

- [ ] **Step 5: コミットする**

```bash
cd /Users/katsumi/moorestech_master-wt-fluid && git add server_v8/mods/moorestechAlphaMod_8/master/blocks.json server_v8/mods/moorestechAlphaMod_8/master/machineRecipes.json server_v8/mods/moorestechAlphaMod_8/master/train.json && git commit -m "feat: 液体の数値をパイプ律速のSatisfactory比率にそろえる

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

### Task 2: master を push・ピンを更新し、Unity でロードを確かめる

**Files:**
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: Task 1 のコミット（`git -C ../moorestech_master-wt-fluid rev-parse HEAD`）
- Produces: push 済みの moorestech_master ブランチ `feat/fluid-rates-satisfactory-ratio`、本repoのピンコミット

- [ ] **Step 1: moorestech_master を push する**

```bash
cd /Users/katsumi/moorestech_master-wt-fluid && git push -u origin feat/fluid-rates-satisfactory-ratio && git rev-parse HEAD
```

- [ ] **Step 2: 本repoのピンを書き換えてコミットする**

Unity はピンの作業ツリー版を実チェックアウト値へ書き戻し、録画テストのスクリプトはコミット済みのピンで master worktree を解決する。そのため Unity を起こす前にピンをコミットする（unity-playmode-recorded-playtest SKILL.md の master ピン規則）。

```bash
cd /Users/katsumi/moorestech-wt-fluid && H=$(git -C ../moorestech_master-wt-fluid rev-parse HEAD) && jq --indent 4 --arg h "$H" '(.repositories[] | select(.key=="moorestech_master") | .commitHash) = $h' .moorestech-external-revisions.json > /tmp/rev.json && mv /tmp/rev.json .moorestech-external-revisions.json && git diff .moorestech-external-revisions.json
```
Expected: moorestech_master の commitHash 1行だけが変わる

```bash
cd /Users/katsumi/moorestech-wt-fluid && git add .moorestech-external-revisions.json docs/adr/0084-fluid-rates-aligned-to-satisfactory-ratios.md docs/superpowers/plans/2026-10-19-fluid-rates-satisfactory-ratio.md .decisions/2026-10-19-*.md && git commit -m "chore: 液体数値のSatisfactory比率合わせへmasterピンを更新

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 3: unityプレイ録画テストでロードと値を確かめる**

始める前に、ほかの worktree（`/Users/katsumi/moorestech` など）で Unity が起動していないことを確かめる。起動していると CEF の一時ディレクトリが共有され、PlayMode が無言で止まる。起動していたら止めてから進める。

unity-playmode-recorded-playtest スキルに従い、`/Users/katsumi/moorestech-wt-fluid` で Library をメインworktreeからコピーしたうえで、v8 ワールドを ready まで起動する最小シナリオを実行する。`run-scenario.sh` の第3引数には `/Users/katsumi/moorestech_master-wt-fluid/server_v8` を明示する。ready 後に uloop execute-dynamic-code で MasterHolder から次を読み、ログに出す:
- 石油蒸留機レシピの time・入出力量（5 / 10 → 2・3・5）
- 貨車 `TrainUnitMaster` の貨車 FluidCapacity（5000）
- 鉄・鋼鉄パイプの flowCapacity（10・30）

Expected: MooresmasterLoaderException なし、`uloop get-logs --project-path ./moorestech_client --log-type Error` が0件、読んだ値が上と一致。実行後に `git diff --exit-code .moorestech-external-revisions.json` が空（ピンが書き戻されていない）。録画ファイルのパスを報告に添える

### Task 3: moores-code-review で全ブランチレビューを実行する

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。対象は本repoブランチと moorestech_master の差分（`git -C ../moorestech_master-wt-fluid diff origin/master...HEAD`）の両方。指摘の反映で値が変わったら Task 1 Step 3〜5 と Task 2 Step 2〜3 をやり直す

### Task 4: PRを作成しセッション終了可能状態にする

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PRは2本: moorestech_master（`gh pr create -R moorestech/moorestech_master`、本文に ADR 0084 の要点と変更値の表）と本repo（本文に master PR のURL）。moorestech_master 側で origin/master が進んでいたらマージして Task 1 Step 3〜4 の検算をやり直し、ピンを再更新する

## 判断記録（ADR）

- 設計ADR: `docs/adr/0084-fluid-rates-aligned-to-satisfactory-ratios.md`（裁定の正本）と `.decisions/2026-10-19-*.md`（液体関連8件）
- simulator review（判事 fable 2026-10-19）: 適用推奨1件（ピンを Unity 起動前にコミット）と Warning 3件（jq --indent 4・nodeGraph の扱いを ADR と揃える・他 worktree の Unity 停止確認）を適用した。出所: シミュレーター予測（裁定不要の手順修正）
- タスク分割: マスタ書き換え（Task 1）と push・ピン・ロード確認（Task 2）を分けた。値の正しさは検算スクリプトで、ロード可否は Unity で別々に確かめるため。出所: agent前提
- 書き換え手段: 手編集でなく GUID 指定のスクリプト。名前一致だと同名ブロック・レシピを取り違えるため、assert で前提（液体GUID・接続口の存在）を確かめてから書く。出所: agent前提
- unityプレイ録画テスト: ロード確認（ready 到達と MasterHolder の値）までを含める。列車を走らせて輸送量を実測する検証は含めない。列車の往復と積み下ろし速度はこのPRで変えず、貨車容量は値の読み取りで確かめられるため。出所: agent前提
- 手元調整の取り込み: moorestech_master メインディレクトリの未コミット差分（nodeGraph を除く）をパッチとして本ブランチに c7ba2d7 でコミット済み。メインディレクトリ（`/Users/katsumi/moorestech_master`）の未コミット差分（nodeGraph の視点を含む）は消さない。PRマージ後にユーザーが破棄する。出所: ユーザー裁定 2026-10-19 選択「液体以外もPRに含める」
