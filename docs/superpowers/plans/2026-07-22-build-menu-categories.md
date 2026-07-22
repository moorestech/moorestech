# 建設メニューカテゴリ化（Satisfactory風） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Web UI建設メニューを「blockCategoriesマスタ定義による左サイドバー大カテゴリ + サブカテゴリ見出しグリッド + 検索 + 固定詳細プレビュー」へ再設計し、同時にレガシー実装をwebui-design様式（GamePanel/SlotFrame/PanelCloseButton）へ移行する。

**Architecture:** マスタ（新設blockCategories.yml + blocks.ymlのcategory必須化/subCategory追加）→ Unity側Catalog/DTO（Tooltip廃止・構造化RequiredItems・categories定義リスト配信）→ webui（zod契約拡張 + 純関数grouping + 様式準拠コンポーネント群）の一方向パイプラインに、既存フィールドを足すだけ。新しい制御フロー・逆流経路は作らない。

**Spec:** `docs/superpowers/specs/2026-07-21-build-menu-categories-design.md`（Fable 4観点レビュー反映済み最終版。判断に迷ったらspecが正）

**Tech Stack:** mooresmaster SourceGenerator / Unity C# (uloop) / zod + React + Mantine (vitest, Playwright)

## Global Constraints

- 1ファイル200行以下・partial絶対禁止・1ディレクトリ10ファイルまで（AGENTS.md）
- イベントはUniRx（Action禁止）・単純getter/setter禁止（値SetはSetHogeメソッド）・デフォルト引数禁止
- 主要処理に日本語→英語の2行セットコメント（各1行厳守）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client`。「Unity is reloading」エラー時は45秒待ってリトライ
- .metaファイル手動作成禁止。Prefab/シーン等のYAML直接編集禁止
- `optional: true`・`?? Default`フォールバック・ローダー欠損補完は禁止（PR978原則）。必須化+全JSON一括更新が正規手順
- Core.Masterはマスタ生ロード・保持・ID⇔GUID解決のみ（`.claude/rules/core-master.md`）
- webui: 表示リテラルは全て `t()` 経由（lint: no-jsx-visible-literal。キー=日本語原文、辞書ファイル追加は不要）
- webui: webui-designホワイトリスト外の表現禁止。視覚寸法は固定長トークン、%指定禁止。**様式が先、実装が後**（Task 8をwebui実装より先に行う）
- `../moorestech_master` は別リポジトリ。そちらの変更はそちらで個別にコミットする
- 作業ブランチ: `feature/build-menu-categories` を master から作成（共有ワーキングツリーの他セッション作業と混ぜない。着手時に必ず `pwd` と `git status` を確認し、無関係の変更ファイルは絶対にコミットに含めない）

## 配置と前例（spec-architecture-review済み）

| 配置決定 | 前例（パス） |
|---|---|
| 新マスタyml `blockCategories.yml` の書式 | `VanillaSchema/fluids.yml`（最小構成マスタ） |
| `BlockCategoryMaster` を Core.Master に新設（生ロード+一意性検証のみ） | `Core.Master/FluidMaster.cs` |
| MasterHolderロード（BlockMasterより前・基盤セクション） | `Core.Master/MasterHolder.cs:30-42` のロード順コメント様式 |
| blocks→blockCategories参照のロード時検証 | `Core.Master/Validator/BlockMasterUtil.cs:249` `BlockDestructionCategoryValidation` |
| 非ブロックエントリの種別導出カテゴリ定数は Client.Game の Catalog 側 | ドメイン判断は具体側（AGENTS.md 汎用基盤原則）。Factory/DTOは写すだけ |
| 大型2列パネルレイアウト | `moorestech_web/webui/src/features/blockInventory/style.module.css` `.panelLarge`（`grid-column: viewer-start/items-end; height:525px`） |
| 固定高詳細プレビュー+FadeRule+グリッド | `features/blockInventory/details/machine/MachineRecipeSelectionTab.tsx` |
| カスタムスクロールバー | `features/recipe/panels/ItemListPanel.module.css:10-30`（Mantine ScrollArea上書き。トーンのみネイビーへ） |
| ModeSwitch縦利用 | `shared/ui/ModeSwitch` は `orientation="vertical"` 実装済み（CSS `.root[data-orientation="vertical"]`） |

データフロー: `マスタJSON →(MasterHolder)→ BuildMenuEntryCatalog →(DtoFactory)→ BuildMenuTopic publish →(zod)→ webui描画`。本計画は既存パイプラインの各駅にフィールドを足す「書き手の拡張」のみで、交差点（bool戻り・迂回セッター・並行経路）は追加しない。

機能パリティ（死活表）: メニュー開閉=生存 / エントリ選択（5種とも`build_menu.select`契約不変）=生存 / blueprint右クリック削除（`blueprint.delete`契約不変）=生存 / ホバー詳細=Mantine Tooltip→固定プレビューへ置換（spec承認済み） / uGUI側メニュー=残置（ToolTipText→Labelへ縮退するが、UIはWeb移行完了方針によりuGUIは非対応で確定済み）。

---

### Task 1: blockCategories スキーマ新設と生成確認

**Files:**
- Create: `VanillaSchema/blockCategories.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/csc.rsp`（1行追記）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（dummyText更新）

**Interfaces:**
- Produces: 生成モデル `Mooresmaster.Model.BlockCategoriesModule.BlockCategories`（`BlockCategoryMasterElement[] Data`、各要素 `string Name` / `BlockSubCategoryElement[] SubCategories`、各 `string Name`）とローダー `Mooresmaster.Loader.BlockCategoriesModule.BlockCategoriesLoader.Load(JToken)`

- [ ] **Step 1: blockCategories.yml を作成**

```yaml
# NOTE このファイルを編集した場合、C#コードはSourceGeneratorによって自動生成されます
# 建設メニューのカテゴリ定義。配列順がそのまま表示順
# Build menu category definitions. Array order is the display order
id: blockCategories
type: object
isDefaultOpen: true
properties:
- key: data
  type: array
  openedByDefault: true
  overrideCodeGeneratePropertyName: BlockCategoryMasterElement
  items:
    type: object
    properties:
    - key: name
      type: string
    - key: subCategories
      type: array
      overrideCodeGeneratePropertyName: BlockSubCategoryElement
      items:
        type: object
        properties:
        - key: name
          type: string
```

ネスト配列の書式が通らない場合は `VanillaSchema/blocks.yml:1051` 付近の `blockDestructionCategories`（ネスト配列の実例）の書式に合わせて修正する。

- [ ] **Step 2: csc.rsp に追記**

`moorestech_server/Assets/Scripts/Core.Master/csc.rsp` の既存行群に追加:

```
/additionalfile:Assets/../../VanillaSchema/blockCategories.yml
```

- [ ] **Step 3: _CompileRequester.cs の dummyText を現在日時文字列に変更**

- [ ] **Step 4: コンパイルして生成を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。（`BlockCategories` 型はTask 4で参照して初めて実在確認できる。ここではスキーマ構文エラーが無いことの確認）

- [ ] **Step 5: Commit**

```bash
git add VanillaSchema/blockCategories.yml moorestech_server/Assets/Scripts/Core.Master/csc.rsp moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "feat(master): blockCategoriesマスタスキーマを新設"
```

---

### Task 2: blocks.yml の category 必須化 + subCategory 追加 + 手書きコンストラクタ追従

**Files:**
- Modify: `VanillaSchema/blocks.yml:186-188`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlaceSystemUtilCalcPlacePointTest.cs:20`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/CommonBlockPlacePointCalculatorTest.cs:70`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorPlacePointCalculatorTest.cs:103,144`

**Interfaces:**
- Produces: `BlockMasterElement` に必須 `Category` / `SubCategory`（string）。コンストラクタ位置は `[6]category, [7]subCategory, [8]sortPriority, ...`（subCategoryをcategory直後に置く）

- [ ] **Step 1: blocks.yml を編集**

`blocks.yml:186-188` の既存定義:

```yaml
    - key: category
      type: string
      optional: true
```

を以下に置換（`optional: true` 削除・foreignKey付与・subCategory追加。foreignKeyのクロスファイル書式は `machineRecipes.yml:21` が前例）:

```yaml
    - key: category
      type: string
      foreignKey:
        schemaId: blockCategories
        foreignKeyIdPath: /data/[*]/name
        displayElementPath: /data/[*]/name
    - key: subCategory
      type: string
```

（subCategoryはカテゴリ内ネストのためforeignKeyパスで表現できず、実行時検証はTask 4のC#バリデーションが担う）

- [ ] **Step 2: _CompileRequester.cs の dummyText を更新しコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 手書き `new BlockMasterElement(` 4箇所が CS7036 で失敗する（これが期待動作。生成コンストラクタの引数が増えた証拠）

- [ ] **Step 3: 手書きコンストラクタ4箇所に引数追加**

現行13引数（例 `BeltConveyorPlacePointCalculatorTest.cs:144`）:

```csharp
var blockMasterElement = new BlockMasterElement(0, Guid.Empty, "TestBlock", "TestBlockType", null, null, null, 0, false, Vector3Int.one, null, null, null);
```

`[6]category` の `null` を `"テスト"` にし、直後に `"テスト"` を挿入して14引数へ（4箇所とも同様。コンパイルエラーの実引数位置を正として合わせる）:

```csharp
var blockMasterElement = new BlockMasterElement(0, Guid.Empty, "TestBlock", "TestBlockType", null, null, "テスト", "テスト", 0, false, Vector3Int.one, null, null, null);
```

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 5: Commit**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs "moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/"
git commit -m "feat(master): blocksのcategoryを必須化しsubCategoryを追加"
```

---

### Task 3: 全mod JSON更新（blocks.json付与 + blockCategories.json新設）

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（73ブロック）
- Create: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blockCategories.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`（61ブロック）
- Create: 同ディレクトリ `blockCategories.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json`（29ブロック）
- Create: 同ディレクトリ `blockCategories.json`
- Modify: `mooresmaster/mooresmaster.SandBox/schema/blocks.json`、`mooresmaster/mooresmaster.SandBox/TestMod/blocks.json`（各1ブロック、手動で `"category": "テスト", "subCategory": "テスト"` を追加）

**Interfaces:**
- Produces: 全blocks.jsonの各data要素に `category` / `subCategory`。プレイ用blockCategories.jsonはspec §1.5の定義。テストmodは「テスト」カテゴリ（subCategories=出現blockType名）+ 種別導出用の固定4ペア

- [ ] **Step 1: プレイ用mod変換スクリプトをscratchpadに作成し実行**

spec §1.3の分類表を完全転記したスクリプト（キーは実データの完全名73件。1件でも欠け・余りがあればexit 1で大声で失敗する）:

```python
#!/usr/bin/env python3
import json, sys

MAPPING = {
    # 採掘
    "風力掘削機": ("採掘", "採掘機"), "原始的な採掘機": ("採掘", "採掘機"),
    "鉄の採掘機": ("採掘", "採掘機"), "電気採掘機": ("採掘", "採掘機"),
    "油井": ("採掘", "液体採取"),
    # 生産
    "石窯": ("生産", "原始加工"), "原始的な粉砕機": ("生産", "原始加工"),
    "原始的な加工機": ("生産", "原始加工"), "原始的な組立機": ("生産", "原始加工"),
    "ふいご付き精錬炉": ("生産", "原始加工"),
    "電気汎用工作装置": ("生産", "電気機械"), "電気粉砕機": ("生産", "電気機械"), "電気炉": ("生産", "電気機械"),
    "酸素発生装置": ("生産", "化学"), "石油蒸留機": ("生産", "化学"), "化学プラント": ("生産", "化学"),
    "EUV露光式半導体製造装置": ("生産", "半導体"),
    # 動力
    "木のシャフト": ("動力", "シャフト"), "木の縦シャフト": ("動力", "シャフト"),
    "木のシャフトボックス": ("動力", "シャフト"), "鉄のシャフト": ("動力", "シャフト"),
    "鉄の縦シャフト": ("動力", "シャフト"), "鉄のシャフトボックス": ("動力", "シャフト"),
    "木の歯車": ("動力", "歯車"), "鉄の歯車": ("動力", "歯車"), "大きな鉄の歯車": ("動力", "歯車"),
    "歯車チェーンポール": ("動力", "チェーンポール"), "コンパクト歯車チェーンポール": ("動力", "チェーンポール"),
    "燃料式風車": ("動力", "動力源"), "ボイラー": ("動力", "動力源"), "蒸気機関": ("動力", "動力源"),
    # 電力
    "回転発電機": ("電力", "発電"), "ガソリンエンジン発電機": ("電力", "発電"),
    "回転生成機": ("電力", "変換"),
    "電柱": ("電力", "送電"), "高圧電柱": ("電力", "送電"), "広範囲電柱": ("電力", "送電"),
    # 物流
    "直線歯車ベルトコンベア": ("物流", "歯車コンベア"), "上り歯車ベルトコンベア": ("物流", "歯車コンベア"),
    "下り歯車ベルトコンベア": ("物流", "歯車コンベア"), "歯車コンベア分岐機": ("物流", "歯車コンベア"),
    "鉄の歯車ベルトコンベア": ("物流", "歯車コンベア"), "鉄の上り歯車ベルトコンベア": ("物流", "歯車コンベア"),
    "鉄の下り歯車ベルトコンベア": ("物流", "歯車コンベア"), "鉄の歯車ベルトコンベア分岐機": ("物流", "歯車コンベア"),
    "ベルトコンベア": ("物流", "電気コンベア"), "上りベルトコンベア": ("物流", "電気コンベア"),
    "下りベルトコンベア": ("物流", "電気コンベア"), "ベルトコンベア分岐器": ("物流", "電気コンベア"),
    "高速ベルトコンベア": ("物流", "電気コンベア"), "上り高速ベルトコンベア": ("物流", "電気コンベア"),
    "下り高速ベルトコンベア": ("物流", "電気コンベア"), "高速ベルトコンベア分岐器": ("物流", "電気コンベア"),
    "フィルター分岐器": ("物流", "仕分け"),
    "木のチェスト": ("物流", "チェスト"), "木のコンベアチェスト": ("物流", "チェスト"),
    "鉄のコンベアチェスト": ("物流", "チェスト"), "鉄のミニコンベアチェスト": ("物流", "チェスト"),
    # 液体
    "鉄のパイプ": ("液体", "パイプ"), "鋼鉄のパイプ": ("液体", "パイプ"),
    "液体タンク": ("液体", "タンク"),
    "歯車ポンプ": ("液体", "ポンプ"),
    # 輸送
    "貨物プラットフォーム": ("輸送", "鉄道"), "蒸気機関車駅": ("輸送", "鉄道"),
    "レール橋脚": ("輸送", "鉄道"), "液体プラットフォーム": ("輸送", "鉄道"),
    # 建材
    "基本土台": ("建材", "土台"), "アスファルト土台": ("建材", "土台"),
    "クリーンルームブロック": ("建材", "クリーンルーム"), "クリーンルームドア": ("建材", "クリーンルーム"),
    "クリーンルームアイテムハッチ": ("建材", "クリーンルーム"), "クリーンルームパイプコネクタ": ("建材", "クリーンルーム"),
    "クリーンルーム空気清浄機": ("建材", "クリーンルーム"),
}

# spec §1.5 の定義（配列順=表示順）。種別導出用の 輸送/車両・ツール・ブループリント を含む
CATEGORIES = [
    ("採掘", ["採掘機", "液体採取"]),
    ("生産", ["原始加工", "電気機械", "化学", "半導体"]),
    ("動力", ["シャフト", "歯車", "チェーンポール", "動力源"]),
    ("電力", ["発電", "変換", "送電"]),
    ("物流", ["歯車コンベア", "電気コンベア", "仕分け", "チェスト"]),
    ("液体", ["パイプ", "ポンプ", "タンク"]),
    ("輸送", ["鉄道", "車両"]),
    ("建材", ["土台", "クリーンルーム"]),
    ("ツール", ["接続", "ブループリント"]),
    ("ブループリント", ["保存済み"]),
]

path = sys.argv[1]
data = json.load(open(path, encoding="utf-8"))
names = [b["name"] for b in data["data"]]
missing = set(names) - set(MAPPING)
unused = set(MAPPING) - set(names)
if missing or unused:
    sys.exit(f"mapping mismatch. missing={missing} unused={unused}")
for b in data["data"]:
    cat, sub = MAPPING[b["name"]]
    # sortPriorityの手前(無ければ末尾)に挿入されるようdict再構築はせずキー追加のみ
    b["category"] = cat
    b["subCategory"] = sub
json.dump(data, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=4)

cat_path = path.replace("blocks.json", "blockCategories.json")
cat_json = {"data": [{"name": c, "subCategories": [{"name": s} for s in subs]} for c, subs in CATEGORIES]}
json.dump(cat_json, open(cat_path, "w", encoding="utf-8"), ensure_ascii=False, indent=4)
print("ok", len(names))
```

Run: `python3 <scratchpad>/apply_categories.py /Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`
Expected: `ok 73`

注意: 既存blocks.jsonのインデント・キー順の大規模差分が出ないよう、実行前に `git -C ../moorestech_master diff --stat` で差分がcategory/subCategory追加とblockCategories.json新規のみであることを確認する。インデントが既存と違う場合はスクリプトのindentを実ファイルに合わせる。

- [ ] **Step 2: テストmod変換スクリプトを作成し実行**

category="テスト"、subCategory=blockType名。blockCategories.jsonは出現blockType＋固定4ペアを機械生成:

```python
#!/usr/bin/env python3
import json, sys

path = sys.argv[1]
data = json.load(open(path, encoding="utf-8"))
types = []
for b in data["data"]:
    b["category"] = "テスト"
    b["subCategory"] = b["blockType"]
    if b["blockType"] not in types:
        types.append(b["blockType"])
json.dump(data, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=4)

# 種別導出カテゴリ（trainCar/connectTool/blueprintCopy/blueprint）が参照する固定ペアも定義する
cats = [{"name": "テスト", "subCategories": [{"name": t} for t in types]},
        {"name": "輸送", "subCategories": [{"name": "車両"}]},
        {"name": "ツール", "subCategories": [{"name": "接続"}, {"name": "ブループリント"}]},
        {"name": "ブループリント", "subCategories": [{"name": "保存済み"}]}]
json.dump({"data": cats}, open(path.replace("blocks.json", "blockCategories.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=4)
print("ok", len(data["data"]))
```

Run（2回）:
- `python3 <scratchpad>/apply_test_categories.py moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json` → `ok 61`
- `python3 <scratchpad>/apply_test_categories.py moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json` → `ok 29`

- [ ] **Step 3: SandBoxの2ファイルを手動編集**

`mooresmaster/mooresmaster.SandBox/schema/blocks.json` と `mooresmaster/mooresmaster.SandBox/TestMod/blocks.json` の各1ブロックへ、既存フィールドの並びに合わせて `"category": "テスト", "subCategory": "テスト"` を追加（blockCategories.jsonは不要。SandBoxはMasterHolderロード対象外）。

- [ ] **Step 4: 付与網羅を機械確認**

```bash
python3 -c "
import json
for p in ['/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json',
          'moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json',
          'moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json']:
    d = json.load(open(p))
    missing = [b['name'] for b in d['data'] if 'category' not in b or 'subCategory' not in b]
    print(p, len(d['data']), 'missing:', missing)
"
```

Expected: 3行とも `missing: []`

- [ ] **Step 5: 新規blocks.jsonのUnity .meta生成のためエディタでリフレッシュされることを確認し、Commit**

masterリポジトリ（別リポジトリ・個別コミット）:

```bash
git -C /Users/katsumi/moorestech_master add server_v8/mods/moorestechAlphaMod_8/master/blocks.json server_v8/mods/moorestechAlphaMod_8/master/blockCategories.json
git -C /Users/katsumi/moorestech_master commit -m "feat(master): 建設メニューカテゴリをblocksへ付与しblockCategoriesを新設"
```

本体リポジトリ（blockCategories.jsonの.metaはUnity起動時に自動生成されるので、生成後に一緒にコミット）:

```bash
git add moorestech_server/Assets/Scripts/Tests.Module/TestMod/ moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/ mooresmaster/mooresmaster.SandBox/
git commit -m "feat(master): テストmodへカテゴリ付与とblockCategories追加"
```

---

### Task 4: BlockCategoryMaster 新設 + MasterHolder 登録 + ロード時バリデーション（TDD）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Core.Master/BlockCategoryMaster.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs`
- Test: creating-server-testsスキルの配置規約に従い、既存のCore.Master系Validatorテストと同じ場所へ `BlockCategoryMasterTest.cs` を追加（前例が無ければUnitTest系ディレクトリ）

**Interfaces:**
- Consumes: Task 1の生成モデル `BlockCategories` / `BlockCategoriesLoader`
- Produces: `MasterHolder.BlockCategoryMaster`（static）、`BlockCategoryMaster.BlockCategories`（生モデル）、`bool BlockCategoryMaster.Contains(string category, string subCategory)`

- [ ] **Step 1: 失敗テストを書く**（creating-server-testsスキル参照。テストクラスの雛形・命名はスキルに従う）

```csharp
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

public class BlockCategoryMasterTest
{
    [Test]
    public void 重複カテゴリ名はバリデーションで失敗する()
    {
        var json = JToken.Parse(@"{""data"":[
            {""name"":""採掘"",""subCategories"":[{""name"":""採掘機""}]},
            {""name"":""採掘"",""subCategories"":[{""name"":""液体採取""}]}]}");
        var master = new BlockCategoryMaster(json);
        Assert.IsFalse(master.Validate(out var logs));
        Assert.IsTrue(logs.Contains("duplicate"));
    }

    [Test]
    public void カテゴリ内サブカテゴリ重複はバリデーションで失敗する()
    {
        var json = JToken.Parse(@"{""data"":[
            {""name"":""採掘"",""subCategories"":[{""name"":""採掘機""},{""name"":""採掘機""}]}]}");
        var master = new BlockCategoryMaster(json);
        Assert.IsFalse(master.Validate(out var logs));
    }

    [Test]
    public void 定義済みペアはContainsがtrueを返す()
    {
        var json = JToken.Parse(@"{""data"":[
            {""name"":""採掘"",""subCategories"":[{""name"":""採掘機""}]}]}");
        var master = new BlockCategoryMaster(json);
        Assert.IsTrue(master.Validate(out _));
        master.Initialize();
        Assert.IsTrue(master.Contains("採掘", "採掘機"));
        Assert.IsFalse(master.Contains("採掘", "未定義"));
        Assert.IsFalse(master.Contains("未定義", "採掘機"));
    }
}
```

- [ ] **Step 2: コンパイルしてテストが失敗（コンパイルエラー=BlockCategoryMaster未定義）することを確認**

- [ ] **Step 3: BlockCategoryMaster.cs を実装**（FluidMaster.cs前例に準拠）

```csharp
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.BlockCategoriesModule;
using Mooresmaster.Model.BlockCategoriesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class BlockCategoryMaster : IMasterValidator
    {
        public readonly BlockCategories BlockCategories;

        private HashSet<(string category, string subCategory)> _definedPairs;

        public BlockCategoryMaster(JToken jToken)
        {
            BlockCategories = BlockCategoriesLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            // カテゴリ名・カテゴリ内サブカテゴリ名の一意性を検証する
            // Validate uniqueness of category names and sub category names within a category
            errorLogs = string.Empty;
            var categoryNames = BlockCategories.Data.Select(c => c.Name).ToList();
            foreach (var duplicated in categoryNames.GroupBy(n => n).Where(g => g.Count() > 1))
                errorLogs += $"[BlockCategoryMaster] duplicate category name:{duplicated.Key}\n";

            foreach (var category in BlockCategories.Data)
            foreach (var duplicated in category.SubCategories.Select(s => s.Name).GroupBy(n => n).Where(g => g.Count() > 1))
                errorLogs += $"[BlockCategoryMaster] duplicate subCategory:{duplicated.Key} in category:{category.Name}\n";

            return errorLogs == string.Empty;
        }

        public void Initialize()
        {
            // 参照整合チェック用にカテゴリ/サブカテゴリの全ペアを索引化する
            // Build a lookup of all category and sub category pairs for reference validation
            _definedPairs = new HashSet<(string, string)>();
            foreach (var category in BlockCategories.Data)
            foreach (var subCategory in category.SubCategories)
                _definedPairs.Add((category.Name, subCategory.Name));
        }

        public bool Contains(string category, string subCategory)
        {
            return _definedPairs.Contains((category, subCategory));
        }
    }
}
```

（生成モデルのプロパティ名 `Data` / `SubCategories` / `Name` が異なる場合は生成コードのコンパイルエラーメッセージを正として合わせる。`IMasterValidator` のシグネチャは `FluidMaster.cs:37-45` と同一）

- [ ] **Step 4: MasterHolder.cs へ登録**

static プロパティ群（`MasterHolder.cs:10-21`）へ追加:

```csharp
        public static BlockCategoryMaster BlockCategoryMaster { get; private set; }
```

`Load()` の基盤Masterセクション（`CharacterMaster` ロードの直後・`BlockMaster` ロードより前）へ追加:

```csharp
            BlockCategoryMaster = new BlockCategoryMaster(GetJson(masterJsonFileContainer, new JsonFileName("blockCategories")));
            InitializeMaster(BlockCategoryMaster);
```

`BlockMaster` ロードの直前の依存コメント（39-40行の様式）へ「BlockCategoryMaster依存（category/subCategoryの参照を検証）」を追記。

- [ ] **Step 5: BlockMasterUtil.cs へ参照整合バリデーション追加**

`Validate(Blocks blocks, out string errorLogs)`（`BlockMasterUtil.cs:11-21`）の観点合算へ `BlockCategoryReferenceValidation()` を追加し、`BlockDestructionCategoryValidation`（249行）と同じ形式のローカル/privateメソッドとして実装:

```csharp
        private static string BlockCategoryReferenceValidation(Blocks blocks)
        {
            // category/subCategoryペアがblockCategoriesに定義済みであることを検証する
            // Validate that each category and subCategory pair is defined in blockCategories
            var logs = string.Empty;
            foreach (var block in blocks.Data)
            {
                if (!MasterHolder.BlockCategoryMaster.Contains(block.Category, block.SubCategory))
                    logs += $"[BlockMaster] Block:{block.Name} has undefined category pair. category:{block.Category} subCategory:{block.SubCategory}\n";
            }
            return logs;
        }
```

- [ ] **Step 6: コンパイル + テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockCategoryMasterTest"`
Expected: 3テストPASS

- [ ] **Step 7: 既存テストを広めに回す**（マスタロード全滅が無いことの確認。MooresmasterLoaderExceptionが出たらJSON更新漏れ）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MasterLoad|BlockMaster|CombinedTest"`
Expected: 全PASS

- [ ] **Step 8: Commit**

```bash
git add moorestech_server/Assets/Scripts/Core.Master/ "moorestech_server/Assets/Scripts/Tests.Module/" 
git commit -m "feat(master): BlockCategoryMasterとcategory参照バリデーションを追加"
```

---

### Task 5: BuildMenuEntry 構造化（tooltip撤去・Label/カテゴリ/建設コスト追加）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuEntry.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuEntryCatalog.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuView.cs`（uGUI追従・ToolTipText→Label）

**Interfaces:**
- Consumes: `MasterHolder.ItemMaster.GetItemId(Guid)`（ItemMaster.cs:70）、`BlockMasterElement.Category/SubCategory`（Task 2）、`MasterHolder.BlockCategoryMaster`
- Produces: `BuildMenuEntry`（readonly struct）: `IPlacementTarget Target; ItemViewData IconView; string Label; string Category; string SubCategory; IReadOnlyList<BuildMenuEntry.RequiredItem> RequiredItems`。ネスト `readonly struct RequiredItem { ItemId ItemId; int Count; }`。種別導出定数（trainCar=輸送/車両、connectTool=ツール/接続、blueprintCopy=ツール/ブループリント、blueprint=ブループリント/保存済み）はCatalogに定義

- [ ] **Step 1: BuildMenuEntry.cs を書き換え**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Core.Master;

namespace Client.Game.InGame.UI.BuildMenu
{
    // 建設メニュー1エントリの表示・分類情報。tooltip文字列は持たず構造化データのみ持つ
    // A single build menu entry. Holds structured data instead of a preformatted tooltip string
    public readonly struct BuildMenuEntry
    {
        public readonly IPlacementTarget Target;
        public readonly ItemViewData IconView;
        public readonly string Label;
        public readonly string Category;
        public readonly string SubCategory;
        public readonly IReadOnlyList<RequiredItem> RequiredItems;

        public BuildMenuEntry(IPlacementTarget target, ItemViewData iconView, string label, string category, string subCategory, IReadOnlyList<RequiredItem> requiredItems)
        {
            Target = target;
            IconView = iconView;
            Label = label;
            Category = category;
            SubCategory = subCategory;
            RequiredItems = requiredItems;
        }

        public readonly struct RequiredItem
        {
            public readonly ItemId ItemId;
            public readonly int Count;

            public RequiredItem(ItemId itemId, int count)
            {
                ItemId = itemId;
                Count = count;
            }
        }
    }
}
```

（既存ファイルのusing・namespace・IPlacementTarget/ItemViewDataの参照形は現行ファイルを正として維持する）

- [ ] **Step 2: BuildMenuEntryCatalog.cs を改修**

- `CreateBlockToolTip` / `CreateTrainCarToolTip` / `ConstructionCostTexts`（87-115行）を削除
- 種別導出カテゴリ定数をクラス先頭へ追加:

```csharp
        // 非ブロックエントリの固定カテゴリ（blockCategories定義に同名ペアが必要。不一致はDtoFactoryのガードで検出）
        // Fixed categories for non-block entries. Pairs must exist in blockCategories; mismatch is caught by the DtoFactory guard
        private const string TrainCarCategory = "輸送";
        private const string TrainCarSubCategory = "車両";
        private const string ConnectToolCategory = "ツール";
        private const string ConnectToolSubCategory = "接続";
        private const string BlueprintCopyCategory = "ツール";
        private const string BlueprintCopySubCategory = "ブループリント";
        private const string BlueprintCategory = "ブループリント";
        private const string BlueprintSubCategory = "保存済み";
```

- RequiredItems変換ヘルパーを追加（block/trainCar共用。マスタの `RequiredItems` はoptionalでnullがあり得るため空リスト化のみ行う。値のデフォルト補完はしない）:

```csharp
        private static List<BuildMenuEntry.RequiredItem> ToRequiredItems(IEnumerable<(Guid itemGuid, int count)> requiredItems)
        {
            // マスタのItemGuidを通信・表示用の揮発ItemIdへ解決する
            // Resolve master ItemGuids into volatile ItemIds for wire and display use
            var results = new List<BuildMenuEntry.RequiredItem>();
            if (requiredItems == null) return results;
            foreach (var (itemGuid, count) in requiredItems)
            {
                results.Add(new BuildMenuEntry.RequiredItem(MasterHolder.ItemMaster.GetItemId(itemGuid), count));
            }
            return results;
        }
```

- 各 `entries.Add(...)` を新コンストラクタへ変更:
  - ブロック(44行付近): `new BuildMenuEntry(target, iconView, blockMaster.Name, blockMaster.Category, blockMaster.SubCategory, ToRequiredItems(blockMaster.RequiredItems?.Select(r => (r.ItemGuid, r.Count))))`
  - 車両(53行付近): label=`iconView.ItemName`、`TrainCarCategory/TrainCarSubCategory`、RequiredItemsは `trainCarMaster.RequiredItems` から同様に変換
  - connectTool(64行付近): label=`connectTool.Name`、`ConnectToolCategory/ConnectToolSubCategory`、RequiredItems=空リスト
  - blueprintCopy(69行付近): label=`"ブループリントコピー"`（現行文字列を維持）、`BlueprintCopyCategory/BlueprintCopySubCategory`、空リスト
  - blueprint(75行付近): label=BP名、`BlueprintCategory/BlueprintSubCategory`、空リスト
- 列挙順・`IsSlopeBlock`除外・unlock判定・`FreeBlockPlacement` は一切変更しない

- [ ] **Step 3: BuildMenuView.cs（uGUI）の ToolTipText 参照を Label へ置換**

`BuildMenuView.cs:108-109` の `SetTextOnly(entry.ToolTipText, entry.ToolTipText)` → `SetTextOnly(entry.Label, entry.Label)`、`SetItem(entry.IconView, 0, entry.ToolTipText)` → `SetItem(entry.IconView, 0, entry.Label)`（他にToolTipText参照があればコンパイルエラーを正として同様にLabelへ）。

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（DtoFactoryの `Tooltip`/`Split` 参照が落ちる場合はTask 6を先行着手せず、ここでは一時的に `Label = entry.Label; Tooltip = entry.Label` の暫定つなぎにせず、Task 6と同一コミットにまとめてよい）

- [ ] **Step 5: Commit**（Task 6と同時コミット可）

---

### Task 6: DTO/Factory/Topic 改修 + WireContractTest/fixture 更新

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuDtos.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs:166-180`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/build_menu_snapshot.json`

**Interfaces:**
- Consumes: Task 5の `BuildMenuEntry`、`MasterHolder.BlockCategoryMaster.BlockCategories.Data`
- Produces: wire契約（webui側zodと同形。Task 7が消費）:
  - payload: `{ categories: {name, subCategories: string[]}[], entries: BuildMenuEntryDto[] }`
  - entry: `{ entryType, entryKey, label, category, subCategory, requiredItems: {itemId:int, count:int}[], iconUrl? }`（tooltip削除）

- [ ] **Step 1: BuildMenuDtos.cs を改訂**（既存DTOのプロパティ宣言様式・シリアライズ属性を踏襲）

```csharp
    public class BuildMenuTopicDto
    {
        public List<BuildMenuCategoryDto> Categories { get; set; }
        public List<BuildMenuEntryDto> Entries { get; set; }
    }

    public class BuildMenuCategoryDto
    {
        public string Name { get; set; }
        public List<string> SubCategories { get; set; }
    }

    public class BuildMenuEntryDto
    {
        public string EntryType { get; set; }
        public string EntryKey { get; set; }
        public string Label { get; set; }
        public string Category { get; set; }
        public string SubCategory { get; set; }
        public List<BuildMenuRequiredItemDto> RequiredItems { get; set; }
        public string IconUrl { get; set; }   // null時キー省略の既存挙動を維持
    }

    public class BuildMenuRequiredItemDto
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
    }
```

- [ ] **Step 2: BuildMenuEntryDtoFactory.cs を改訂**

- `Label = entry.ToolTipText.Split('\n')[0]` / `Tooltip = entry.ToolTipText` を廃止し、`Label = entry.Label; Category = entry.Category; SubCategory = entry.SubCategory; RequiredItems = entry.RequiredItems.Select(r => new BuildMenuRequiredItemDto { ItemId = r.ItemId.AsPrimitive(), Count = r.Count }).ToList()` へ
- categories定義リストの変換と、エントリのカテゴリ未定義ガードを追加:

```csharp
        public static List<BuildMenuCategoryDto> CreateCategoryDtos()
        {
            // blockCategoriesマスタの配列順そのままが表示順の正
            // The array order of the blockCategories master is the source of truth for display order
            return MasterHolder.BlockCategoryMaster.BlockCategories.Data
                .Select(c => new BuildMenuCategoryDto
                {
                    Name = c.Name,
                    SubCategories = c.SubCategories.Select(s => s.Name).ToList(),
                }).ToList();
        }
```

`CreateDtos` 内、各エントリ変換時に種別導出定数のタイポ・定義ズレを大声で検出:

```csharp
            // 種別導出カテゴリがblockCategories定義に無い場合は設定不備として即座に失敗させる
            // Fail loudly when a derived category pair is missing from the blockCategories definition
            if (!MasterHolder.BlockCategoryMaster.Contains(entry.Category, entry.SubCategory))
                throw new InvalidOperationException($"BuildMenu entry has undefined category pair. label:{entry.Label} category:{entry.Category} subCategory:{entry.SubCategory}");
```

- [ ] **Step 3: BuildMenuTopic.cs の BuildJson でpayloadへ `Categories = BuildMenuEntryDtoFactory.CreateCategoryDtos()` を含める**

- [ ] **Step 4: build_menu_snapshot.json を新契約へ書き換え**

```json
{
    "categories": [
        { "name": "物流", "subCategories": ["チェスト"] },
        { "name": "輸送", "subCategories": ["鉄道", "車両"] },
        { "name": "ツール", "subCategories": ["接続", "ブループリント"] },
        { "name": "ブループリント", "subCategories": ["保存済み"] }
    ],
    "entries": [
        { "entryType": "block", "entryKey": "1", "label": "鉄の機械", "category": "物流", "subCategory": "チェスト", "requiredItems": [{ "itemId": 3, "count": 5 }], "iconUrl": "/api/block-icons/1.png" },
        { "entryType": "trainCar", "entryKey": "8f9c2a51-0000-0000-0000-000000000001", "label": "貨物車両", "category": "輸送", "subCategory": "車両", "requiredItems": [{ "itemId": 7, "count": 2 }], "iconUrl": "/api/train-car-icons/8f9c2a51-0000-0000-0000-000000000001.png" },
        { "entryType": "connectTool", "entryKey": "BeltConveyor", "label": "接続ツール", "category": "ツール", "subCategory": "接続", "requiredItems": [], "iconUrl": "/api/connect-tool-icons/3.png" },
        { "entryType": "blueprintCopy", "entryKey": "", "label": "ブループリントコピー", "category": "ツール", "subCategory": "ブループリント", "requiredItems": [] },
        { "entryType": "blueprint", "entryKey": "starter-base", "label": "starter-base", "category": "ブループリント", "subCategory": "保存済み", "requiredItems": [] }
    ]
}
```

（entryKey/iconUrl/labelの合成値はシリアライズ形状検証用。既存fixtureの値運用を踏襲し、既存値と整合する範囲で維持する）

- [ ] **Step 5: WireContractTest.cs の BuildMenuMatchesFixture を新DTO手組みへ更新**（fixtureと同値のDTOを構築し `JToken.DeepEquals` する既存方式のまま、Categories/Category/SubCategory/RequiredItemsを追加・Tooltipを削除）

- [ ] **Step 6: コンパイル + テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContractTest"`
Expected: 全PASS

- [ ] **Step 7: Commit（Task 5の変更と合わせて）**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/ moorestech_client/Assets/Scripts/Client.WebUiHost/Game/ moorestech_client/Assets/Scripts/Client.Tests/WebUi/
git commit -m "feat(webui): 建設メニュー契約へカテゴリと構造化コストを追加しtooltipを廃止"
```

---

### Task 7: webui契約更新（zod / payloadTypes / validators.test / wireContract.test）

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts`
- Modify: `moorestech_web/webui/src/bridge/contract/payloadTypes.ts:122-124`
- Modify: `moorestech_web/webui/src/bridge/contract/validators.test.ts:174-187`
- Modify: `moorestech_web/webui/src/bridge/contract/wireContract.test.ts:85-91`

**Interfaces:**
- Consumes: Task 6のwire契約・共有fixture
- Produces: `BuildMenuCategory` / `BuildMenuRequiredItem` / `BuildMenuEntryData` / `BuildMenuData` 型（Task 9-10が消費）

- [ ] **Step 1: buildMenu.ts を改訂**

```ts
import { z } from "zod";

export const BuildMenuEntryTypeSchema = z.enum(["block", "trainCar", "connectTool", "blueprintCopy", "blueprint"]);

export const BuildMenuRequiredItemSchema = z.object({
  itemId: z.number().int(),
  count: z.number().int(),
});

export const BuildMenuEntryDataSchema = z.object({
  entryType: BuildMenuEntryTypeSchema,
  entryKey: z.string(),
  label: z.string(),
  category: z.string(),
  subCategory: z.string(),
  requiredItems: z.array(BuildMenuRequiredItemSchema),
  iconUrl: z.string().optional(),
});

export const BuildMenuCategorySchema = z.object({
  name: z.string(),
  subCategories: z.array(z.string()),
});

export const BuildMenuDataSchema = z.object({
  categories: z.array(BuildMenuCategorySchema),
  entries: z.array(BuildMenuEntryDataSchema),
});
```

- [ ] **Step 2: payloadTypes.ts の z.infer 再エクスポートへ `BuildMenuCategory` / `BuildMenuRequiredItem` を追加**

- [ ] **Step 3: validators.test.ts のbuildMenu節を更新**（accept: カテゴリ+構造化コスト付きエントリ / reject: category欠落・requiredItems非配列・tooltip残存payloadは受理してもよい=unknown key方針は既存zodのstrict設定に従う。既存のaccept/rejectケース構成を踏襲して書き換え）

- [ ] **Step 4: wireContract.test.ts の期待値を更新**（`entries[0].category === "物流"`、`categories[0].name === "物流"`、`entries[3].iconUrl` undefined 等）

- [ ] **Step 5: テスト実行**

Run: `cd moorestech_web/webui && npm run test`
Expected: validators / wireContract 全PASS

- [ ] **Step 6: Commit**

```bash
git add moorestech_web/webui/src/bridge/
git commit -m "feat(webui): buildMenu zod契約へカテゴリと構造化コストを追加"
```

---

### Task 8: webui-design 様式追記 + ModeSwitch disabled prop（実装より先）

**Files:**
- Modify: `.claude/skills/webui-design/SKILL.md`
- Modify: `moorestech_web/webui/src/shared/ui/ModeSwitch/index.tsx` + 同ディレクトリのcss module
- Modify: `moorestech_web/webui/src/app/index.css`（建設メニュー用固定長トークン追加）

**Interfaces:**
- Produces: `ModeSwitch` に汎用 `disabled?: boolean` prop（root へ `data-disabled`、全ボタン `disabled` 属性、`--text-muted` 系減衰表現）。トークン `--build-menu-sidebar-width: 8.5rem` / `--build-menu-preview-height: calc(var(--slot-size) + 8px)` / `--build-menu-edge-safe-area: 16px`

- [ ] **Step 1: SKILL.md へ様式追記**（spec §4.4の5点。§8.8ワールドピンHUDの後に採番）
  1. §8.6 ModeSwitch節へ「縦利用（orientation="vertical"）によるサイドバーナビ」を成文化
  2. §8.6 ModeSwitch節へ「disabled?: boolean + data-disabled（--text-muted系減衰・クリック不可）」を追加
  3. 新§8.9「検索入力」: 素の`<input>` + `--gauge-track`同族の半透明面 + `--text-muted`プレースホルダ + focus-visibleはModeSwitch前例踏襲 + 寸法固定長トークン。Mantine TextInput不使用
  4. 新§8.10「カスタムスクロールバー」: Mantine ScrollAreaの`:global`上書き（前例 `ItemListPanel.module.css:10-30`）をトラック`var(--gauge-track)`・ノブ`var(--bevel-c2)`のネイビートーンで様式化
  5. 新§8.11「建設メニュー」: 大型2列占有（`viewer-start/items-end`・`.panelLarge`前例）+ 縦ModeSwitchサイドバー + 検索 + 固定高プレビュー（§8.7様式）+ サブカテゴリ見出し（`--text-muted`ラベル+FadeRule）グリッドの構成・使用トークンをホワイトリスト化

- [ ] **Step 2: ModeSwitch へ disabled prop 実装**（ドメイン語彙なしの汎用属性。判断は利用側が行いboolを渡すだけ）

```tsx
type ModeSwitchProps = {
  value: string;
  options: ModeSwitchOption[];
  onChange: (value: string) => void;
  orientation?: "horizontal" | "vertical";
  disabled?: boolean;
  testId?: string;
};
// root: data-disabled={disabled || undefined} を付与、各buttonに disabled={disabled}
```

css module:

```css
.root[data-disabled] .option {
  color: var(--text-muted);
  pointer-events: none;
}
```

- [ ] **Step 3: index.css へ建設メニュー用トークン追加**（`--block-panel-right-safe-area` 群の隣に配置）

```css
  /* 建設メニュー: サイドバー幅・プレビュー高・フェード帯と内容の安全余白（固定長。%は破綻源） */
  --build-menu-sidebar-width: 8.5rem;
  --build-menu-preview-height: calc(var(--slot-size) + 8px);
  --build-menu-edge-safe-area: 16px;
```

- [ ] **Step 4: lint + 既存テスト**

Run: `cd moorestech_web/webui && npm run lint && npm run test`
Expected: PASS（ModeSwitch既存利用箇所に影響なし）

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/webui-design/SKILL.md moorestech_web/webui/src/shared/ui/ModeSwitch/ moorestech_web/webui/src/app/index.css
git commit -m "feat(webui): 建設メニュー様式をwebui-designへ追記しModeSwitch disabledを追加"
```

---

### Task 9: buildMenuGrouping 純関数 + vitestテスト（TDD）

**Files:**
- Create: `moorestech_web/webui/src/features/buildMenu/buildMenuGrouping.ts`
- Test: `moorestech_web/webui/src/features/buildMenu/buildMenuGrouping.test.ts`

**Interfaces:**
- Consumes: Task 7の `BuildMenuCategory` / `BuildMenuEntryData`
- Produces:
  - `type BuildMenuSection = { category: string; subCategory: string; entries: BuildMenuEntryData[] }`
  - `visibleCategories(categories, entries): BuildMenuCategory[]` — エントリ1件以上のカテゴリのみ定義順
  - `resolveSelectedCategory(selected: string | null, visible: BuildMenuCategory[]): string | null` — null/消滅時は先頭へフォールバック
  - `sectionsForCategory(categoryName: string, categories, entries): BuildMenuSection[]` — サブカテゴリ定義順・空サブカテゴリ除外・エントリは配列順維持
  - `searchSections(query: string, categories, entries): BuildMenuSection[]` — label部分一致（大文字小文字無視）・カテゴリ定義順→サブカテゴリ定義順

- [ ] **Step 1: 失敗テストを書く**

```ts
import { describe, expect, it } from "vitest";
import type { BuildMenuCategory, BuildMenuEntryData } from "../../bridge/contract/payloadTypes";
import { resolveSelectedCategory, searchSections, sectionsForCategory, visibleCategories } from "./buildMenuGrouping";

const entry = (label: string, category: string, subCategory: string): BuildMenuEntryData => ({
  entryType: "block", entryKey: label, label, category, subCategory, requiredItems: [],
});

const categories: BuildMenuCategory[] = [
  { name: "採掘", subCategories: ["採掘機", "液体採取"] },
  { name: "物流", subCategories: ["チェスト", "電気コンベア"] },
  { name: "建材", subCategories: ["土台"] },
];

const entries = [
  entry("木のチェスト", "物流", "チェスト"),
  entry("鉄の採掘機", "採掘", "採掘機"),
  entry("ベルトコンベア", "物流", "電気コンベア"),
];

describe("visibleCategories", () => {
  it("エントリの無いカテゴリを除外し定義順を維持する", () => {
    expect(visibleCategories(categories, entries).map((c) => c.name)).toEqual(["採掘", "物流"]);
  });
});

describe("resolveSelectedCategory", () => {
  it("nullなら先頭カテゴリへフォールバックする", () => {
    expect(resolveSelectedCategory(null, visibleCategories(categories, entries))).toBe("採掘");
  });
  it("表示対象外のカテゴリ名なら先頭へフォールバックする", () => {
    expect(resolveSelectedCategory("建材", visibleCategories(categories, entries))).toBe("採掘");
  });
  it("表示中のカテゴリ名は維持する", () => {
    expect(resolveSelectedCategory("物流", visibleCategories(categories, entries))).toBe("物流");
  });
  it("表示カテゴリが無ければnull", () => {
    expect(resolveSelectedCategory("物流", [])).toBeNull();
  });
});

describe("sectionsForCategory", () => {
  it("サブカテゴリ定義順で空サブカテゴリを除外する", () => {
    const sections = sectionsForCategory("物流", categories, entries);
    expect(sections.map((s) => s.subCategory)).toEqual(["チェスト", "電気コンベア"]);
    expect(sections[0].entries.map((e) => e.label)).toEqual(["木のチェスト"]);
  });
});

describe("searchSections", () => {
  it("横断部分一致でカテゴリ定義順にグループ化する", () => {
    const sections = searchSections("鉄", categories, entries);
    expect(sections.map((s) => `${s.category}/${s.subCategory}`)).toEqual(["採掘/採掘機"]);
  });
  it("大文字小文字を無視する", () => {
    const en = [entry("Iron Chest", "物流", "チェスト")];
    expect(searchSections("iron", categories, en)).toHaveLength(1);
  });
  it("0件なら空配列", () => {
    expect(searchSections("存在しない", categories, entries)).toEqual([]);
  });
});
```

- [ ] **Step 2: 実行して失敗確認** — Run: `cd moorestech_web/webui && npx vitest run src/features/buildMenu/buildMenuGrouping.test.ts` → FAIL（module not found）

- [ ] **Step 3: buildMenuGrouping.ts を実装**

```ts
import type { BuildMenuCategory, BuildMenuEntryData } from "../../bridge/contract/payloadTypes";

export type BuildMenuSection = {
  category: string;
  subCategory: string;
  entries: BuildMenuEntryData[];
};

// エントリが1件以上あるカテゴリのみを定義順で返す（unlock進行で自然に増える）
export function visibleCategories(categories: BuildMenuCategory[], entries: BuildMenuEntryData[]): BuildMenuCategory[] {
  return categories.filter((category) => entries.some((e) => e.category === category.name));
}

// 選択カテゴリ名の解決。null・表示対象外なら表示中の先頭へフォールバック
export function resolveSelectedCategory(selected: string | null, visible: BuildMenuCategory[]): string | null {
  if (visible.length === 0) return null;
  if (selected !== null && visible.some((c) => c.name === selected)) return selected;
  return visible[0].name;
}

// カテゴリ内をサブカテゴリ定義順でグループ化。エントリ並びは配信配列順（=sortPriority昇順）を維持
export function sectionsForCategory(categoryName: string, categories: BuildMenuCategory[], entries: BuildMenuEntryData[]): BuildMenuSection[] {
  const definition = categories.find((c) => c.name === categoryName);
  if (!definition) return [];
  return definition.subCategories
    .map((subCategory) => ({
      category: categoryName,
      subCategory,
      entries: entries.filter((e) => e.category === categoryName && e.subCategory === subCategory),
    }))
    .filter((section) => section.entries.length > 0);
}

// 全カテゴリ横断のlabel部分一致検索（大文字小文字無視）。カテゴリ定義順→サブカテゴリ定義順
export function searchSections(query: string, categories: BuildMenuCategory[], entries: BuildMenuEntryData[]): BuildMenuSection[] {
  const lowered = query.toLowerCase();
  const hits = entries.filter((e) => e.label.toLowerCase().includes(lowered));
  return categories.flatMap((c) => sectionsForCategory(c.name, categories, hits));
}
```

- [ ] **Step 4: テスト実行** — Run: 同上 → 全PASS

- [ ] **Step 5: Commit**

```bash
git add moorestech_web/webui/src/features/buildMenu/buildMenuGrouping.ts moorestech_web/webui/src/features/buildMenu/buildMenuGrouping.test.ts
git commit -m "feat(webui): 建設メニューのカテゴリ導出・検索純関数を追加"
```

---

### Task 10: webui コンポーネント実装（様式準拠の全面書き換え）

**Files:**
- Rewrite: `moorestech_web/webui/src/features/buildMenu/BuildMenuPanel.tsx`（レイアウトと状態の束ね）
- Create: `moorestech_web/webui/src/features/buildMenu/CategorySidebar.tsx`
- Create: `moorestech_web/webui/src/features/buildMenu/BuildMenuSearchInput.tsx`
- Create: `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailPreview.tsx`
- Create: `moorestech_web/webui/src/features/buildMenu/BuildMenuCategoryGrid.tsx`
- Rewrite: `moorestech_web/webui/src/features/buildMenu/BuildMenuSlot.tsx`（SlotFrameベース）
- Rewrite: `moorestech_web/webui/src/features/buildMenu/style.module.css`

**Interfaces:**
- Consumes: Task 7契約型・Task 8の `ModeSwitch disabled`/トークン・Task 9の純関数・`shared/ui` の `GamePanel/SlotFrame/PanelCloseButton/FadeRule/ItemSlot/SlotGrid`・`useSlotMouse`・Mantine `ScrollArea`
- Produces: 動作するカテゴリ化ビルドメニュー。e2e契約: testid `build-menu-entry-${entryType}-${entryKey}` 維持・`build_menu.select`/`blueprint.delete`/`ui_state.request` アクション契約不変。新testid: `build-menu-sidebar`（ModeSwitch testId）・`build-menu-search`・`build-menu-preview`・`build-menu-section-${category}-${subCategory}`

実装要点（各ファイル200行以下・importパス/バレル利用は現行BuildMenuPanel.tsxの書式を踏襲）:

- [ ] **Step 1: BuildMenuSlot.tsx をSlotFrameベースへ書き換え**

```tsx
import { SlotFrame } from "../../shared/ui";
import type { BuildMenuEntryData } from "../../bridge/contract/payloadTypes";
import { tutorialAnchor } from /* 現行BuildMenuSlot.tsxと同じimport元 */;
import styles from "./style.module.css";

type Props = {
  entry: BuildMenuEntryData;
  onLeftClick: () => void;
  onRightClick?: () => void;
  onHoverChange: (hovering: boolean) => void;
};

export function BuildMenuSlot({ entry, onLeftClick, onRightClick, onHoverChange }: Props) {
  return (
    <SlotFrame
      filled
      testId={`build-menu-entry-${entry.entryType}-${entry.entryKey}`}
      onLeftDown={onLeftClick}
      onRightDown={onRightClick}
      onHoverChange={onHoverChange}
      {...tutorialAnchor(/* 現行と同一のアンカーキー生成を維持 */)}
    >
      {entry.iconUrl ? (
        <img className={styles.slotIcon} src={entry.iconUrl} alt={entry.label} draggable={false} />
      ) : (
        <span className={styles.slotLabel}>{entry.label}</span>
      )}
    </SlotFrame>
  );
}
```

（64px直書き・ハードコード色・生onClick・Mantine Tooltipを全廃。SlotFrameのonLeftDownは `(shift: boolean) => void` 形なら合わせる）

- [ ] **Step 2: BuildMenuSearchInput.tsx**（§8.9様式。素input+トークンのみ）

```tsx
import { useI18n } from /* i18nStoreの現行import書式 */;
import styles from "./style.module.css";

type Props = { value: string; onChange: (value: string) => void };

export function BuildMenuSearchInput({ value, onChange }: Props) {
  const { t } = useI18n();
  return (
    <input
      className={styles.searchInput}
      type="text"
      value={value}
      placeholder={t("検索")}
      onChange={(e) => onChange(e.currentTarget.value)}
      data-testid="build-menu-search"
    />
  );
}
```

- [ ] **Step 3: BuildMenuDetailPreview.tsx**（§8.7の固定高様式。ホバー中エントリ→無ければ案内。2段フォールバック無し）

```tsx
import { FadeRule, ItemSlot } from "../../shared/ui";
import { useI18n } from /* 現行書式 */;
import type { BuildMenuEntryData } from "../../bridge/contract/payloadTypes";
import styles from "./style.module.css";

type Props = { entry: BuildMenuEntryData | null };

export function BuildMenuDetailPreview({ entry }: Props) {
  const { t } = useI18n();
  return (
    <div className={styles.preview} data-testid="build-menu-preview">
      <div className={styles.previewBody}>
        {entry === null ? (
          <span className={styles.previewHint}>{t("カーソルを合わせると詳細を表示します")}</span>
        ) : (
          <>
            {entry.iconUrl && <img className={styles.previewIcon} src={entry.iconUrl} alt={entry.label} draggable={false} />}
            <span className={styles.previewName}>{entry.label}</span>
            {entry.requiredItems.length > 0 && (
              <div className={styles.previewCost}>
                {entry.requiredItems.map((item) => (
                  <ItemSlot key={item.itemId} itemId={item.itemId} count={item.count} />
                ))}
              </div>
            )}
          </>
        )}
      </div>
      <FadeRule />
    </div>
  );
}
```

- [ ] **Step 4: BuildMenuCategoryGrid.tsx**（サブカテゴリ見出し+SlotGrid。検索中は「カテゴリ / サブカテゴリ」複合見出し）

```tsx
import { SlotGrid } from "../../shared/ui"; // SlotGridのimport元は現行BuildMenuPanel.tsxを踏襲
import type { BuildMenuEntryData } from "../../bridge/contract/payloadTypes";
import type { BuildMenuSection } from "./buildMenuGrouping";
import { BuildMenuSlot } from "./BuildMenuSlot";
import styles from "./style.module.css";

type Props = {
  sections: BuildMenuSection[];
  compositeHeading: boolean; // 検索中はカテゴリ名/サブカテゴリ名の複合見出し
  onSelect: (entry: BuildMenuEntryData) => void;
  onDelete: (entry: BuildMenuEntryData) => void;
  onHoverChange: (entry: BuildMenuEntryData | null) => void;
};

export function BuildMenuCategoryGrid({ sections, compositeHeading, onSelect, onDelete, onHoverChange }: Props) {
  return (
    <div className={styles.gridArea}>
      {sections.map((section) => (
        <section key={`${section.category}/${section.subCategory}`} data-testid={`build-menu-section-${section.category}-${section.subCategory}`}>
          <h3 className={styles.sectionHeading}>
            {compositeHeading ? `${section.category} / ${section.subCategory}` : section.subCategory}
          </h3>
          <SlotGrid cols={8}>
            {section.entries.map((entry) => (
              <BuildMenuSlot
                key={`${entry.entryType}-${entry.entryKey}`}
                entry={entry}
                onLeftClick={() => onSelect(entry)}
                onRightClick={entry.entryType === "blueprint" ? () => onDelete(entry) : undefined}
                onHoverChange={(hovering) => onHoverChange(hovering ? entry : null)}
              />
            ))}
          </SlotGrid>
        </section>
      ))}
    </div>
  );
}
```

（見出しテキストはマスタ由来文字列のためt()不要=既存entry.labelと同扱い。検索0件時の「該当なし」表示はPanel側）

- [ ] **Step 5: CategorySidebar.tsx**（縦ModeSwitch）

```tsx
import { ModeSwitch } from "../../shared/ui";
import type { BuildMenuCategory } from "../../bridge/contract/payloadTypes";

type Props = {
  categories: BuildMenuCategory[];
  selected: string;
  disabled: boolean; // 検索中はサイドバー無効
  onSelect: (name: string) => void;
};

export function CategorySidebar({ categories, selected, disabled, onSelect }: Props) {
  return (
    <ModeSwitch
      value={selected}
      options={categories.map((c) => ({ value: c.name, label: c.name, testId: `build-menu-category-${c.name}` }))}
      onChange={onSelect}
      orientation="vertical"
      disabled={disabled}
      testId="build-menu-sidebar"
    />
  );
}
```

- [ ] **Step 6: BuildMenuPanel.tsx を全面書き換え**（GamePanel化・状態束ね）

```tsx
import { useState } from "react";
import { ScrollArea } from "@mantine/core";
import { GamePanel, PanelCloseButton } from "../../shared/ui";
import { dispatchAction, useTopic, Topics, UiStateNames } from /* 現行書式 */;
import { useI18n } from /* 現行書式 */;
import type { BuildMenuEntryData } from "../../bridge/contract/payloadTypes";
import { resolveSelectedCategory, searchSections, sectionsForCategory, visibleCategories } from "./buildMenuGrouping";
import { BuildMenuCategoryGrid } from "./BuildMenuCategoryGrid";
import { BuildMenuDetailPreview } from "./BuildMenuDetailPreview";
import { BuildMenuSearchInput } from "./BuildMenuSearchInput";
import { CategorySidebar } from "./CategorySidebar";
import styles from "./style.module.css";

export function BuildMenuPanel() {
  const { t } = useI18n();
  const data = useTopic(Topics.buildMenu);
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [hovered, setHovered] = useState<BuildMenuEntryData | null>(null);
  if (!data) return null;

  // 検索中は全カテゴリ横断、通常時は選択カテゴリ（消滅時は先頭へフォールバック）
  const visible = visibleCategories(data.categories, data.entries);
  const searching = query !== "";
  const currentCategory = resolveSelectedCategory(selectedCategory, visible);
  const sections = searching
    ? searchSections(query, data.categories, data.entries)
    : currentCategory !== null
      ? sectionsForCategory(currentCategory, data.categories, data.entries)
      : [];

  const select = (entry: BuildMenuEntryData) =>
    void dispatchAction("build_menu.select", { entryType: entry.entryType, entryKey: entry.entryKey });
  const remove = (entry: BuildMenuEntryData) => void dispatchAction("blueprint.delete", { name: entry.entryKey });
  const close = () => void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });

  return (
    <div className={styles.panel} data-testid="build-menu-panel">
      <GamePanel title={t("ビルドメニュー")} variant="default">
        <PanelCloseButton onClick={close} ariaLabel={t("閉じる")} className={styles.close} testId="build-menu-close" />
        <div className={styles.columns}>
          <CategorySidebar
            categories={visible}
            selected={currentCategory ?? ""}
            disabled={searching}
            onSelect={setSelectedCategory}
          />
          <div className={styles.main}>
            <BuildMenuSearchInput value={query} onChange={setQuery} />
            <BuildMenuDetailPreview entry={hovered} />
            <ScrollArea className={styles.scroll} type="auto">
              {sections.length === 0 && searching ? (
                <span className={styles.noHit}>{t("該当なし")}</span>
              ) : (
                <BuildMenuCategoryGrid
                  sections={sections}
                  compositeHeading={searching}
                  onSelect={select}
                  onDelete={remove}
                  onHoverChange={setHovered}
                />
              )}
            </ScrollArea>
          </div>
        </div>
      </GamePanel>
    </div>
  );
}
```

（closeボタン配置は `blockInventory/style.module.css` の `.close`（`position:absolute; top:var(--bevel-3); right:var(--panel-edge-fade)`）前例に合わせる。パネル開閉によるunmountで検索/選択リセットは意図仕様）

**マウント位置の注意**: 現行BuildMenuPanelは `position: fixed` のためstageグリッド外にマウントされている可能性がある。`grid-column: viewer-start / items-end` を効かせるには、App側のレンダリング位置を研究パネル（`features/research`）と同じstageグリッド内へ移す。App.tsxのマウント箇所を確認し、研究パネルの条件付き描画と同じ場所・同じ方式に合わせること。

- [ ] **Step 7: style.module.css を全面書き換え**（%指定・ハードコード色ゼロ。ScrollAreaスクロールバーは§8.10様式）

```css
/* 大型2列占有はblockInventory .panelLarge前例。上端=持ち物と揃い、下端=ホットバー手前 */
.panel {
  grid-column: viewer-start / items-end;
  grid-row: 1;
  width: 759.3px;
  height: 525px;
  min-width: 0;
  position: relative;
  z-index: var(--z-screen);
}

.close {
  position: absolute;
  top: var(--bevel-3);
  right: var(--panel-edge-fade);
  z-index: var(--z-overlay-panel);
}

.columns {
  display: grid;
  grid-template-columns: var(--build-menu-sidebar-width) 1fr;
  gap: var(--build-menu-edge-safe-area);
  height: 100%;
  padding-right: var(--build-menu-edge-safe-area);
  padding-bottom: var(--block-panel-bottom-safe-area);
}

.main {
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-height: 0;
}

.searchInput {
  background: var(--gauge-track);
  border: var(--bevel-1) solid var(--bevel-c1);
  color: var(--text-default);
  padding: var(--mode-switch-padding-block) var(--mode-switch-padding-inline);
}

.searchInput::placeholder {
  color: var(--text-muted);
}

.preview {
  min-height: var(--build-menu-preview-height);
}

.previewBody {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: var(--build-menu-preview-height);
}

.previewHint,
.noHit,
.sectionHeading {
  color: var(--text-muted);
}

.sectionHeading {
  font-size: 12px;
  font-weight: 400;
}

.previewIcon,
.slotIcon {
  width: calc(var(--slot-size) - 8px);
  height: calc(var(--slot-size) - 8px);
  object-fit: contain;
}

.slotLabel {
  font-size: 10px;
  color: var(--text-default);
  word-break: break-all;
}

.scroll {
  flex: 1;
  min-height: 0;
}

/* §8.10 カスタムスクロールバー（ItemListPanel前例のネイビートーン版） */
.scroll :global(.mantine-ScrollArea-scrollbar) {
  background: var(--gauge-track);
}

.scroll :global(.mantine-ScrollArea-thumb) {
  background: var(--bevel-c2);
  border-radius: 0;
}
```

（実装時、GamePanelのpadding実態・フェード帯との干渉を目視で確認し、不足辺は安全帯トークンで補正。`FadeRule` はプレビュー下端=Preview内に含めた）

- [ ] **Step 8: lint + unitテスト + tsc**

Run: `cd moorestech_web/webui && npm run lint && npm run test && npm run build`
Expected: 全PASS（no-jsx-visible-literal違反ゼロ）

- [ ] **Step 9: Commit**

```bash
git add moorestech_web/webui/src/features/buildMenu/
git commit -m "feat(webui): 建設メニューをカテゴリ化し様式準拠へ全面移行"
```

---

### Task 11: e2e fixtures 拡充 + regression spec 再構成

**Files:**
- Modify: `moorestech_web/webui/e2e/mock-host/fixtures.ts:133-139`
- Modify: `moorestech_web/webui/e2e/tests/regression/buildMenu.spec.ts`

**Interfaces:**
- Consumes: Task 10のtestid群（`build-menu-entry-*` / `build-menu-sidebar` / `build-menu-category-*` / `build-menu-search` / `build-menu-preview` / `build-menu-section-*`）

- [ ] **Step 1: fixtures.ts のbuildMenuを拡充**（同一カテゴリ複数サブカテゴリ + 複数カテゴリ跨ぎ検索ヒット構成）

```ts
export const buildMenu = {
  categories: [
    { name: "物流", subCategories: ["チェスト", "電気コンベア"] },
    { name: "輸送", subCategories: ["鉄道", "車両"] },
    { name: "ブループリント", subCategories: ["保存済み"] },
  ],
  entries: [
    { entryType: "block", entryKey: "wood-chest", label: "木のチェスト", category: "物流", subCategory: "チェスト", requiredItems: [{ itemId: 1, count: 4 }], iconUrl: "/icons/wood-chest.png" },
    { entryType: "block", entryKey: "iron-chest", label: "鉄のチェスト", category: "物流", subCategory: "チェスト", requiredItems: [], iconUrl: "/icons/iron-chest.png" },
    { entryType: "block", entryKey: "belt-conveyor", label: "ベルトコンベア", category: "物流", subCategory: "電気コンベア", requiredItems: [], iconUrl: "/icons/belt-conveyor.png" },
    { entryType: "block", entryKey: "rail", label: "鉄道レール", category: "輸送", subCategory: "鉄道", requiredItems: [], iconUrl: "/icons/rail.png" },
    { entryType: "trainCar", entryKey: "cargo-car", label: "貨物車両", category: "輸送", subCategory: "車両", requiredItems: [], iconUrl: "/icons/cargo-car.png" },
    { entryType: "blueprint", entryKey: "starter-base", label: "starter-base", category: "ブループリント", subCategory: "保存済み", requiredItems: [] },
  ],
} satisfies BuildMenuData;
```

（「鉄」検索で 物流/チェスト の鉄のチェスト と 輸送/鉄道 の鉄道レール が複数カテゴリ跨ぎでヒットする）

- [ ] **Step 2: buildMenu.spec.ts を再構成**（現行の「3エントリ同時可視」assertはカテゴリ分散で不成立。シナリオ: 開閉/カテゴリ切替選択/BP削除/横断検索/検索中サイドバー無効/検索0件/検索クリア復元/ホバープレビュー/空カテゴリ非表示）

```ts
test("カテゴリ切替でセクションが入れ替わる", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await expect(page.getByTestId("build-menu-section-物流-チェスト")).toBeVisible();
  await expect(page.getByTestId("build-menu-entry-block-rail")).toBeHidden();
  await page.getByTestId("build-menu-category-輸送").click();
  await expect(page.getByTestId("build-menu-entry-block-rail")).toBeVisible();
  await expect(page.getByTestId("build-menu-entry-block-wood-chest")).toBeHidden();
});

test("エントリ選択とBP右クリック削除のアクション契約", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.getByTestId("build-menu-entry-block-wood-chest").click();
  expect(await payloadsOf(page, "build_menu.select")).toContainEqual({ entryType: "block", entryKey: "wood-chest" });
  await page.getByTestId("build-menu-category-ブループリント").click();
  await page.getByTestId("build-menu-entry-blueprint-starter-base").click({ button: "right" });
  expect(await payloadsOf(page, "blueprint.delete")).toContainEqual({ name: "starter-base" });
});

test("横断検索は複合見出しで区切りサイドバーを無効化する", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.getByTestId("build-menu-search").fill("鉄");
  await expect(page.getByTestId("build-menu-section-物流-チェスト")).toBeVisible();
  await expect(page.getByTestId("build-menu-section-輸送-鉄道")).toBeVisible();
  await expect(page.getByTestId("build-menu-sidebar")).toHaveAttribute("data-disabled", "true");
  await page.getByTestId("build-menu-search").fill("");
  await expect(page.getByTestId("build-menu-sidebar")).not.toHaveAttribute("data-disabled", "true");
  await expect(page.getByTestId("build-menu-section-物流-チェスト")).toBeVisible();
});

test("検索0件は該当なし表示", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.getByTestId("build-menu-search").fill("存在しないブロック");
  await expect(page.getByTestId("build-menu-panel")).toContainText("該当なし");
});

test("ホバーでプレビューが更新される", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  await page.getByTestId("build-menu-entry-block-wood-chest").hover();
  await expect(page.getByTestId("build-menu-preview")).toContainText("木のチェスト");
});

test("エントリの無いカテゴリはサイドバーに出ない", async ({ page }) => {
  await setUiState(page, "BuildMenu");
  // fixturesの全カテゴリにエントリがあるため、ここでは「定義順どおり3カテゴリのみ」を確認
  await expect(page.getByTestId("build-menu-sidebar").locator("button")).toHaveCount(3);
});
```

（`payloadsOf`/`setUiState` の実シグネチャは `e2e/support/mockControl.ts` の現行実装に合わせる。既存のclose/開閉テストは維持）

- [ ] **Step 3: e2e実行**

Run: `cd moorestech_web/webui && npm run test:e2e`
Expected: 全PASS

- [ ] **Step 4: Commit**

```bash
git add moorestech_web/webui/e2e/
git commit -m "test(webui): 建設メニューe2eをカテゴリ構成へ再構成"
```

---

### Task 12: 目視QA + 実マスタ検証

**Files:** なし（検証タスク。発見事項は該当タスクのファイルを修正）

- [ ] **Step 1: mock-hostで目視QA**（webui-design §10の手順どおりスクショ確認: 4辺の余白・フェード帯との干渉・見出し区別・中央揃え。`e2e/capture-eval.ts` を利用）
- [ ] **Step 2: クライアント全テスト実行**（テストmod JSON更新漏れの検出込み）

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "."`（時間がかかる場合はEditModeInPlayingTest系+サーバーCombinedTestを優先）
Expected: 全PASS。MooresmasterLoaderExceptionが出たらそのmodのJSONを修正

- [ ] **Step 3: 実マスタで起動し目視確認**（unity-playmode-recorded-playtestスキルのDSL、またはuloopでPlayMode起動。BuildMenuを開き、カテゴリ順=blockCategories定義順・坂バリアント非表示・車両が輸送カテゴリに合流・検索とプレビュー動作を確認。DebugParametersのcache残留=FreeBlockPlacement=trueに注意: 全表示になっていたらcacheを先に確認）
- [ ] **Step 4: 発見事項を修正し、最終コミット + 未コミット作業ゼロを確認**

```bash
git status  # 共有ワーキングツリーの他セッション変更を巻き込んでいないか必ず確認
```

---

## Self-Review 済み事項

- spec §1〜§6 の全要件にタスクを対応付け（§1.1-1.5→Task 1-4、§2→Task 6-7、§3→Task 5、§4→Task 8-10、§5エッジケース→Task 9のフォールバック純関数+Task 6のガード、§6検証計画→各タスクのテスト+Task 12）
- spec §6.6「新設バリデーションの失敗系テスト」→ Task 4 Step 1
- 型整合: `BuildMenuSection` / `BuildMenuCategory` / `resolveSelectedCategory` 等の名前はTask 9定義をTask 10-11が同名で消費
- 生成コード（Mooresmaster.Model/Loader）のシンボル名は実生成が正。プランの想定名と異なる場合はコンパイルエラーメッセージに合わせて修正し、プラン側の名前ズレを理由に設計を変えない
