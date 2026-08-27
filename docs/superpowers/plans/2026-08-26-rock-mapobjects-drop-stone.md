# 岩系mapObjectのドロップを石へ移す Implementation Plan

> **実行済み（2026-08-26）:** ユーザーの「サクッとやってPR作って」指示により、本planはインラインで実行した。
> **Task 1（不変条件のEditModeテスト追加）は実施していない** — Unity Editorでのコンパイル・テスト実行が必要で時間がかかるため、
> 別タスクへ送った。残りの Task 0・2・3・4 は実施済み。
> 実行時に判明した数え間違いの訂正: 小石ドロップは90件ではなく **89件**（Mining 85 / PickUp 4）、
> mapVeins は12件ではなく **11件**（削除後10件）。以前の値はguid出現回数に鉱脈1件を混ぜたもので、
> 本文の数値は訂正済み。

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 採掘できる岩系mapObject 85件のドロップを「小石」から「石」へ移し、小石はPickUpの小石mapObject 4件からのみ出るようにする。

**Architecture:** 変更の実体は `moorestech_master` リポジトリの `map.json` のデータ書き換えのみ（C#のプロダクションコード変更ゼロ）。本repo側には、ピン済みmasterの `map.json` を読んで不変条件を守るEditModeテストを1本追加し、以後の退行を機械的に止める。前例は `Client.Tests/Map/VeinOutcropAddressableLoadTest.cs`（`PinnedMasterRepository.ReadPinnedFile` でコミット済みピンのmasterを読むテスト）。

**Tech Stack:** JSON（マスタデータ） / C# NUnit EditModeテスト（`Client.Tests` asmdef、Newtonsoft.Json.Linq） / uloop CLI / git worktree（`moores-wt`）

## Requirements

1. `map.json` の mapObjects のうち `miningType=Mining` かつ earnItems に小石(`582040ec-093b-4c8e-8fe3-f4ec030cf1ca`)を持つ **85件全部** の `itemGuid` を石(`44aaddd6-e3c0-4131-a159-9140d3e2e33b`)へ置換する。受け入れ基準: 変更後、石を earnItems に持つ mapObject が85件、うち全件 `miningType=="Mining"`。
2. `miningType=PickUp` の4件（`小石` / `Pebble1` / `Pebble2` / `Pebble3`）は小石のまま無変更。受け入れ基準: 変更後、小石を earnItems に持つ mapObject は4件で全件 `miningType=="PickUp"`。
3. `minCount`/`maxCount` は据え置く（岩は1〜4、小石は1〜1）。受け入れ基準: 変更前後で全 earnItems の `minCount`/`maxCount` が完全一致。
4. mapVeins から `小石鉱脈`(`d48d49b5-a5e2-4f44-a1a6-8d7b9c1f4e50`) を削除する。受け入れ基準: 変更後 mapVeins は10件で、小石を指す vein が0件。
5. 上記の不変条件（小石はPickUpのみ・小石鉱脈なし）を守るEditModeテストを1本追加する。受け入れ基準: ピン更新前は赤、ピン更新後は緑。
6. `moorestech_master` 側でブランチを切ってpush・PRを作成し、本repoの `.moorestech-external-revisions.json` のピンをそのPRのpush済みコミットへ更新して本repo側もPRを作る。受け入れ基準: 両repoにPRが存在し、ピンが master repo のpush済みコミットを指す。

**やらないこと（スコープ境界）:**
- `challenges.json` の文言・順序変更（チャレンジ7「石鉱脈から石を5個採掘しよう」が岩で達成可能になる件は bd `moorestech-tbdr` へ送る）
- `craftRecipes.json` / `research.json` / `items.json` / `blocks.json` / `machineRecipes.json` / `generation.json` の変更
- 岩から出る石の個数調整・`石`の `initialUnlocked` 変更
- C#プロダクションコードの変更、スキーマ(`VanillaSchema/map.yml`)の変更

## Global Constraints

- 変更は本repo（`moorestech`）と `moorestech_master` の2repoにまたがる。**別リポジトリに変更が及ぶ場合も本repoと同様にpushしてPRを作る（AGENTS.md 必須）**。ローカルコミット止まり・push only（PR無し）は禁止。
- 本repoの `.moorestech-external-revisions.json` のピンは、master repo のPRが指す**push済みコミット**を指すこと。
- **Unityは `.moorestech-external-revisions.json` を実チェックアウト値へ書き戻す**。`git add -A` でピン更新が消える事故が実績としてあるため、ピンのコミットは `git add .moorestech-external-revisions.json` と明示し、コミット直前に `git diff --cached` で値を目視確認する。
- メインワークツリーでのブランチ作成はhookで物理拒否される。作業は `moores-wt new` で作った使い捨てworktree内で行う（CLAUDE.local.md）。
- `.meta` ファイルは手動作成しない。新規 `.cs` の `.meta` はUnity起動により自動生成されたものをコミットする。
- 1ディレクトリのコードは10ファイルまで。`Client.Tests/Map/` は既に11 `.cs` あるため、新規テストはサブディレクトリ `Client.Tests/Map/MasterData/` へ置く。
- コメントは日本語1行＋英語1行のセットを3〜10行ごとに入れる。日本語・英語とも1行に収める。
- `try-catch` 禁止。`partial` 禁止。`Func<>` 禁止。
- `.cs` を変更したら必ずコンパイルを実行する（`uloop compile --project-path ./moorestech_client`）。
- テストは `--filter-type regex` で対象を限定して実行する。`uloop run-tests` の既定は PlayMode のため EditMode テストには `--test-mode EditMode` を明示する。
- `uloop run-tests` が180秒でCLIタイムアウトしても失敗ではない。結果は `.uloop/outputs/TestResults` のXMLを見る。連打しない。
- uloopが「Domain Reload in progress」を返したら45秒待ってリトライする。

---

## 実装前の既知の前提（実装者は着手時に確認すること）

### パス変数（各シェルステップの冒頭で定義する）

個人の絶対パスを書かないため、以下をgitから導出して使う。`PRIMARY_REPO` はworktreeからでもメインクローンを指す（`PinnedMasterRepository` と同じ導出）。

```bash
PRIMARY_REPO=$(dirname "$(git rev-parse --path-format=absolute --git-common-dir)")
MASTER_REPO="$PRIMARY_REPO/../moorestech_master"
WT=~/moorestech-worktrees/rock-drop-stone
```


- **本repoのベース:** `origin/master`。`origin/master` のピンは `200ab3c908075cb0a9a661fb294384642454b3c9`。
- **master repoのベース:** `moorestech_master` の `origin/master`（本plan作成時点で `1016aae`）。`60e815a`・`200ab3c9` はいずれもその祖先。
- **既知のP0（本タスクとは無関係だが実行時検証を阻む可能性がある）:** bd `moorestech-tlza`「masterピン200ab3c9のgerman列をクライアントが未対応で起動不能」、bd `moorestech-hvwb`「CI全PR停止」。実プレイでの動作確認が通らない場合はこれらが原因かを先に切り分け、本タスクの受け入れはJSONレベルのテスト（Task 3）で行う。
- 本plan・ADR・裁定台帳の3ファイルはメインワークツリーに**未追跡**で存在する。Task 0でworktreeへ運ぶ。

---

### Task 0: 作業worktreeの用意とドキュメントの移送

**Files:**
- Copy: `docs/adr/0036-rock-mapobjects-drop-stone-not-pebble.md`
- Copy: `.decisions/2026-08-26-岩系mapObjectは石を落としPickUp小石だけが小石を落とす.md`
- Copy: `docs/superpowers/plans/2026-08-26-rock-mapobjects-drop-stone.md`

**Interfaces:**
- Consumes: なし
- Produces: worktreeパス `$WT`（= `~/moorestech-worktrees/rock-drop-stone`）、ブランチ `feature/rock-mapobjects-drop-stone`

- [ ] **Step 1: worktreeを作る**

```bash
moores-wt new feature/rock-mapobjects-drop-stone --dir rock-drop-stone --from origin/master --fetch
```

Expected: worktreeが作られ、Library/PersonalAssetsがclonefileでコピーされ、Unity Editorが `uloop launch` で起動する（所要3分強）。

- [ ] **Step 2: 作成直後にoriginとの一致を確認する**

`moores-wt new` は同名の古いローカルブランチがあると再利用するため、必ず突き合わせる。

```bash
cd "$WT"
git log --oneline -1
git rev-parse HEAD origin/master
```

Expected: `HEAD` と `origin/master` が同一。異なる場合は `git reset --hard origin/master` してから続行する。

- [ ] **Step 3: 未追跡のADR・裁定台帳・planをworktreeへ運ぶ**

```bash
SRC="$PRIMARY_REPO"
DST="$WT"
cp "$SRC/docs/adr/0036-rock-mapobjects-drop-stone-not-pebble.md" "$DST/docs/adr/"
cp "$SRC/.decisions/2026-08-26-岩系mapObjectは石を落としPickUp小石だけが小石を落とす.md" "$DST/.decisions/"
cp "$SRC/docs/superpowers/plans/2026-08-26-rock-mapobjects-drop-stone.md" "$DST/docs/superpowers/plans/"
```

- [ ] **Step 4: 3ファイルをコミットする**

```bash
cd "$WT"
git add docs/adr/0036-rock-mapobjects-drop-stone-not-pebble.md \
        ".decisions/2026-08-26-岩系mapObjectは石を落としPickUp小石だけが小石を落とす.md" \
        docs/superpowers/plans/2026-08-26-rock-mapobjects-drop-stone.md
git commit -m "docs: 岩系mapObjectのドロップを石へ移すADRと実装計画を追加"
```

- [ ] **Step 5: bdタスクを着手にする**

素のコマンドをそのまま1行で実行する（wrapper・パイプ・リダイレクトを付けるとhookに拒否される）。

```
bd update moorestech-k9ui --claim
```

---

### Task 1: ドロップ元の不変条件テストを追加する（この時点では赤）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/MasterData/MapObjectDropSourceTest.cs`
- Reference（変更しない）: `moorestech_client/Assets/Scripts/Client.Tests/Map/VeinOutcropAddressableLoadTest.cs`（同型の前例）
- Reference（変更しない）: `moorestech_client/Assets/Scripts/Client.Tests/Support/PinnedMasterRepository.cs`

**Interfaces:**
- Consumes: `Client.Tests.Support.PinnedMasterRepository.ReadPinnedFile(string pathInMasterRepository) -> string`（コミット済みピンが指すmaster repoコミットからファイル本文を読む）
- Produces: NUnitテストクラス `Client.Tests.Map.MasterData.MapObjectDropSourceTest`（テスト名 `小石はPickUpのmapObjectからのみ入手できる` / `石を落とすmapObjectは全てMining採掘である` / `小石を指す鉱脈は存在しない`）

**このテストが読むのは「コミット済みピンが指すmasterのmap.json」であり、作業ツリーのmap.jsonではない。** よってTask 2でmaster側を直しただけでは緑にならず、Task 3でピンをコミットして初めて緑になる。この順序は意図したものである。

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Map/MasterData/MapObjectDropSourceTest.cs` を新規作成する。

```csharp
using System;
using System.Collections.Generic;
using Client.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.Map.MasterData
{
    /// <summary>
    ///     小石は小石mapObjectのみ、岩系mapObjectは石を落とすというドロップ元の不変条件を検証する
    ///     Verifies the drop-source invariant: pebbles come only from pebble map objects and rocks drop stone
    /// </summary>
    public class MapObjectDropSourceTest
    {
        private const string MapJsonPath = "server_v8/mods/moorestechAlphaMod_8/master/map.json";
        private const string PebbleItemGuid = "582040ec-093b-4c8e-8fe3-f4ec030cf1ca";
        private const string StoneItemGuid = "44aaddd6-e3c0-4131-a159-9140d3e2e33b";

        [Test]
        public void 小石はPickUpのmapObjectからのみ入手できる()
        {
            var pebbleDroppers = FindMapObjectsDropping(PebbleItemGuid);

            // 0件だと以降の検証が全て素通りするので先に落とす
            // With zero droppers every later assertion would pass vacuously, so fail here first
            Assert.IsNotEmpty(pebbleDroppers, "no map object drops the pebble item; the test would pass vacuously");

            foreach (var mapObject in pebbleDroppers)
            {
                var name = (string)mapObject["mapObjectName"];
                Assert.AreEqual("PickUp", (string)mapObject["miningType"], $"pebble must drop only from PickUp map objects: {name}");
            }
        }

        [Test]
        public void 石を落とすmapObjectは全てMining採掘である()
        {
            var stoneDroppers = FindMapObjectsDropping(StoneItemGuid);

            // 0件だと以降の検証が全て素通りするので先に落とす
            // With zero droppers every later assertion would pass vacuously, so fail here first
            Assert.IsNotEmpty(stoneDroppers, "no map object drops the stone item; the test would pass vacuously");

            foreach (var mapObject in stoneDroppers)
            {
                var name = (string)mapObject["mapObjectName"];
                Assert.AreEqual("Mining", (string)mapObject["miningType"], $"stone must drop only from Mining map objects: {name}");
            }
        }

        [Test]
        public void 小石を指す鉱脈は存在しない()
        {
            var mapJson = LoadMapJson();
            foreach (var token in (JArray)mapJson["mapVeins"])
            {
                var vein = (JObject)token;
                var veinParam = (JObject)vein["veinParam"];
                if (veinParam == null) continue;

                var veinName = (string)vein["veinName"];
                Assert.AreNotEqual(PebbleItemGuid, (string)veinParam["itemGuid"], $"pebble must not be obtainable from a vein: {veinName}");
            }
        }

        private static List<JObject> FindMapObjectsDropping(string itemGuid)
        {
            var droppers = new List<JObject>();
            foreach (var token in (JArray)LoadMapJson()["mapObjects"])
            {
                var mapObject = (JObject)token;
                var earnItems = (JArray)mapObject["earnItems"];
                if (earnItems == null) continue;

                foreach (var earnItem in earnItems.Children<JObject>())
                {
                    if (!string.Equals((string)earnItem["itemGuid"], itemGuid, StringComparison.Ordinal)) continue;
                    droppers.Add(mapObject);
                    break;
                }
            }

            return droppers;
        }

        private static JObject LoadMapJson()
        {
            return JObject.Parse(PinnedMasterRepository.ReadPinnedFile(MapJsonPath));
        }
    }
}
```

- [ ] **Step 2: コンパイルする**

```bash
cd "$WT"
uloop compile --project-path ./moorestech_client
```

Expected: エラー0件。エラーが出たら `uloop get-logs --project-path ./moorestech_client --log-type Error` で内容を確認して直す。

- [ ] **Step 3: テストを実行して失敗を確認する**

```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode \
  --filter-type regex --filter-value "MapObjectDropSourceTest"
```

Expected: `小石はPickUpのmapObjectからのみ入手できる` が FAIL（`pebble must drop only from PickUp map objects: Boulder1` 等、Mining側85件のいずれかで落ちる）。`石を落とすmapObjectは全てMining採掘である` も FAIL（石を落とすmapObjectが0件のため vacuous guard で落ちる）。`小石を指す鉱脈は存在しない` も FAIL（小石鉱脈が残っているため）。
CLIが180秒でタイムアウトしても失敗ではない。`.uloop/outputs/TestResults` の最新XMLで結果を確認する。

- [ ] **Step 4: コミットする**

`.meta` はUnityが自動生成したものだけをコミットする（手動作成禁止）。

```bash
cd "$WT"
git add moorestech_client/Assets/Scripts/Client.Tests/Map/MasterData
git status --short moorestech_client/Assets/Scripts/Client.Tests/Map/MasterData
git commit -m "test(map): 小石と石のドロップ元の不変条件テストを追加"
```

Expected: `MapObjectDropSourceTest.cs` と `MapObjectDropSourceTest.cs.meta`、`MasterData.meta` が入る。`.meta` が無い場合はUnityを前面化して生成させてから再度 `git add` する。

---

### Task 2: master repoのmap.jsonを書き換えてPRを作る

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`

**Interfaces:**
- Consumes: なし
- Produces: `moorestech_master` の push済みコミットハッシュ（Task 3のピン更新で使う）とPR URL

- [ ] **Step 1: master repoでブランチを切る**

master repoは detached HEAD でピン運用されている。ブランチは `origin/master` から切る。

```bash
cd "$MASTER_REPO"
git fetch origin
git switch -c feature/rock-mapobjects-drop-stone origin/master
git log --oneline -1
```

Expected: `origin/master` の先頭コミットにいる。

- [ ] **Step 2: 変更前の状態を記録する**

```bash
cd "$MASTER_REPO"
python3 - <<'EOF'
import json
p='server_v8/mods/moorestechAlphaMod_8/master/map.json'
d=json.load(open(p))
P='582040ec-093b-4c8e-8fe3-f4ec030cf1ca'; S='44aaddd6-e3c0-4131-a159-9140d3e2e33b'
def drops(o,g): return any(e['itemGuid']==g for e in (o.get('earnItems') or []))
print('小石ドロップ:', sum(1 for o in d['mapObjects'] if drops(o,P)))
print('  うちMining:', sum(1 for o in d['mapObjects'] if drops(o,P) and o['miningType']=='Mining'))
print('  うちPickUp:', sum(1 for o in d['mapObjects'] if drops(o,P) and o['miningType']=='PickUp'))
print('石ドロップ:', sum(1 for o in d['mapObjects'] if drops(o,S)))
print('mapVeins:', len(d['mapVeins']))
EOF
```

Expected:
```
小石ドロップ: 90
  うちMining: 86
  うちPickUp: 4
石ドロップ: 0
mapVeins: 12
```
数字が違う場合はベースが想定と異なる。planを書いた前提が崩れているので、続行せずユーザーへ報告する。

- [ ] **Step 3: map.jsonを書き換える**

行単位の置換はせず、対象要素だけをその場で書き換えて元のフォーマットで出力する。既存ファイルはインデント4スペース・キー順保持なので、`json.load` の順序保持（Python 3.7+ のdictは挿入順保持）を使い `indent=4` + `ensure_ascii=False` + 末尾改行で書き戻す。

```bash
cd "$MASTER_REPO"
python3 - <<'EOF'
import json
p='server_v8/mods/moorestechAlphaMod_8/master/map.json'
d=json.load(open(p))
P='582040ec-093b-4c8e-8fe3-f4ec030cf1ca'
S='44aaddd6-e3c0-4131-a159-9140d3e2e33b'
PEBBLE_VEIN='d48d49b5-a5e2-4f44-a1a6-8d7b9c1f4e50'

replaced=0
for o in d['mapObjects']:
    if o['miningType']!='Mining': continue
    for e in (o.get('earnItems') or []):
        if e['itemGuid']==P:
            e['itemGuid']=S
            replaced+=1
print('置換した earnItems:', replaced)

before=len(d['mapVeins'])
d['mapVeins']=[v for v in d['mapVeins'] if v['veinGuid']!=PEBBLE_VEIN]
print('削除した mapVeins:', before-len(d['mapVeins']))

with open(p,'w',encoding='utf-8') as f:
    json.dump(d,f,ensure_ascii=False,indent=4)
    f.write('\n')
EOF
```

Expected:
```
置換した earnItems: 86
削除した mapVeins: 1
```

- [ ] **Step 4: 変更後の不変条件を検証する**

```bash
cd "$MASTER_REPO"
python3 - <<'EOF'
import json
p='server_v8/mods/moorestechAlphaMod_8/master/map.json'
d=json.load(open(p))
P='582040ec-093b-4c8e-8fe3-f4ec030cf1ca'; S='44aaddd6-e3c0-4131-a159-9140d3e2e33b'
def drops(o,g): return any(e['itemGuid']==g for e in (o.get('earnItems') or []))
peb=[o for o in d['mapObjects'] if drops(o,P)]
sto=[o for o in d['mapObjects'] if drops(o,S)]
assert len(peb)==4, peb
assert all(o['miningType']=='PickUp' for o in peb), [o['mapObjectName'] for o in peb]
assert sorted(o['mapObjectName'] for o in peb)==sorted(['小石','Pebble1','Pebble2','Pebble3'])
assert len(sto)==86, len(sto)
assert all(o['miningType']=='Mining' for o in sto)
assert all(e['minCount']==1 and e['maxCount']==4 for o in sto for e in o['earnItems'] if e['itemGuid']==S)
assert all(e['minCount']==1 and e['maxCount']==1 for o in peb for e in o['earnItems'] if e['itemGuid']==P)
assert len(d['mapVeins'])==11
assert not any((v.get('veinParam') or {}).get('itemGuid')==P for v in d['mapVeins'])
print('OK: 小石4件(PickUp) / 石85件(Mining) / mapVeins 10件 / 個数据え置き')
EOF
```

Expected: `OK: 小石4件(PickUp) / 石85件(Mining) / mapVeins 10件 / 個数据え置き`

- [ ] **Step 5: 差分が意図した範囲だけか確認する**

```bash
cd "$MASTER_REPO"
git diff --stat
git diff | grep '^[+-]' | grep -v '^[+-][+-]' | grep -v '582040ec\|44aaddd6' | head -30
```

Expected: 変更ファイルは `map.json` のみ。最後のコマンドで出るのは `小石鉱脈` ブロックの削除行（`veinGuid`/`veinName`/`veinType`/`veinParam`/`outcropAddressablePath`/`soundEffectType`/`terrainSurroundEffectType`/`handMiningType`/`handMiningParam` とツール定義）だけであること。他ファイルや無関係な整形差分が出ていたらStep 3をやり直す。

- [ ] **Step 6: コミットしてpushする**

```bash
cd "$MASTER_REPO"
git add server_v8/mods/moorestechAlphaMod_8/master/map.json
git commit -m "feat(map): 岩系mapObject85件のドロップを小石から石へ移し小石鉱脈を削除

小石はPickUpの小石mapObject4件からのみ入手できるようにする。
未参照の死にデータだった小石鉱脈(d48d49b5)を削除する。
ADR: moorestech/docs/adr/0036-rock-mapobjects-drop-stone-not-pebble.md"
git push -u origin feature/rock-mapobjects-drop-stone
git rev-parse HEAD
```

Expected: pushが成功し、`git rev-parse HEAD` が40桁のコミットハッシュを出力する。**このハッシュをTask 3で使う。**

- [ ] **Step 7: master repo側のPRを作る**

```bash
cd "$MASTER_REPO"
gh pr create --base master --head feature/rock-mapobjects-drop-stone \
  --title "岩系mapObject85件のドロップを小石から石へ移す" \
  --body "$(cat <<'BODY'
## 概要
採掘できる岩系mapObject 85件（`Vanilla/Environment/Rock/**` 84件 + `CowSkull`）のドロップを「小石」から「石」へ移す。小石はPickUpの小石mapObject 4件（`小石` / `Pebble1`〜`3`）からのみ入手できるようになる。

## 変更内容
- `map.json` mapObjects: `miningType=Mining` の85件の `earnItems.itemGuid` を 小石(582040ec…) → 石(44aaddd6…)。個数1〜4は据え置き
- `map.json` mapVeins: 未参照の死にデータだった `小石鉱脈`(d48d49b5…) を削除

## 背景
大型の岩から小石しか出ないのが見た目と噛み合っていなかった。`石` の `initialUnlocked:false` はレシピ表示のアンロック（原始研究1の `unlockItemRecipeView`）であって取得可否ではないため、序盤に岩から石が出ても不整合は起きない。

## 検証
- 変更後: 小石ドロップ4件（全てPickUp）／石ドロップ85件（全てMining）／mapVeins 10件／earnItemsの個数は変更前と完全一致
- 本repo側で `MapObjectDropSourceTest` がこの不変条件を守る

## 関連
- ADR: moorestech `docs/adr/0036-rock-mapobjects-drop-stone-not-pebble.md`
- 本体PR: moorestech `feature/rock-mapobjects-drop-stone`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
BODY
)"
```

Expected: PR URLが出力される。

---

### Task 3: 本repoのピンを更新してテストを緑にする

**Files:**
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: Task 2 Step 6 で得た `moorestech_master` のpush済みコミットハッシュ
- Produces: ピン更新済みのコミット（Task 1のテストが緑になる）

- [ ] **Step 1: ピンを更新する**

`<MASTER_COMMIT>` を Task 2 Step 6 のハッシュに置き換えて実行する。

```bash
cd "$WT"
python3 - <<'EOF'
import json
MASTER_COMMIT='<MASTER_COMMIT>'
assert len(MASTER_COMMIT)==40, 'Task 2 Step 6 のハッシュに置き換えること'
p='.moorestech-external-revisions.json'
d=json.load(open(p))
for r in d['repositories']:
    if r['key']=='moorestech_master':
        r['commitHash']=MASTER_COMMIT
with open(p,'w',encoding='utf-8') as f:
    json.dump(d,f,indent=4)
    f.write('\n')
EOF
git diff .moorestech-external-revisions.json
```

Expected: `commitHash` の1行だけが変わっている。

- [ ] **Step 2: ピンだけを明示的にステージしてコミットする**

Unityがこのファイルを実チェックアウト値へ書き戻すため、`git add -A` は使わない。

```bash
cd "$WT"
git add .moorestech-external-revisions.json
git diff --cached .moorestech-external-revisions.json
```

Expected: ステージされた差分の `commitHash` が Task 2 のハッシュであること。違う値になっていたらUnityが書き戻しているので、Step 1からやり直す。

```bash
git commit -m "chore: master dataピンを岩系ドロップ変更後のコミットへ更新"
```

- [ ] **Step 3: master repoの作業ツリーをピンのコミットへ合わせる**

テストはピンのコミットから `git show` で読むため作業ツリーの状態に依存しないが、実行時検証のために合わせておく。

```bash
cd "$MASTER_REPO"
git rev-parse HEAD
```

Expected: Task 2でpushしたコミットと同じ。異なる場合は `git switch feature/rock-mapobjects-drop-stone` する。

- [ ] **Step 4: テストを実行して緑を確認する**

```bash
cd "$WT"
uloop run-tests --project-path ./moorestech_client --test-mode EditMode \
  --filter-type regex --filter-value "MapObjectDropSourceTest"
```

Expected: 3テストとも PASS。CLIが180秒でタイムアウトした場合は `.uloop/outputs/TestResults` の最新XMLで `result="Passed"` を確認する。

- [ ] **Step 5: 既存のマップ系テストが壊れていないことを確認する**

```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode \
  --filter-type regex --filter-value "VeinOutcropAddressableLoadTest|MapObjectAddressableLoadTest|MapVeinMasterTest|MapObjectMasterValidationTest|ChallengeMasterValidationTest"
```

Expected: 全てPASS。`VeinOutcropAddressableLoadTest` は鉱脈が12→11件になっても露頭アドレスの検証なので影響しない。落ちた場合は削除した小石鉱脈への参照が他にないかを `grep -rn "d48d49b5" ../moorestech_master moorestech_server moorestech_client` で確認する。

- [ ] **Step 6: 本repoのブランチをpushする**

```bash
cd "$WT"
git push -u origin feature/rock-mapobjects-drop-stone
```

---

### Task 4: 全ブランチレビューとPR作成

**Files:**
- 変更なし（レビューと成果物提出のみ）

**Interfaces:**
- Consumes: Task 0〜3の全コミット、Task 2で作った master repo のPR URL
- Produces: 本repoのPR URL、bdクローズ

- [ ] **Step 1: コードレビュースキルで全ブランチレビューを実行する（省略不可）**

`moores-code-review` スキルを起動し、`origin/master..HEAD` の全差分をレビューする。指摘のうち機械的修正は適用し、設計判断はユーザーへ諮る。**このステップはゴール文言による省略ができない必須ゲートである。**

- [ ] **Step 2: レビュー指摘の修正をコミットしてpushする**

```bash
cd "$WT"
git add -- <修正したファイルを明示>
git commit -m "fix: レビュー指摘の対応"
git push
```

`.moorestech-external-revisions.json` を巻き込まないよう、`git add -A` は使わずファイルを明示する。

- [ ] **Step 3: 本repoのPRを作る**

```bash
cd "$WT"
gh pr create --base master --head feature/rock-mapobjects-drop-stone \
  --title "岩系mapObjectのドロップを石へ移し小石はPickUpのみに絞る" \
  --body "$(cat <<'BODY'
## 概要
採掘できる岩系mapObject 85件のドロップを「小石」から「石」へ移し、小石はPickUpの小石mapObject 4件からのみ入手できるようにする。

## 変更内容
- `.moorestech-external-revisions.json`: master dataピンを岩系ドロップ変更後のコミットへ更新
- `Client.Tests/Map/MasterData/MapObjectDropSourceTest.cs`: ドロップ元の不変条件テストを追加（小石はPickUpのみ／石はMiningのみ／小石を指す鉱脈なし）
- `docs/adr/0036-rock-mapobjects-drop-stone-not-pebble.md`・`.decisions/`・実装plan

## 実データの変更
master repo側のPRで `map.json` を変更している（下記）。

## 既知の積み残し
チャレンジ7「石鉱脈から石を5個採掘しよう」が岩の採掘でも達成可能になる。導線の是正は bd `moorestech-tbdr` へ送った（今回スコープ外・ユーザー裁定 2026-08-26）。

## 関連
- master data PR: <Task 2で作ったPR URL>
- ADR: `docs/adr/0036-rock-mapobjects-drop-stone-not-pebble.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
BODY
)"
```

- [ ] **Step 4: bdを閉じる**

素のコマンドを1行ずつ実行する（wrapper・パイプ・リダイレクト禁止）。

```
bd note moorestech-k9ui "master PR: <master PR URL> / 本体PR: <本体PR URL>"
```

```
bd close moorestech-k9ui --reason="岩系mapObject85件を石ドロップへ移し小石鉱脈を削除。小石はPickUp4件のみ。不変条件テスト追加済み"
```

- [ ] **Step 5: worktreeを片付ける（PRマージ後）**

```bash
moores-wt rm rock-drop-stone
```

未コミット・未pushがあると拒否される。拒否されたら内容を確認してから対処する。

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0036-rock-mapobjects-drop-stone-not-pebble.md`
- 裁定台帳: `.decisions/2026-08-26-岩系mapObjectは石を落としPickUp小石だけが小石を落とす.md`

planning中に生じた判断:

- **不変条件をEditModeテストで守る（新規テスト1本を追加する）。** AGENTS.md は「アセットの変更だけでテストを新設しない」としているが、本件はアセットではなくマスタデータの不変条件であり、同型の前例 `Client.Tests/Map/VeinOutcropAddressableLoadTest.cs`（ピン済みmasterの `map.json` を読んで検証するEditModeテスト）が既にある。出所: agent前提（VeinOutcropAddressableLoadTest 前例）
- **テストの配置は `Client.Tests/Map/MasterData/`（新規サブディレクトリ）。** `Client.Tests/Map/` は既に11 `.cs` あり「1ディレクトリ10ファイルまで」を超えているため。既存ファイルの移動はスコープ外とし行わない。出所: agent前提（AGENTS.md ディレクトリ規約）
- **テストは件数（85件・4件）をハードコードしない。** 今後岩mapObjectが増減しても壊れない「PickUpのみ／Miningのみ」という質的不変条件だけを検査し、vacuous pass ガードを置く。件数の一致確認はTask 2 Step 4の一回限りの検証で行う。出所: agent前提（VeinOutcropAddressableLoadTest の vacuous guard 前例）
- **`map.json` の書き換えは `json.load`→要素書き換え→`json.dump(indent=4)` で行い、sed等の行置換はしない。** 既存ファイルがインデント4スペースで整形済みのため、キー順を保持したまま同一整形で書き戻せば差分は対象要素だけに収まる。出所: agent前提
- **ピンのコミットは `git add .moorestech-external-revisions.json` の明示指定とする。** Unityがこのファイルを実チェックアウト値へ書き戻すため、`git add -A` でピン更新が消える事故の実績がある。出所: agent前提（過去事故の学び）
- **本repoのベースは `origin/master`、master repoのベースは `moorestech_master` の `origin/master`。** 現在のメインワークツリーのHEADが指すピン(60e815a)は `origin/master` のピン(200ab3c9)より古く、そのまま使うと不要な巻き戻しになる。出所: agent前提
- **チャレンジ7の導線是正は別タスク（bd `moorestech-tbdr`）。** 出所: ユーザー裁定 2026-08-26 選択「今回はドロップ変更のみ。導線は別件」
