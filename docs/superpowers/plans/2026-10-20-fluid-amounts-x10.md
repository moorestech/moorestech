# 液体の数量10倍化 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

> **実行記録:** ユーザー指示（2026-10-20「タスクが終わったらもうさっとjsonかえていいよ」）により、このplanは新規セッションへ引き継がず作成セッションが Task 1 と Task 2 のピン更新だけを直接実行した。パイプハッチ定数（Task 2 Step 1）は別タスクへ移した（`.decisions/2026-10-20-パイプハッチ容量の10倍化とマスタ化は別タスクにする.md`）。Unity でのロード確認・moores-code-review は省略した。

**Goal:** v8 マスタとコード直書きの液体量をすべて×10の単位にし、moorestech_master のPRと本repoのPR（ピン更新＋定数1件）を出す。

**Architecture:** 比率は ADR 0084 のまま、液体の数量だけを10倍する。`/Users/katsumi/moorestech_master-wt-fluid-x10`（branch `feat/fluid-amounts-x10`、土台 origin/master 8296655）の `blocks.json`・`machineRecipes.json`・`train.json` を、キーで対象を決めて×10するPythonスクリプト1本で書き換え、変更前後の値を表で assert する。JSONは `json.dumps(ensure_ascii=False, indent=2)`（末尾改行なし）で元ファイルと1バイト違わず往復することを実測済み。本repo（`/Users/katsumi/moorestech-wt-fluid-x10`、branch `feat/fluid-amounts-x10`、土台 origin/master a16b2e82b4）は `.moorestech-external-revisions.json` の moorestech_master ピンと、`VanillaCleanRoomBoundaryTemplate.PipeHatchFluidCapacity` を更新する。

**Tech Stack:** Python 3（`-I`）、jq、moorestech_master JSON、uloop、unity-playmode-recorded-playtest、gh

## Requirements

1. 液体（原油・ナフサ・ガソリン・残油・水・蒸気）の数量を×10にする。受け入れ: Task 1 のスクリプトの assert 表（変更前→変更後）がすべて通る
   - machineRecipes.json の `inputFluids[].amount`・`outputFluids[].amount` 全件（ボイラー2・石油蒸留機1・化学プラント4）
   - blocks.json の `generateFluid[].amount`（油井・歯車ポンプ）、`fuelFluids[].amount`（ガソリン発電機）、`requiredFluids[].amount`（蒸気機関）
   - blocks.json の全 `fluidInventoryConnectors` の `flowCapacity`（ボイドパイプ 1000→10000 を含む）
   - blocks.json の `innerTankCapacity`・`fuelFluidTankCapacity`・FuelGearGenerator の `fluidCapacity`、FluidPipe／TrainFluidPlatform の `capacity`（0 は 0 のまま）
   - train.json の trainCars の `fluidCapacity`（0 は 0 のまま）
2. 処理時間（`time`・`generateTime`・`consumptionTime`）・アイテムの入出力数・`power`・アイテム燃料の amount（燃料式風車の gearFuelItems）・`filterCapacity`・列車の `fluidContainer.weight` は変えない。受け入れ: Task 1 Step 3 の差分行チェックで対象キー以外が変わっていない
3. 比率が ADR 0084 のまま。受け入れ: Task 1 Step 4 の検算が 蒸留機/油井 2.0、蒸留機/鉄パイプ 5.0、蒸留機/鋼鉄パイプ 15.0、発電機/蒸留機 2.4、化学プラント/蒸留機 4.0・3.2・2.5・2.0、ボイラー(水100/2秒)/鉄パイプ 2.0
4. クリーンルームのパイプハッチ容量 `PipeHatchFluidCapacity` を 100f→1000f。受け入れ: コンパイル0エラー、`CleanRoomPipeHatchFluidTest`・`CleanRoomHatchTest` が PASS
5. moorestech_master の変更は push して PR を作り、本repoのピンはそのPRが指す push 済みコミットを指す。受け入れ: 両PRが作成済みでマージ可能
6. v8 マスタが Unity でエラーなくロードされ、MasterHolder 上の値が×10後の値と一致する（unityプレイ録画テストで確認）

やらないこと:
- セーブのマイグレーションは書かない。CurrentVersion も上げない（既存セーブの液体在庫は数量のまま＝実質1/10）
- テスト用 mod（moorestech_server のテストマスタ）の数値は変えない
- VanillaSchema・他の C# コードは触らない。`PipeHatchFluidCapacity` をマスタ化しない
- `/Users/katsumi/moorestech_master` メインディレクトリの未コミット差分（beltSpeedPerSecond の 1.0→1、machineRecipes の count 3→1、nodeGraph）は PR に含めず、消さない
- `/Users/katsumi/moorestech` メインディレクトリの未コミット差分（ピン・`TrainUnitNextBranchFinder.cs.meta`）とローカルコミット `e97360fc64 v7超軽量設定` は触らない

## Global Constraints

- 対象 mod は `server_v8/mods/moorestechAlphaMod_8/master/` のみ
- JSON の書き換えは Task 1 のスクリプトで行い、手で整形し直さない（`json.dumps(..., ensure_ascii=False, indent=2)`、末尾改行なし）
- ×10 の結果が整数なら int で書く（0.5→`5`、0.1→`1`）
- 別repoの変更も push して PR を作る。ピンは push 済みコミットを指す（AGENTS.md）
- PR は通常のマージコミット前提（Squash しない）
- 新規 worktree で Unity を起動するときは、メインworktree（`/Users/katsumi/moorestech/moorestech_client/Library`）から Library をコピーする。Library は削除しない
- C# のコメントは日本語→英語の2行セット（AGENTS.md）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 型・機構の新設なし（マスタ値と既存定数のみ）。死活表: 液体の操作はすべて存続
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / 発火なし（型・API新設なし、ADR 0084/0086と一致）

---

### Task 1: v8 マスタの液体量を×10にする

**Files:**
- Modify: `../moorestech_master-wt-fluid-x10/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`
- Modify: `../moorestech_master-wt-fluid-x10/server_v8/mods/moorestechAlphaMod_8/master/machineRecipes.json`
- Modify: `../moorestech_master-wt-fluid-x10/server_v8/mods/moorestechAlphaMod_8/master/train.json`

**Interfaces:**
- Consumes: なし
- Produces: moorestech_master のコミット（Task 2 で push・ピンに使う）

- [ ] **Step 1: 作業場所を確認する**

```bash
cd /Users/katsumi/moorestech_master-wt-fluid-x10 && pwd && git branch --show-current && git log --oneline -1 && git status --short
```
Expected: branch `feat/fluid-amounts-x10`、先頭 `8296655`、status 空。違えば止めて報告する

- [ ] **Step 2: 書き換えスクリプトを実行する**

```bash
cd /Users/katsumi/moorestech_master-wt-fluid-x10/server_v8/mods/moorestechAlphaMod_8/master && python3 -I - <<'EOF'
import json

def load(path):
    with open(path, encoding='utf-8') as f:
        return json.load(f)

def save(path, data):
    with open(path, 'w', encoding='utf-8') as f:
        f.write(json.dumps(data, ensure_ascii=False, indent=2))

def x10(v):
    r = v * 10
    return int(round(r)) if abs(r - round(r)) < 1e-9 else r

changed = []  # (場所, 前, 後)

def scale(obj, key, where):
    before = obj[key]
    obj[key] = x10(before)
    changed.append((where, before, obj[key]))

# blocks.json: キーで対象を決める
blocks = load('blocks.json')
for b in blocks['data']:
    p, name, bt = b['blockParam'], b['name'], b['blockType']
    conns = p.get('fluidInventoryConnectors')
    if conns:
        for i, port in enumerate(conns.get('inflowConnects', []) + conns.get('outflowConnects', [])):
            scale(port['option'], 'flowCapacity', f'{name}.flowCapacity[{i}]')
    for list_key in ('generateFluid', 'fuelFluids', 'requiredFluids'):
        for i, e in enumerate(p.get(list_key, [])):
            assert 'fluidGuid' in e, (name, list_key)
            scale(e, 'amount', f'{name}.{list_key}[{i}].amount')
    for k in ('innerTankCapacity', 'fuelFluidTankCapacity'):
        if k in p and p[k] != 0:
            scale(p, k, f'{name}.{k}')
    if bt == 'FuelGearGenerator' and p.get('fluidCapacity', 0) != 0:
        scale(p, 'fluidCapacity', f'{name}.fluidCapacity')
    if bt in ('FluidPipe', 'TrainFluidPlatform'):
        scale(p, 'capacity', f'{name}.capacity')
save('blocks.json', blocks)

# machineRecipes.json: 液体の入出力量
recipes = load('machineRecipes.json')
for r in recipes['data']:
    for list_key in ('inputFluids', 'outputFluids'):
        for i, e in enumerate(r.get(list_key) or []):
            scale(e, 'amount', f"{r['machineRecipeGuid']}.{list_key}[{i}]")
save('machineRecipes.json', recipes)

# train.json: 貨車の液体容量
train = load('train.json')
for c in train['trainCars']:
    if c.get('fluidCapacity', 0) != 0:
        scale(c, 'fluidCapacity', f"{c['trainCarGuid']}.fluidCapacity")
save('train.json', train)

# 変更前→変更後の期待表（ADR 0084 の値 → ×10）
expected = {
    'ボイラー.innerTankCapacity': (200, 2000),
    '蒸気機関.requiredFluids[0].amount': (0.1, 1),
    '蒸気機関.fluidCapacity': (10, 100),
    '鉄のパイプ.capacity': (50, 500),
    '歯車ポンプ.generateFluid[0].amount': (1, 10),
    '歯車ポンプ.innerTankCapacity': (100, 1000),
    '油井.generateFluid[0].amount': (2, 20),
    '油井.innerTankCapacity': (30, 300),
    '石油蒸留機.innerTankCapacity': (30, 300),
    '化学プラント.innerTankCapacity': (30, 300),
    'ガソリンエンジン発電機.fuelFluids[0].amount': (1, 10),
    'ガソリンエンジン発電機.fuelFluidTankCapacity': (20, 200),
    '液体タンク.capacity': (5000, 50000),
    '液体プラットフォーム.capacity': (5000, 50000),
    '鋼鉄のパイプ.capacity': (50, 500),
    'f998f54b-7619-491c-a100-ee88955f9058.fluidCapacity': (5000, 50000),
    '019ee38b-1c1a-71ca-9b2e-b35d2a6f9a23.inputFluids[0]': (10, 100),
    '019ee849-5e17-75d4-bdd3-c23840a4b110.inputFluids[0]': (0.5, 5),
    '019ee865-a643-766f-8475-7496c563d15d.inputFluids[0]': (0.5, 5),
    '019ee86a-9769-716c-bc3f-488c558a6f44.inputFluids[0]': (2, 20),
    '019ee87f-8957-740d-a226-e1f94edd594c.inputFluids[0]': (1, 10),
}
got = {w: (a, b) for w, a, b in changed}
for w, ab in expected.items():
    assert got.get(w) == ab, (w, got.get(w), ab)
# 接続口: パイプ鉄10・鋼鉄30・ボイド1000・その他60
for w, a, b in changed:
    if '.flowCapacity[' in w:
        exp = 10 if w.startswith('鉄のパイプ') else 30 if w.startswith('鋼鉄のパイプ') else 1000 if w.startswith('ボイドパイプ') else 60
        assert (a, b) == (exp, exp * 10), (w, a, b)
# 蒸留機の出力とボイラー2レシピ
for w, a, b in changed:
    if w.startswith('019ee38b-1c1a-71ca-9b2e-b35d2a6f9a23.outputFluids'):
        assert a in (2, 3, 5) and b == a * 10, (w, a, b)
    if w.startswith(('d5032597-fdb8-4f45-9857-1c018ba64c28', '019da407-23be-73db-989c-fda5967ca034')):
        assert a in (10, 30) and b == a * 10, (w, a, b)
for w, a, b in changed:
    print(w, a, '->', b)
print('changed', len(changed))
print('ok')
EOF
```
Expected: 末尾が `ok`。assert で落ちたらマスタが ADR 0084 の値と違うので、`git checkout -- .` で戻して止めて報告する

- [ ] **Step 3: 差分が対象キーの値の行だけであることを確認する**

```bash
cd /Users/katsumi/moorestech_master-wt-fluid-x10 && git diff --stat && git diff | grep '^[-+] ' | grep -vE '"(flowCapacity|amount|innerTankCapacity|fuelFluidTankCapacity|capacity|fluidCapacity)":'
```
Expected: 3ファイルのみ変更、2つ目のコマンドの出力は空。`amount` の変更行数が Step 2 の出力のうち `.amount` / `Fluids[` を含む行数と一致する（アイテムの amount は Step 2 で `fluidGuid` を assert して除外済み）

- [ ] **Step 4: 比率を検算する**

```bash
cd /Users/katsumi/moorestech_master-wt-fluid-x10/server_v8/mods/moorestechAlphaMod_8/master && python3 -I - <<'EOF'
import json
b = {x['blockGuid']: x['blockParam'] for x in json.load(open('blocks.json', encoding='utf-8'))['data']}
r = {x['machineRecipeGuid']: x for x in json.load(open('machineRecipes.json', encoding='utf-8'))['data']}
d = r['019ee38b-1c1a-71ca-9b2e-b35d2a6f9a23']
crude = d['inputFluids'][0]['amount'] / d['time']
out = {x['fluidGuid']: x['amount'] / d['time'] for x in d['outputFluids']}
well = b['019ee389-bf73-73ef-979c-ca7e25697558']['generateFluid'][0]
gen = b['019eeaa6-71c4-739f-8eb8-509ea9af13d3']['fuelFluids'][0]
iron = b['7d35321c-98b7-49db-8fc7-6f23854dd1c7']['fluidInventoryConnectors']['inflowConnects'][0]['option']['flowCapacity']
steel = b['019f0d29-e72e-779a-893a-144cf32f4521']['fluidInventoryConnectors']['inflowConnects'][0]['option']['flowCapacity']
print('distiller per well', (well['amount'] / well['generateTime']) / crude)   # 2.0
print('distiller per iron pipe', iron / crude, 'per steel pipe', steel / crude)  # 5.0 / 15.0
print('generators per distiller', out['019eded2-7454-76da-8943-84c4e7a59179'] / (gen['amount'] / gen['time']), 'power', gen['power'])  # 2.4 / 1000
for g, name in [('019ee849-5e17-75d4-bdd3-c23840a4b110', 'rubber'), ('019ee865-a643-766f-8475-7496c563d15d', 'plastic'),
                ('019ee86a-9769-716c-bc3f-488c558a6f44', 'asphalt'), ('019ee87f-8957-740d-a226-e1f94edd594c', 'solid fuel')]:
    x = r[g]; f = x['inputFluids'][0]
    print(name, 'plants per distiller', out[f['fluidGuid']] / (f['amount'] / x['time']))  # 4.0 / 3.2 / 2.5 / 2.0
boiler = r['d5032597-fdb8-4f45-9857-1c018ba64c28']
print('boiler per iron pipe', iron / (boiler['inputFluids'][0]['amount'] / boiler['time']))  # 2.0 (水100/2秒)
EOF
```
Expected: `2.0` / `5.0 15.0` / `2.4 power 1000` / rubber 4.0・plastic 3.2・asphalt 2.5・solid fuel 2.0 / boiler `2.0`。ボイラーのレシピ guid `d5032597…` が 水100/2秒でなければ（もう一方の `019da407…` が該当なら）そちらで計算し直す

- [ ] **Step 5: コミットする**

```bash
cd /Users/katsumi/moorestech_master-wt-fluid-x10 && git add server_v8/mods/moorestechAlphaMod_8/master/blocks.json server_v8/mods/moorestechAlphaMod_8/master/machineRecipes.json server_v8/mods/moorestechAlphaMod_8/master/train.json && git commit -m "feat: 液体の数量をすべて10倍の単位にそろえる

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

### Task 2: パイプハッチ容量・ピン更新と Unity での確認

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/CleanRoom/VanillaCleanRoomBoundaryTemplate.cs:18-20`
- Modify: `.moorestech-external-revisions.json`
- Modify: `docs/adr/0084-fluid-rates-aligned-to-satisfactory-ratios.md:4`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomPipeHatchFluidTest.cs`（既存・変更なし）

**Interfaces:**
- Consumes: Task 1 のコミット（`git -C /Users/katsumi/moorestech_master-wt-fluid-x10 rev-parse HEAD`）
- Produces: push 済みの moorestech_master ブランチ `feat/fluid-amounts-x10`、本repoのコミット

- [ ] **Step 1: パイプハッチ容量を×10にする**

`VanillaCleanRoomBoundaryTemplate.cs` の18〜20行目を次に置き換える:

```csharp
        // パイプハッチは液体10倍単位での固定容量にする
        // The pipe hatch uses a fixed capacity in the x10 fluid unit
        private const float PipeHatchFluidCapacity = 1000f;
```

- [ ] **Step 2: ADR 0084 の状態行に注記する**

`docs/adr/0084-fluid-rates-aligned-to-satisfactory-ratios.md` の `状態: 採択` を `状態: 採択（液体の数量の単位は ADR 0086 で×10。比率はこのADRのまま）` にする

- [ ] **Step 3: moorestech_master を push し、ピンを書き換えてコミットする**

Unity はピンの作業ツリー版を実チェックアウト値へ書き戻し、録画テストのスクリプトはコミット済みのピンで master worktree を解決する。そのため Unity を起こす前にピンをコミットする。

```bash
cd /Users/katsumi/moorestech_master-wt-fluid-x10 && git push -u origin feat/fluid-amounts-x10 && cd /Users/katsumi/moorestech-wt-fluid-x10 && H=$(git -C /Users/katsumi/moorestech_master-wt-fluid-x10 rev-parse HEAD) && jq --indent 4 --arg h "$H" '(.repositories[] | select(.key=="moorestech_master") | .commitHash) = $h' .moorestech-external-revisions.json > /tmp/rev-x10.json && mv /tmp/rev-x10.json .moorestech-external-revisions.json && git diff .moorestech-external-revisions.json
```
Expected: moorestech_master の commitHash 1行だけが変わる

```bash
cd /Users/katsumi/moorestech-wt-fluid-x10 && git add .moorestech-external-revisions.json moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/CleanRoom/VanillaCleanRoomBoundaryTemplate.cs docs/adr/0084-fluid-rates-aligned-to-satisfactory-ratios.md docs/adr/0086-fluid-amounts-scaled-by-ten.md docs/superpowers/plans/2026-10-20-fluid-amounts-x10.md .decisions/2026-10-20-液体*.md && git commit -m "feat: 液体の数量を10倍の単位にそろえる（masterピン更新・パイプハッチ容量）

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 4: Library をコピーしてコンパイル・テストする**

始める前に、ほかの worktree（`/Users/katsumi/moorestech` など）で Unity が起動していないことを確かめる（CEF の一時ディレクトリ共有で PlayMode が無言で止まるため）。起動していたら止めてから進める。

```bash
[ -d /Users/katsumi/moorestech-wt-fluid-x10/moorestech_client/Library ] || cp -R /Users/katsumi/moorestech/moorestech_client/Library /Users/katsumi/moorestech-wt-fluid-x10/moorestech_client/Library
cd /Users/katsumi/moorestech-wt-fluid-x10 && uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPipeHatchFluidTest|CleanRoomHatchTest"
```
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 5: unityプレイ録画テストでロードと値を確かめる**

unity-playmode-recorded-playtest スキルに従い、`/Users/katsumi/moorestech-wt-fluid-x10` で v8 ワールドを ready まで起動する最小シナリオを実行する。`run-scenario.sh` の第3引数には `/Users/katsumi/moorestech_master-wt-fluid-x10/server_v8` を明示する。ready 後に uloop execute-dynamic-code で MasterHolder から次を読み、ログに出す:
- 石油蒸留機レシピ（`019ee38b-1c1a-71ca-9b2e-b35d2a6f9a23`）の入出力量（100 → 20・30・50）
- 油井の generateFluid amount（20）、蒸気機関の requiredFluids amount（1）
- 貨車 `f998f54b-7619-491c-a100-ee88955f9058` の FluidCapacity（50000）
- 鉄・鋼鉄パイプの flowCapacity（100・300）と capacity（500）

Expected: MooresmasterLoaderException なし、`uloop get-logs --project-path ./moorestech_client --log-type Error` が0件、読んだ値が上と一致。実行後に `git diff --exit-code .moorestech-external-revisions.json` が空。録画ファイルのパスを報告に添える

### Task 3: moores-code-review で全ブランチレビューを実行する

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。対象は本repoブランチと moorestech_master の差分（`git -C /Users/katsumi/moorestech_master-wt-fluid-x10 diff origin/master...HEAD`）の両方。指摘の反映で値が変わったら Task 1 Step 3〜5 と Task 2 Step 3〜5 をやり直す

### Task 4: PRを作成しセッション終了可能状態にする

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。PRは2本: moorestech_master（`gh pr create -R moorestech/moorestech_master`、本文に ADR 0086 の要点と変更前後の値の表）と本repo（本文に master PR のURL）。moorestech_master 側で origin/master が進んでいたらマージして Task 1 Step 3〜4 の検算をやり直し、ピンを再更新する

## 判断記録（ADR）

- 設計ADR: `docs/adr/0086-fluid-amounts-scaled-by-ten.md`（裁定の正本）と `.decisions/2026-10-20-液体の数量はすべて10倍の単位にそろえる.md`・`.decisions/2026-10-20-液体10倍化で既存セーブの液体在庫は数量のまま引き継ぐ.md`。比率の正本は `docs/adr/0084-fluid-rates-aligned-to-satisfactory-ratios.md`
- 書き換え手段: GUID ごとの値指定ではなく、キーで対象を決めて×10し、ADR 0084 の値→×10 の期待表で assert する。液体量を持つキーが漏れなく対象になり、想定外の値（マスタが ADR 0084 とずれている）を止められるため。アイテム燃料の amount は `fluidGuid` を持つ要素だけに絞って除外する。出所: agent前提
- タスク分割: マスタ書き換え（Task 1）と、コード定数・ピン・Unity 確認（Task 2）を分けた。値の正しさは検算スクリプトで、ロード可否とコードは Unity で別々に確かめるため。出所: agent前提（前例 `docs/superpowers/plans/2026-10-19-fluid-rates-satisfactory-ratio.md` と同じ分割）
- パイプハッチ定数（`VanillaCleanRoomBoundaryTemplate.cs` の `PipeHatchFluidCapacity`）は 100→1000（×10）。コメントは実値と合っていなかった（鉄パイプ容量は50）ので書き直す。マスタ化はしない（範囲外）。出所: agent前提（ADR 0086「液体すべて×10」）
- unityプレイ録画テスト: ロード確認（ready 到達と MasterHolder の値）までを含める。液体の流れの実測は含めない。比率は変えず、値は MasterHolder の読み取りで確かめられるため。出所: agent前提
- 列車の `fluidContainer.weight` は貨車1両の固定重量（`FluidTrainCarContainer.GetWeight()` がマスタ値をそのまま返し、液体量を掛けない）なので×10の対象外。出所: agent前提（コード確認済み）
- Task 1 のスクリプトはマスタ（8296655）のコピーで事前に実行し、assert 全通過・52値変更・Step 4 の検算値一致・対象キー以外の差分0を確認済み。キー構造（`fluidInventoryConnectors.*Connects[].option.flowCapacity`、`capacity` を持つのは FluidPipe／TrainFluidPlatform だけ、蒸気機関は FuelGearGenerator）も実データで確かめた。出所: agent前提（plan作成時の実測）
- スキル文書: `.agents/skills/recipe-chain-machine-count/SKILL.md` には液体の具体的な数値例が無い（計算式だけ）ため変更しない。ADR 0086 Consequences の「数値例があれば直す」はこれで充足。出所: agent前提（plan作成時に grep で確認）
- 作業場所: 両repoとも origin/master から新規 worktree（本repo `/Users/katsumi/moorestech-wt-fluid-x10`、master `/Users/katsumi/moorestech_master-wt-fluid-x10`）。両メインディレクトリには無関係な未コミット差分とローカルコミットがあり、混ぜないため。出所: agent前提
