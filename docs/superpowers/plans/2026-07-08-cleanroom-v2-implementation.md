# クリーンルームシステム v2 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 仕様書 `docs/superpowers/specs/2026-07-08-cleanroom-design-v2.md` のクリーンルームシステム（密閉検出・純度シミュレーション・境界ブロック・空気清浄機・専用機械・チップ抽選・セーブロード）をサーバー側に実装する。

**Architecture:** 部屋の検出と純度状態は新アセンブリ `Game.CleanRoom` の `CleanRoomDatastore`（DI singleton・`GameUpdater` 購読、`GearNetworkDatastore` と同型）が所有する。ブロック側は `Game.Block.Interface` のマーカー/受信インターフェースを実装したコンポーネントとして参加し、機械は部屋状態のプッシュを受けて Idle/Processing/Halted の3状態で動く。純度・抽選の計算核は純粋 static 関数に隔離してテストを固定する。

**Tech Stack:** C# (Unity, moorestech_server), UniRx (`GameUpdater.UpdateObservable`), NUnit (Server.Tests), Newtonsoft.Json, Mooresmaster SourceGenerator（cleanRoom.yml → CleanRoomModule）

**作業ディレクトリ:** `/Users/katsumi/moorestech-worktrees/tree2`（branch `feature/cleanroom-v2`）

## Global Constraints

- AGENTS.md 全規約に従う。特に: 1ファイル200行以下 / 1ディレクトリ10ファイル以下 / partial 絶対禁止 / try-catch 禁止 / デフォルト引数禁止 / イベントは UniRx（`Subject<T>` private 保持 + `IObservable<T>` 公開、C# `event Action` 禁止）/ 単純 getter/setter プロパティ禁止（Set は `SetHoge` メソッド）
- コメントは日英2行セット（各1行厳守）を主要処理セクションに約3〜10行ごと。自明コメント禁止
- **本計画書・仕様書への参照をコードコメントに書かない**（「Task 3参照」「§4の順序」等は禁止。コードだけで完結する不変条件の説明を書く）
- **テスト専用の public メンバー・フック・カウンタを本番コードに生やさない**（`XxxForTest` 禁止。テストは実挙動・実コンポーネントの公開状態で観測する。テスト用の設定注入はコンストラクタ引数で行う）
- **将来機能のための予約・seam・空フックを書かない**。実装した機能は必ず本番経路（実プレイ）から発火する
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client` を実行（tree2 のプロジェクトを開いている Unity に対して）。「Unity is reloading」エラーは45秒待ってリトライ
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
- スキーマ編集後は csc.rsp / `_CompileRequester.cs` の更新を忘れない（Task 1 参照）
- 各タスク完了ごとに `git add <対象> && git commit` する。コミットメッセージは `feat(cleanroom): <内容>`
- 数値は仕様書 §8 の初期パラメータを唯一のソースとする（本計画にも転記済み）

## 配置と前例（spec-architecture-review 済み）

| 項目 | 配置先 | 前例（引用） |
|---|---|---|
| `CleanRoomMaster`（生ロード・辞書化のみ） | Core.Master | `MasterHolder.cs` の既存 Master 群 |
| 純度判定・積分・抽選（マスタ値の解釈） | Game.CleanRoom / Game.Block（ドメイン層） | 層マップ「マスタ値のドメイン解釈はドメイン層」 |
| `CleanRoomDatastore`（実行時状態の所有＋tick） | Game.CleanRoom（新asmdef） | `Game.Gear/Common/GearNetworkDatastore.cs`（GameUpdater購読・DI singleton） |
| ブロック→データストアの登録経路 | `ServerContext.WorldBlockUpdateEvent` 購読（datastore側） | 部屋検出は**非境界ブロックも**対象のため、Gear式の自己登録（境界だけ）では足りない。新規パターンとして採用（能動介入なし・購読のみの受動統合） |
| コンポーネント契約（マーカー/受信/フィルタ/ハッチ） | Game.Block.Interface/Component/CleanRoom/ | `IElectricConsumer`（Game.EnergySystem）等の契約分離と同型 |
| ブロック実装・テンプレート | Game.Block/Blocks/CleanRoom/ + Factory/BlockTemplate/ | `VanillaMachineTemplate.cs`、`VanillaIBlockTemplates.cs` |
| 機械の Halted 状態 | `ProcessState` enum への値追加＋専用 `CleanRoomMachineProcessorComponent` | 状態ハンドラ前例 `Game.Block/Blocks/Machine/State/`。共有 `VanillaMachineProcessorComponent` は変更しない（既存全機械への影響を遮断）。受動統合案（Vanillaをそのまま使いゲートを外付け）は、開始判定・電力要求が状態機械の内側にあり外から介入する経路が存在しないため不成立 → 専用プロセッサで状態機械ごと所有する |
| セーブ | `WorldSaveAllInfoV1` にフィールド追加 | 同ファイル `inventorySlotLevel` / `railSegments` |
| 電力接続 | `IElectricConsumer` 実装 + `ElectricWireConnectorComponent` | `VanillaMachineTemplate.cs` の wireConnector 組み立て |
| マスタ欠損フォールバック | `MasterHolder` で cleanRoom キー欠損時に空マスタ生成 | 前例なし・新規パターン（仕様 §7「未定義Modでも起動」の要求由来） |

機能パリティ（死活表）: 本機能は greenfield。既存操作への影響は `ProcessState` enum への `Halted` 値追加のみで、既存機械が Halted を発することはない（クリーンルーム機械専用の遷移）。既存セーブは cleanRoom キー欠損として空状態でロードされる（Task 10 でテスト固定）。

## ファイル構成（新規）

```
moorestech_server/Assets/Scripts/
├── Core.Master/CleanRoomMaster.cs
├── Game.Block.Interface/Component/CleanRoom/
│   ├── ICleanRoomBoundaryComponent.cs   (CleanRoomBoundaryKind enum 同居)
│   ├── ICleanRoomAirFilter.cs
│   ├── ICleanRoomItemHatch.cs
│   ├── ICleanRoomMachine.cs             (稼働中判定 + 効果受信)
│   └── CleanRoomEffect.cs               (readonly struct)
├── Game.CleanRoom/                       (新asmdef。Game.Gear.asmdef の参照構成を雛形に、
│   │                                      Game.Block.Interface / Game.World.Interface / Game.Context / Core.Master / Core.Update / UniRx を参照)
│   ├── Game.CleanRoom.asmdef
│   ├── CleanRoom.cs                      (部屋モデル: Cells/V/S/N/行)
│   ├── CleanRoomCellSets.cs              (セル集合構築・リーク上限)
│   ├── CleanRoomDetector.cs              (flood-fill 検出エンジン)
│   ├── CleanRoomPurityLogic.cs           (積分・行判定ヒステリシス・按分の純関数)
│   ├── CleanRoomPollution.cs             (A_total 算出)
│   ├── CleanRoomDetectionService.cs      (dirtyキュー・予算・差分更新)
│   ├── CleanRoomCarryOver.cs             (重なり引き継ぎ)
│   ├── CleanRoomDatastore.cs             (ファサード: tick・登録簿・保存)
│   └── CleanRoomSaveData.cs
├── Game.Block/Blocks/CleanRoom/
│   ├── CleanRoomBoundaryComponent.cs
│   ├── CleanRoomItemHatchComponent.cs
│   ├── CleanRoomPipeHatchComponent.cs
│   ├── CleanRoomAirFilterComponent.cs
│   ├── CleanRoomMachineComponent.cs      (受信保持+稼働判定+電力Consumer)
│   ├── CleanRoomMachineProcessorComponent.cs
│   ├── HaltedMachineProcessState.cs
│   └── CleanRoomChipDraw.cs              (決定的抽選の純関数)
├── Game.Block/Factory/BlockTemplate/
│   ├── VanillaCleanRoomBoundaryTemplate.cs
│   ├── VanillaCleanRoomAirFilterTemplate.cs
│   └── VanillaCleanRoomMachineTemplate.cs
└── Tests/CombinedTest/Core/CleanRoom/    (テストは全てここ)
```

初期パラメータ（仕様 §8 転記・テスト期待値の根拠）:

- クラス行（良い順）: A(≤10, Lv4, 0%, ACH≥0.017) / B(≤50, Lv3, 5%, ≥0.0083) / C(≤200, Lv2, 15%, ≥0.0042) / D(≤1000, Lv1, 35%, ≥0.0014)。行に入らなければ Out
- 汚染係数: aVolume 0.10 / aSurface 0.05 / aConnector 0.50 / aMachine 2.0 / kHatch 0.30
- 清浄機: q=5.0、フィルター容量5000。昇格ヒステリシス: 濃度×0.8・ACH×1.25
- 検出: 部屋上限4096セル・予算8192セル/tick・ハッチ窓20tick・中継バッファ4
- 基準例: 5×5×3内寸(V=75,S=110)+接続点2+稼働機械1+清浄機1台満電 → A_total=16.0 → C_eq=3.2 → A成立

---

### Task 1: スキーマ追加（blocks.yml + cleanRoom.yml）と SourceGenerator

**Files:**
- Modify: `VanillaSchema/blocks.yml`（blockType switch に6ケース追加）
- Create: `VanillaSchema/cleanRoom.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/csc.rsp`（cleanRoom.yml の行を追加）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`（dummyText 変更）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`, `items.json`, `machineRecipes.json`
- Create: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/cleanRoom.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`, `ForUnitTestItemId.cs`（新ブロック/アイテムの定数追加。既存定数の宣言形式に合わせる）

**Interfaces (Produces):**
- 生成型: `Mooresmaster.Model.BlocksModule` に `CleanRoomWallBlockParam`（空）, `CleanRoomDoorBlockParam`（空）, `CleanRoomItemHatchBlockParam`（inventoryConnectors）, `CleanRoomPipeHatchBlockParam`（fluidInventoryConnectors）, `CleanRoomAirFilterBlockParam`, `CleanRoomMachineBlockParam`
- 生成型: `Mooresmaster.Model.CleanRoomModule.CleanRoomMasterElement`（thresholds / pollution / chipDraws）
- `BlockTypeConst.CleanRoomWall` 等6定数（SourceGenerator が blocks.yml の case から生成）
- テスト定数: `ForUnitTestModBlockId.CleanRoomWallId` / `CleanRoomDoorId` / `CleanRoomItemHatchId` / `CleanRoomPipeHatchId` / `CleanRoomAirFilterId` / `CleanRoomMachineId`

- [ ] **Step 1: blocks.yml に6ケースを追加**

`blockParam` の `switch: ./blockType` の cases 末尾に追加（既存 ElectricMachine ケースの書式に合わせる）:

```yaml
      - when: CleanRoomWall
        type: object
        properties: []
      - when: CleanRoomDoor
        type: object
        properties: []
      - when: CleanRoomItemHatch
        type: object
        implementationInterface:
        - IInventoryConnectors
        properties:
        - key: inventoryConnectors
          ref: inventoryConnects
      - when: CleanRoomPipeHatch
        type: object
        properties:
        - key: fluidInventoryConnectors
          ref: fluidInventoryConnects
      - when: CleanRoomAirFilter
        type: object
        properties:
        - key: requiredPower
          type: number
          default: 100
        - key: removalVolumePerSecond
          type: number
          default: 5
        - key: filterItemGuid
          type: uuid
          foreignKey:
            schemaId: items
            foreignKeyIdPath: /data/[*]/itemGuid
            displayElementPath: /data/[*]/name
        - key: filterCapacity
          type: number
          default: 5000
        - key: maxWireConnectionCount
          type: integer
          default: 2
        - key: maxWireLength
          type: number
          default: 8
      - when: CleanRoomMachine
        type: object
        implementationInterface:
        - IInventoryConnectors
        - IMachineParam
        properties:
        - key: requiredPower
          type: number
          default: 5
        - key: idlePowerRate
          type: number
          default: 0.2
        - key: inputSlotCount
          type: integer
          default: 1
        - key: outputSlotCount
          type: integer
          default: 1
        - key: inventoryConnectors
          ref: inventoryConnects
        - key: inputTankCount
          type: integer
          default: 0
        - key: outputTankCount
          type: integer
          default: 0
        - key: innerTankCapacity
          type: number
          default: 0
        - key: moduleSlotCount
          type: integer
          default: 0
        - key: fluidInventoryConnectors
          ref: fluidInventoryConnects
        - key: maxWireConnectionCount
          type: integer
          default: 2
        - key: maxWireLength
          type: number
          default: 8
```

注意: ElectricMachine ケースの実物を必ず開き、interface 名・ref 名（`inventoryConnects` / `fluidInventoryConnects`）・インデントを一致させること。

- [ ] **Step 2: cleanRoom.yml を新規作成**

```yaml
id: cleanRoom
schema:
  type: object
  properties:
  - key: pollution
    type: object
    properties:
    - key: aVolume
      type: number
      default: 0.1
    - key: aSurface
      type: number
      default: 0.05
    - key: aConnector
      type: number
      default: 0.5
    - key: aMachine
      type: number
      default: 2.0
    - key: kHatch
      type: number
      default: 0.3
  - key: thresholds
    type: array
    items:
      type: object
      properties:
      - key: className
        type: string
      - key: maxConcentration
        type: number
      - key: requiredAirChangeRate
        type: number
      - key: maxChipLevel
        type: integer
      - key: downBinRate
        type: number
  - key: chipDraws
    type: array
    items:
      type: object
      properties:
      - key: machineRecipeGuid
        type: uuid
        foreignKey:
          schemaId: machineRecipes
          foreignKeyIdPath: /data/[*]/machineRecipeGuid
          displayElementPath: /data/[*]/machineRecipeGuid
      - key: euvSuccessRate
        type: number
        default: 0.8
      - key: outputDistributions
        type: array
        items:
          type: object
          properties:
          - key: outputItemGuid
            type: uuid
            foreignKey:
              schemaId: items
              foreignKeyIdPath: /data/[*]/itemGuid
              displayElementPath: /data/[*]/name
          - key: levels
            type: array
            items:
              type: object
              properties:
              - key: level
                type: integer
              - key: weight
                type: number
              - key: chipItemGuid
                type: uuid
                foreignKey:
                  schemaId: items
                  foreignKeyIdPath: /data/[*]/itemGuid
                  displayElementPath: /data/[*]/name
```

注意: 既存メインスキーマ（`fluids.yml` 等の先頭）を開き、`id:` / ルート構造の書式を実物に合わせること（`data` 配列ルートか object ルートかを既存に倣う。fluids.yml が `/data/[*]/...` 形式なら thresholds/chipDraws/pollution を1要素の data 配列に包む形へ調整し、フォーリンキーの Path も合わせる）。

- [ ] **Step 3: csc.rsp に追加 + _CompileRequester の dummyText 変更**

`csc.rsp` 末尾に `/additionalfile:Assets/../../VanillaSchema/cleanRoom.yml` を追加。`_CompileRequester.cs` の `dummyText` を `"cleanroom-v2-task1"` に変更。

- [ ] **Step 4: テストMod データ追加**

`items.json` に追加（既存要素の書式で guid は新規生成）: `TestSemiconductorChipLv1`〜`Lv4`、`TestCleanRoomFilter`、`TestChipRawWafer`（抽選対象レシピの入力材料）。
`blocks.json` に6ブロック追加: `TestCleanRoomWall`（blockType CleanRoomWall, blockSize [1,1,1]）、`TestCleanRoomDoor`、`TestCleanRoomItemHatch`（inventoryConnectors は北面入力・南面出力。既存 Chest の inventoryConnectors 書式を流用し directions を [[0,0,-1]] / [[0,0,1]] とする）、`TestCleanRoomPipeHatch`、`TestCleanRoomAirFilter`（requiredPower 100, removalVolumePerSecond 5, filterItemGuid=TestCleanRoomFilter, filterCapacity 5000）、`TestCleanRoomMachine`（ElectricMachine 系ブロックの blockParam 書式を流用、requiredPower 100, inputSlotCount 2, outputSlotCount 2）。
`machineRecipes.json` に追加: `TestChipRecipe`（blockGuid=TestCleanRoomMachine, 入力 TestChipRawWafer×1, 時間1秒, 出力 TestSemiconductorChipLv1×1）。
`cleanRoom.json` を新規作成: pollution は初期係数、thresholds は A/B/C/D の4行（本計画冒頭の値）、chipDraws は TestChipRecipe に対し outputItemGuid=TestSemiconductorChipLv1、levels = Lv1..Lv4 の4行（weight 各 0.25、chipItemGuid は各 TestSemiconductorChipLvN）、euvSuccessRate 0.8。
`ForUnitTestModBlockId.cs` / `ForUnitTestItemId.cs` に定数を追加（既存定数の宣言方法を必ず確認して同じ形式で）。

- [ ] **Step 5: コンパイルして生成型を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。`BlockTypeConst.CleanRoomWall` と `Mooresmaster.Model.CleanRoomModule` が生成されている（`grep -rn "CleanRoomModule" Library/ --include="*.cs" -l` ではなく、次タスクのコード参照で確認するでもよい）。新規 yml が認識されない場合は Unity 再起動（uloop launch）してから再コンパイル。

- [ ] **Step 6: Commit**

```bash
git add VanillaSchema moorestech_server/Assets/Scripts/Core.Master moorestech_server/Assets/Scripts/Tests.Module
git commit -m "feat(cleanroom): add block type and cleanRoom master schemas"
```

---

### Task 2: CleanRoomMaster と MasterHolder 配線（欠損フォールバック付き）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Core.Master/CleanRoomMaster.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomMasterTest.cs`

**Interfaces (Produces):**
```csharp
namespace Core.Master
{
    public class CleanRoomMaster : IMasterValidator
    {
        public IReadOnlyList<CleanRoomMasterElement.ThresholdsElement> Thresholds { get; } // JSON順（良い順）
        public CleanRoomMasterElement.PollutionElement Pollution { get; }   // 欠損時 null
        public bool IsAvailable { get; }                                     // cleanRoom.json が存在し thresholds が1行以上
        public int OutThresholdIndex { get; }                                // == Thresholds.Count
        public bool TryGetThresholdIndexByClassName(string className, out int index); // セーブ復元用（行順に依存しない解決）
        public bool TryGetChipDraw(Guid machineRecipeGuid, out CleanRoomMasterElement.ChipDrawsElement chipDraw);
        public CleanRoomMaster(JToken jToken);   // 既存Masterのコンストラクタ形式に合わせる
        public static CleanRoomMaster CreateEmpty(); // 欠損フォールバック用
    }
    // MasterHolder に追加:
    public static CleanRoomMaster CleanRoomMaster { get; private set; }
}
```

生成型の実プロパティ名（ThresholdsElement 等の入れ子クラス名）は SourceGenerator の出力に依存する。**実装前に必ず生成コードを確認し**（`ItemMaster.cs` がどう生成型を参照しているかも参照）、上記シグネチャの型名を実際の生成名に合わせて読み替えること。

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomMasterTest
    {
        [Test]
        public void ThresholdsLoadedInOrderTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.CleanRoomMaster;
            Assert.IsTrue(master.IsAvailable);
            Assert.AreEqual(4, master.Thresholds.Count);
            // 良い順（A→D）で並び、濃度上限が単調増加であること
            // Rows are ordered best-to-worst with monotonically increasing concentration caps
            Assert.AreEqual(10.0, master.Thresholds[0].MaxConcentration, 0.0001);
            Assert.AreEqual(1000.0, master.Thresholds[3].MaxConcentration, 0.0001);
            Assert.AreEqual(4, master.OutThresholdIndex);
        }

        [Test]
        public void EmptyMasterFallbackTest()
        {
            var empty = CleanRoomMaster.CreateEmpty();
            Assert.IsFalse(empty.IsAvailable);
            Assert.AreEqual(0, empty.Thresholds.Count);
        }
    }
}
```

- [ ] **Step 2: テストが失敗（コンパイルエラー）することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `CleanRoomMaster` 未定義エラー

- [ ] **Step 3: CleanRoomMaster を実装**

`ItemMaster.cs` / `FluidMaster.cs` の実装形式（JToken → Loader → 生成型、Validate、Initialize）を開いて確認し、同じ形式で実装する。Validate では thresholds の maxConcentration が昇順であること・downBinRate が [0,1] であることを検証しエラーログを返す。`TryGetChipDraw` は machineRecipeGuid → 要素の Dictionary を Initialize で構築する。

- [ ] **Step 4: MasterHolder に配線（欠損フォールバック）**

`MasterHolder.Load` の MachineRecipesMaster ロードの後に追加:

```csharp
// cleanRoom.json を持たない Mod でも起動できるよう、欠損時は空マスタで代替する
// Fall back to an empty master when the mod ships no cleanRoom.json, so such mods still boot
CleanRoomMaster = TryGetJson(masterJsonFileContainer, new JsonFileName("cleanRoom"), out var cleanRoomJson)
    ? new CleanRoomMaster(cleanRoomJson)
    : CleanRoomMaster.CreateEmpty();
InitializeMaster(CleanRoomMaster);
```

`TryGetJson` は既存 `GetJson` の隣に private static で追加（`JsonContents.TryGetValue` を使う）。

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomMasterTest"`
Expected: 2件 PASS

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Core.Master moorestech_server/Assets/Scripts/Tests
git commit -m "feat(cleanroom): add CleanRoomMaster with missing-file fallback"
```

---

### Task 3: 境界コンポーネントと密閉検出コア

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/CleanRoom/ICleanRoomBoundaryComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Game.CleanRoom.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoom.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomCellSets.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetector.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomDetectionTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef`（Game.CleanRoom を references に追加）

**Interfaces (Produces):**
```csharp
// Game.Block.Interface.Component
public enum CleanRoomBoundaryKind { Wall, Door, ItemHatch, PipeHatch }
public interface ICleanRoomBoundaryComponent : IBlockComponent
{
    CleanRoomBoundaryKind BoundaryKind { get; }
}

// Game.CleanRoom
public class CleanRoom
{
    public int Id { get; }                                  // 揮発。永続キー禁止
    public IReadOnlyCollection<Vector3Int> Cells { get; }   // 占有セル含む全内部セル
    public int Volume { get; }                              // 空セル数
    public int SurfaceArea { get; }                         // 空セルが境界に接する面数
    public double ImpurityCount { get; }                    // N
    public int ThresholdIndex { get; }                      // Out = MasterHolder.CleanRoomMaster.OutThresholdIndex
    public void AddImpurity(double delta);                  // 0未満にはクランプ
    public void SetImpurity(double value);
    public void SetThresholdIndex(int index);
    public bool Contains(Vector3Int cell);
    public CleanRoom(int id, HashSet<Vector3Int> cells, int volume, int surfaceArea);
}

public static class CleanRoomCellSets
{
    public const int MaxRoomVolume = 4096;
    public static void BuildCellSets(IWorldBlockDatastore world, out HashSet<Vector3Int> boundaryCells, out HashSet<Vector3Int> occupiedCells);
    public static IEnumerable<Vector3Int> SixNeighbors(Vector3Int p);
    public static int LeakVisitedLimit(bool bboxInitialized, Vector3Int min, Vector3Int max);
}

public static class CleanRoomDetector
{
    // 全走査。visitedCellCount は探索コスト（予算制御・テスト検証用に呼び出し側が使う）
    public static List<CleanRoom> DetectAllRooms(IWorldBlockDatastore world, out int visitedCellCount);
    // 種集合から局所検出。firstRoomId から昇順採番
    public static List<CleanRoom> DetectFromSeeds(IReadOnlyList<Vector3Int> seeds,
        HashSet<Vector3Int> boundaryCells, HashSet<Vector3Int> occupiedCells,
        int firstRoomId, out int visitedCellCount);
}
```

**アルゴリズム（正確に実装すること）:**
- `BuildCellSets`: 全ブロックを走査し、`ICleanRoomBoundaryComponent` を持つブロックの占有セル（`BlockPositionInfo.MinPos`〜`MaxPos` の3重ループ）を boundaryCells へ、それ以外のブロックの占有セルを occupiedCells へ入れる
- flood-fill: 種（境界セルの6近傍のうち非境界セル）から、境界セルを通らずに6近傍へ拡張。占有セル（非境界ブロック）は**通過する**（Cells に含まれるが Volume に数えない）。空セルは Volume+1、空セルが境界に接する面ごとに SurfaceArea+1
- リーク判定: fill 中に触れた境界セルで AABB を成長させる。fill 済みセル数が `LeakVisitedLimit`（AABB体積×2+64、上限 MaxRoomVolume）を超えたらリーク（=部屋不成立）。壁に一度も触れないまま MaxRoomVolume を超えてもリーク
  - 根拠コメントに書くべき不変条件: 「密閉部屋の内部セルは、その部屋を囲う壁の外接箱の内側に必ず収まる」
- 訪問済みセルは種をまたいで共有し、同一連結域を二度探索しない。リークした連結域に接触した fill もリーク

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using System.Linq;
using Game.CleanRoom;
using Game.Context;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomDetectionTest
    {
        [Test]
        public void SealedRoomIsDetectedTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 内寸1x1x1（外形3x3x3）の箱を壁で組む
            // Build a 3x3x3 shell enclosing a single interior cell
            BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));

            var rooms = CleanRoomDetector.DetectAllRooms(ServerContext.WorldBlockDatastore, out _);
            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(1, rooms[0].Volume);
            Assert.AreEqual(6, rooms[0].SurfaceArea);
            Assert.IsTrue(rooms[0].Contains(new Vector3Int(1, 1, 1)));
        }

        [Test]
        public void MissingWallLeaksTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            // 壁を1枚壊す → リークして部屋不成立
            // Remove one wall block; the fill leaks and no room forms
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(1, 1, 0));

            var rooms = CleanRoomDetector.DetectAllRooms(ServerContext.WorldBlockDatastore, out _);
            Assert.AreEqual(0, rooms.Count);
        }

        [Test]
        public void InteriorMachineOccupiedCellReducesVolumeTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 内寸3x3x1（外形5x5x3）。内部にチェストを1個置くと V が1減り Cells には残る
            // Interior 3x3x1; an interior chest reduces V by one but stays inside Cells
            BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 2));
            AddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(2, 1, 2));

            var rooms = CleanRoomDetector.DetectAllRooms(ServerContext.WorldBlockDatastore, out _);
            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(8, rooms[0].Volume);       // 9 - 1
            Assert.IsTrue(rooms[0].Contains(new Vector3Int(2, 1, 2)));
        }

        #region Internal

        // min..max の外殻に壁ブロックを設置する（内部は空洞）
        // Place wall blocks on the shell of min..max, leaving the interior hollow
        public static void BuildBox(Vector3Int min, Vector3Int max)
        {
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
            {
                var isShell = x == min.x || x == max.x || y == min.y || y == max.y || z == min.z || z == max.z;
                if (isShell) AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(x, y, z));
            }
        }

        public static void AddBlock(BlockId blockId, Vector3Int pos)
        {
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        #endregion
    }
}
```

注意: `TryAddBlock` / `RemoveBlock` の実シグネチャと `ForUnitTestModBlockId` の型（BlockId か int か）を既存テスト（`Tests/CombinedTest/` 内の任意の設置テスト）で確認して合わせる。ヘルパーは後続タスクのテストからも使うため public static とする。

- [ ] **Step 2: コンパイルエラーを確認**（`uloop compile`、CleanRoomDetector 未定義）

- [ ] **Step 3: 実装**

- `ICleanRoomBoundaryComponent` + `CleanRoomBoundaryKind`（1ファイル）
- `CleanRoomBoundaryComponent`: kind を保持するだけの marker 実装（IsDestroy/Destroy 付き）
- `VanillaCleanRoomBoundaryTemplate`: コンストラクタで kind を受け、New/Load とも `CleanRoomBoundaryComponent` のみ載せた `BlockSystem` を返す（ハッチ挙動は後続タスクでこのテンプレートに追記する）。`VanillaIBlockTemplates` に4種を登録:
```csharp
BlockTypesDictionary.Add(BlockTypeConst.CleanRoomWall, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.Wall));
BlockTypesDictionary.Add(BlockTypeConst.CleanRoomDoor, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.Door));
BlockTypesDictionary.Add(BlockTypeConst.CleanRoomItemHatch, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.ItemHatch));
BlockTypesDictionary.Add(BlockTypeConst.CleanRoomPipeHatch, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.PipeHatch));
```
- `Game.CleanRoom.asmdef`: `Game.Gear.asmdef` を開いて構成を確認し、name=Game.CleanRoom、references に Game.Block.Interface / Game.World.Interface / Game.Context / Core.Master / Core.Update / UniRx / Core.Item.Interface を（実際にコンパイルが要求するものだけ）設定
- `CleanRoom` / `CleanRoomCellSets` / `CleanRoomDetector` を上記アルゴリズムどおり実装。`DetectFromSeeds` が本体で、`DetectAllRooms` は全境界セルの6近傍を種にした薄いラッパ

- [ ] **Step 4: テスト実行**（`--filter-value "CleanRoomDetectionTest"`、3件 PASS）

- [ ] **Step 5: Commit**（`feat(cleanroom): add boundary blocks and sealed-room detection core`）

---

### Task 4: CleanRoomDatastore（dirty差分更新・引き継ぎ・DI統合）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionService.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomCarryOver.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Server.Boot.asmdef`（Game.CleanRoom 参照追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomIncrementalDetectionTest.cs`

**Interfaces (Produces):**
```csharp
public class CleanRoomDatastore
{
    public const int DirtyCellBudgetPerTick = 8192;
    public IReadOnlyList<CleanRoom> Rooms { get; }
    public bool TryGetCleanRoomAt(Vector3Int cell, out CleanRoom room);
    public bool TryGetCleanRoom(IBlock block, out CleanRoom room);   // 全占有セルが同一部屋のとき true
    public void RebuildAll();                                        // ロード時用
    public CleanRoomDatastore(IWorldBlockDatastore worldBlockDatastore);  // GameUpdater購読 + 設置/破壊イベント購読
}

public class CleanRoomDetectionService
{
    // budget はテストが小さい値で直接構築して検証する（本番は DirtyCellBudgetPerTick）
    public CleanRoomDetectionService(IWorldBlockDatastore world, int dirtyCellBudgetPerTick);
    public IReadOnlyList<CleanRoom> Rooms { get; }
    public void OnBlockChanged(WorldBlockData blockData);  // 影響セルをdirtyへ
    public void ProcessDirtySeeds();                       // 毎tick呼ぶ。最低1シードは前進
    public void RebuildAll();
}

public static class CleanRoomCarryOver
{
    // 新部屋群に旧部屋群の N/行 をセル重なりで引き継ぐ。結合=N合算、分割=按分。対応しない旧部屋の状態は消滅
    public static void Apply(List<CleanRoom> newRooms, IReadOnlyList<CleanRoom> oldRooms);
}
```

**挙動仕様:**
- `CleanRoomDatastore` コンストラクタ: `GameUpdater.UpdateObservable.Subscribe`（GearNetworkDatastore と同じ）+ `ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent/OnBlockRemoveEvent` を購読して `OnBlockChanged` へ流す。購読 IDisposable は保持し `Destroy()` で解放
- `OnBlockChanged`: 境界ブロックなら占有セル+6近傍をシード化。非境界ブロックは既存部屋の Cells に重なる場合のみシード化（部屋外の設置は無視）
- `ProcessDirtySeeds`: シードキュー（HashSet で重複排除）を予算内で消化。tick 冒頭で `BuildCellSets` を1回だけ構築して共有。各シードにつき `DetectFromSeeds` で局所検出し、影響部屋（シード近傍 or 新部屋セルに重なる既存部屋）を差し替える。新部屋の Cells が既存部屋と完全一致ならインスタンス維持（何もしない）
- `CleanRoomCarryOver.Apply`: 各新部屋について、重なる旧部屋ごとに `N_old × overlap / oldCells.Count` を合算して N に設定。行（ThresholdIndex）は最大重なりの旧部屋から引き継ぎ。重なる旧部屋が無い新部屋は N=0・行=Out。**旧部屋の状態はこの引き継ぎ以外で生き残らない**（孤立・幽霊・猶予は実装しない）
- 新規部屋の初期行は Out（`MasterHolder.CleanRoomMaster.OutThresholdIndex`）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
// CleanRoomIncrementalDetectionTest.cs の主要ケース（BuildBox/AddBlock は CleanRoomDetectionTest のヘルパーを使う）
[Test]
public void PlaceAndRemoveWallUpdatesRoomsOverTicksTest()
{
    // DIから CleanRoomDatastore を取得し、壁で箱を組み GameUpdater.UpdateOneTick() を数回回すと部屋が現れる。
    // 壁を1枚壊して数tick回すと部屋が消える。
    var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
        .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
    var datastore = serviceProvider.GetService<CleanRoomDatastore>();

    CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
    for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
    Assert.AreEqual(1, datastore.Rooms.Count);

    ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(1, 1, 0));
    for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
    Assert.AreEqual(0, datastore.Rooms.Count);
}

[Test]
public void SplitRoomRedistributesImpurityTest()
{
    // 内寸3x1x1の部屋を作り N=90 を注入 → 中央に壁を置いて2部屋へ分割 → N が按分され総量が保存される
    // 旧部屋 Cells=3（全て空セル）。分割後 各1セル → 各 N=30、合計60（中央セル分の30は壁になったため消える）
    var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
        .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
    var datastore = serviceProvider.GetService<CleanRoomDatastore>();

    CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 2, 2));
    for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
    Assert.AreEqual(1, datastore.Rooms.Count);
    datastore.Rooms[0].SetImpurity(90.0);

    CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(2, 1, 1));
    for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();
    Assert.AreEqual(2, datastore.Rooms.Count);
    Assert.AreEqual(30.0, datastore.Rooms[0].ImpurityCount, 0.001);
    Assert.AreEqual(30.0, datastore.Rooms[1].ImpurityCount, 0.001);
}

[Test]
public void BudgetLimitsWorkPerTickButAlwaysProgressesTest()
{
    // CleanRoomDetectionService を budget=1 で直接構築し、複数シードが数tickに分けて消化されることを検証
    // （本番予算 8192 に依存しない、前進保証の検証）
}
```

- [ ] **Step 2: コンパイルエラー確認**
- [ ] **Step 3: 実装**（挙動仕様どおり。DI 登録は `GearNetworkDatastore` の登録行の直後に `initializerCollection.AddSingleton<CleanRoomDatastore>();`、services 側と eager 化の `GetService` 行も GearNetworkDatastore の3箇所と同じ形で追加）
- [ ] **Step 4: テスト実行**（`CleanRoomIncrementalDetectionTest` 全PASS + `CleanRoomDetectionTest` が回帰していないこと）
- [ ] **Step 5: Commit**（`feat(cleanroom): add CleanRoomDatastore with budgeted incremental detection`）

---

### Task 5: 純度シミュレーション（積分・ヒステリシス・幾何汚染）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomPurityLogic.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomPollution.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`（tick に純度積分を追加）
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/CleanRoom/ICleanRoomAirFilter.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/CleanRoom/ICleanRoomItemHatch.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/CleanRoom/ICleanRoomMachine.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/CleanRoom/CleanRoomEffect.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomPurityTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/CleanRoomPurityLogicTest.cs`

**Interfaces (Produces):**
```csharp
// Game.Block.Interface.Component
public interface ICleanRoomAirFilter : IBlockComponent
{
    double RemovalVolumePerSecond { get; }       // 電力割合・フィルター有無を織り込んだ実効 q
    void ApplyRemovedImpurity(double removed);   // フィルター摩耗の押し込み
}
public interface ICleanRoomItemHatch : IBlockComponent
{
    double RecentThroughputPerSecond { get; }
}
public interface ICleanRoomMachine : IBlockComponent
{
    bool IsPolluting { get; }                    // Processing 中のみ true
    void SetCleanRoomEffect(CleanRoomEffect effect);
}
public readonly struct CleanRoomEffect
{
    public readonly bool CanOperate;             // 密閉部屋内かつ行が Out でない
    public readonly int MaxChipLevel;
    public readonly double DownBinRate;
    public CleanRoomEffect(bool canOperate, int maxChipLevel, double downBinRate);
}

// Game.CleanRoom
public static class CleanRoomPurityLogic
{
    public const double PromoteConcentrationFactor = 0.8;
    public const double PromoteAirChangeFactor = 1.25;
    // 1tick積分: max(0, n + (aTotal - removalVolume * n / volume) * dt)
    public static double IntegrateTick(double impurityCount, int volume, double aTotalPerSecond, double removalVolumePerSecond, double deltaSeconds);
    // 行判定: 現在行より上（良い側）を狙うときだけ両条件に昇格マージンを掛ける。戻り値 rows.Count = Out
    public static int DecideThresholdIndex(int currentIndex, double concentration, double airChangeRate, IReadOnlyList<CleanRoomThresholdRow> rows);
}
public readonly struct CleanRoomThresholdRow
{
    public readonly double MaxConcentration;
    public readonly double RequiredAirChangeRate;
    public CleanRoomThresholdRow(double maxConcentration, double requiredAirChangeRate);
}
public static class CleanRoomPollution
{
    // 幾何項+接続点+稼働機械+ハッチ搬送。係数は MasterHolder.CleanRoomMaster.Pollution から
    public static double ComputeATotal(CleanRoom room, IWorldBlockDatastore world);
}
```

**挙動仕様:**
- Datastore の tick を「①ProcessDirtySeeds ②各部屋: A_total 算出 → フィルター収集 → 積分 → 摩耗配分 → 行更新 ③機械へ効果プッシュ（Task 8 で配線）」の順に拡張
- `ComputeATotal` の境界走査: 部屋の全セル×6近傍で境界ブロックを `BlockInstanceId` 重複排除して収集し、`BoundaryKind != Wall` を接続点として数え、`ICleanRoomItemHatch` の搬送レートを合算する（1回の走査で両方を取る）。稼働機械は部屋内部セルのブロックから `ICleanRoomMachine.IsPolluting` を数える（重複排除）。共有境界は各部屋が自分の走査でフル計上する（意図どおり）
- 部屋内フィルター収集: 部屋 Cells に占有セルが重なるブロックの `ICleanRoomAirFilter`（フィルターは境界ではなく**部屋内部に置く**設置物）。n·q = Σ RemovalVolumePerSecond
- 摩耗配分: 今tickの除去量 `removed = min(N, nq × C_old × dt)` を各フィルターへ `RemovalVolumePerSecond / nq` 比で `ApplyRemovedImpurity`
- 行更新: `ACH = nq / V`。`DecideThresholdIndex` で毎tick更新
- `CleanRoomMaster.Thresholds` → `CleanRoomThresholdRow` 変換は datastore 初期化時に1回

- [ ] **Step 1: 純関数のユニットテストを書く**（`CleanRoomPurityLogicTest`。DI 不要、直接呼ぶ）
  - `IntegrateTick`: A=16, nq=5, V=75 で反復すると C=N/V が 3.2 に収束（1000tick 後 |C−3.2|<0.01）
  - `IntegrateTick`: 除去0なら単調増加、負にならない
  - `DecideThresholdIndex`: 行[A(10,0.017),B(50,0.0083)]で、(現在B, C=9, ACH=0.02) → まだB（昇格には C≤8 かつ ACH≥0.02125）/ (現在B, C=7.9, ACH=0.022) → A / (現在A, C=10.5) → B（降格は素の閾値）/ どの行も満たさない → 2（Out）
- [ ] **Step 2: 失敗確認 → 実装 → PASS**
- [ ] **Step 3: 統合テストを書く**（`CleanRoomPurityTest`）
  - 密閉部屋（清浄機なし・機械なし）で数百tick回すと N が幾何項ぶん増加し、行が Out のまま
  - 期待A_total: 内寸1セル箱なら V=1,S=6,接続点0 → A_total=0.1+0.3=0.4/秒。100tick(5秒)後 N≈2.0（除去なし線形）を誤差1%で検証
- [ ] **Step 4: PASS 確認 → Commit**（`feat(cleanroom): add purity integration with hysteresis and geometric pollution`）

---

### Task 6: アイテムハッチとパイプハッチ

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomItemHatchComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomPipeHatchComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs`（ItemHatch/PipeHatch のケースでコンポーネント合成）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomHatchTest.cs`

**Interfaces:**
- Consumes: `ICleanRoomItemHatch`（Task 5）、`BlockTemplateUtil.CreateInventoryConnector`、`IFluidInventory.CreateFluidInventoryConnector`（`VanillaMachineTemplate.cs` と `VanillaFluidBlockTemplate.cs` の実物で確認）
- Produces: `CleanRoomItemHatchComponent : IBlockInventory, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomItemHatch`（中継バッファ上限4スタック、リング窓20tick）

**挙動仕様:**
- ItemHatch: `InsertItem` で中継バッファへ受け（満杯なら差し戻し＝`InsertionCheck` false）、毎tick `Update` で `BlockConnectorComponent<IBlockInventory>` の接続先へ押し出す。押し出した個数を長さ20のリングバッファへ記録し `RecentThroughputPerSecond = 窓合計 / (20 × SecondsPerTick)`。バッファ内容は `IBlockSaveState`（`GetSaveState`/復元コンストラクタ、`ItemStackSaveJsonObject` 利用。`FuelGearGeneratorItemComponent` など既存の IBlockSaveState 実装の形式で）
- PipeHatch: 既存 `FluidPipe` の内部実装（`VanillaFluidBlockTemplate` が組むコンポーネント）を確認し、同じ流体コンポーネント構成＋容量100で「流体が通る境界ブロック」を組む。流体コンポーネントが再利用できるならそれを載せるだけにする（新規実装しない）
- ベルト→ハッチ→チェストの搬送は既存インベントリ接続の仕組みに乗るため専用コードは書かない

- [ ] **Step 1: 失敗するテストを書く**
  - ハッチを含む密閉箱が部屋として成立する（ハッチ=境界）
  - ベルトコンベアからハッチへ挿入 → 数tick後、ハッチの出力面の先のチェストに届く（既存のベルト搬送テストの書き方を `Tests/CombinedTest` から探して流用）
  - バッファ満杯（4スタック投入済み・出力先なし）で `InsertItem` が差し戻す
  - 搬送直後の `RecentThroughputPerSecond` > 0、20tick 無搬送で 0 に戻る
  - 搬送レートが `ComputeATotal` に kHatch×レートとして乗る（部屋の N 増加速度で検証）
- [ ] **Step 2: 失敗確認 → 実装 → PASS → Commit**（`feat(cleanroom): add item/pipe hatches with throughput-based pollution`）

---

### Task 7: 空気清浄機（電力・フィルター摩耗）と基準例の固定

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomAirFilterComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomAirFilterTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`（CleanRoomAirFilter 登録）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomAirFilterTest.cs`

**Interfaces:**
- Produces: `CleanRoomAirFilterComponent : IElectricConsumer, IUpdatableBlockComponent, IOpenableBlockInventoryComponent（フィルタースロット1）, IBlockSaveState, ICleanRoomAirFilter`
  - `RemovalVolumePerSecond = removalVolumePerSecondParam × min(1, currentPower/requiredPower) × (フィルター残数>0 ? 1 : 0)`
  - `ApplyRemovedImpurity(removed)`: 累計に加算し、`filterCapacity` を超えるごとにフィルターアイテムを1個消費（スロットから減算）。累計残はセーブ対象
- 電力接続: `VanillaMachineTemplate` の `ElectricWireConnectorComponent` 組み立てを踏襲（consumer = 自コンポーネント）。`SupplyEnergy` は加算蓄積し `Update` 冒頭でラッチ（`MachineProcessContext` の SuppliedPower/CurrentPower と同じ流儀）
- フィルタースロットの Openable インベントリ実装は既存の単純スロットブロック（Chest 系 or 発電機の燃料スロット `VanillaPowerGeneratorTemplate` 側実装）を確認して同じ部品を使う

- [ ] **Step 1: 失敗するテストを書く**
  - **基準例（機械項なし）**: 内寸5×5×3の部屋（壁+アイテムハッチ1+ドア1）にフィルター清浄機1台（満電・フィルター装填）→ 十分な tick 後、C が A_total=14.0/5=2.8 に収束し、行が A（index 0）になる（A_total = 0.1×75 + 0.05×110 + 0.5×2 = 14.0。機械項は Task 8 で +2.0）
  - 電力半分（供給50/要求100）→ 実効q=2.5、C_eq=5.6
  - フィルター未装填 → 除去0、N 増加継続
  - 摩耗: 除去累計が 5000 を超えたらスロットのフィルターが1個減る
  - 大部屋不変条件: 内寸10×10×5・清浄機1台 → 行が A にならない（ACH 不足）
  - 満電の与え方は既存の電力系テスト（ElectricMachine のテスト）を探して同じ方法を使う（発電機+ワイヤー接続 or セグメント直接供給）
- [ ] **Step 2: 失敗確認 → 実装 → PASS → Commit**（`feat(cleanroom): add air filter machine with power scaling and filter wear`）

---

### Task 8: クリーンルーム機械（Halted状態・部屋ゲート）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`（`ProcessState` enum に `Halted` 追加 + `ToStr()` に case 追加のみ）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineBlockStateConst.cs`（`HaltedState = "halted"` 追加）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomMachineComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomMachineProcessorComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/HaltedMachineProcessState.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomMachineTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`（CleanRoomMachine 登録）
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`（tick 末尾で登録済み機械へ `SetCleanRoomEffect` をプッシュ）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomMachineTest.cs`

**Interfaces:**
- Produces: `CleanRoomMachineProcessorComponent : IBlockStateObservable, IUpdatableBlockComponent`
  - 構成: `MachineProcessContext` + `IdleMachineProcessState` + `ProcessingMachineProcessState` + `HaltedMachineProcessState` を再利用合成（`VanillaMachineProcessorComponent` の実装を開いて忠実に踏襲。共有クラスが internal の場合は同一アセンブリ内なのでそのまま使える）
  - ゲート: `Update` の状態遷移前に「effect.CanOperate == false かつ現在 Halted でない」なら強制的に Halted へ遷移（OnExit は呼ぶが加工ジョブは `ProcessingMachineProcessState` が保持したまま）。`HaltedMachineProcessState.GetNextUpdate` は CanOperate 回復時、保持ジョブがあれば Processing、なければ Idle を返す
  - `EffectiveRequestPower`: Halted 中は 0
- Produces: `CleanRoomMachineComponent : ICleanRoomMachine, IElectricConsumer`
  - `IsPolluting` = プロセッサが Processing、`SetCleanRoomEffect` は保持のみ（初期値は worst: CanOperate=false）。`RequestEnergy` はプロセッサの `EffectiveRequestPower`
- Datastore 側: `ICleanRoomMachine` を持つブロックを設置/破壊イベントで登録簿に載せ、tick 末尾で各機械に `TryGetCleanRoom(block)` の結果から `CleanRoomEffect` を計算してプッシュ。行→効果変換: 部屋なし or 行=Out → (false,0,0) / それ以外 → (true, row.MaxChipLevel, row.DownBinRate)
- チップ抽選の接続は Task 9（本タスクでは出力はレシピどおり）

- [ ] **Step 1: 失敗するテストを書く**
  - 部屋の外に設置した機械は材料を入れても Halted のまま加工しない、`RequestEnergy` が 0
  - クラスD以上の部屋内では加工が進み、レシピどおり出力される
  - 加工中に壁を壊す → Halted、残り時間が凍結、電力要求0。壁を戻し（部屋は Out スタート）→ まだ Halted。清浄機で行が D に達する → Processing 再開し完了する
- [ ] **Step 2: 失敗確認 → 実装 → PASS → Commit**（`feat(cleanroom): add clean-room machine with Halted state and room gating`）

---

### Task 9: チップ抽選（決定的乱数）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomChipDraw.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomMachineProcessorComponent.cs`（完了時に抽選して出力を差し替え。サイクルカウンタ保持）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/CleanRoomChipDrawTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomChipOutputTest.cs`

**Interfaces (Produces):**
```csharp
public static class CleanRoomChipDraw
{
    public enum Result { NotLeveled, NoOutput, Drawn }
    // dist: (level, weight, chipItemId) 昇順。maxLevel はクラス天井、downBinRate は格下げ率
    // 処理順: EUV失敗 → 天井切り詰め再正規化 → 基礎抽選 → 格下げ(Lv1据え置き) → 確定
    public static Result TryDraw(IReadOnlyList<(int level, double weight, ItemId chipItemId)> dist,
        int maxLevel, double downBinRate, double euvSuccessRate,
        long deterministicSeed, int outputIndex, out ItemId itemId);
}
```

**実装仕様:**
- 乱数: salt付き splitmix64。`Roll(seed, salt, outputIndex)` が [0,1) を返す。salt は EUV判定・基礎抽選・格下げで別の定数（相関排除）。実装:
```csharp
private static double Roll(long seed, ulong salt, int outputIndex)
{
    var x = (ulong)seed * 0x9E3779B97F4A7C15UL + salt + (ulong)(outputIndex + 1) * 0xBF58476D1CE4E5B9UL;
    x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
    x ^= x >> 27; x *= 0x94D049BB133111EBUL;
    x ^= x >> 31;
    return (x >> 11) * (1.0 / (1UL << 53));
}
```
- 天井以下に重みが残らない場合は NoOutput（黙って Lv1 を出さない）
- プロセッサ統合: 加工完了ごとにサイクルカウンタ（uint、セーブ対象）を進め、`seed = ((long)blockInstanceId.AsPrimitive() << 20) ^ cycleCount`。レシピ出力の各アイテムについて `MasterHolder.CleanRoomMaster.TryGetChipDraw(recipeGuid)` の分布に outputItemGuid が載っていれば抽選結果で置き換え（NoOutput ならそのスロットは出力なし）、載っていなければそのまま出す。抽選時の maxLevel/downBinRate は**完了時点**の受信効果から取る
- 産出物の容量確認（開始時の CanStoreOutputs）はレシピの素の出力で行う。チップはレシピ出力アイテムの置き換えであり個数は増えないため容量は悪化しない、という不変条件をコメントに書く

- [ ] **Step 1: 純関数テスト**（DI不要）
  - 同一(seed, outputIndex)で結果が常に同一
  - euvSuccessRate=0 → 常に NoOutput / =1 → NoOutput なし（maxLevel≥1, 分布あり）
  - maxLevel=2 で Lv3/Lv4 が出ない（seed 0..9999 の全数検査）
  - downBinRate=1 で Lv2 以上が必ず1段下がる、Lv1 は Lv1 のまま
  - 均等 weight・maxLevel=4・downBin=0・EUV=1 で seed 0..39999 の出現比率が各25%±2%
- [ ] **Step 2: 統合テスト**: クラスA部屋で加工完了 → 出力スロットにいずれかの TestSemiconductorChipLvN が入る。セーブ/ロード後も次サイクルの結果が（同一カウンタなら）一致する
- [ ] **Step 3: 失敗確認 → 実装 → PASS → Commit**（`feat(cleanroom): add deterministic chip level draw`）

---

### Task 10: セーブ/ロード

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomSaveData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`（GetSaveData/Restore）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`（Game.CleanRoom 参照）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/CleanRoomSaveLoadTest.cs`

**実装仕様:**
- `CleanRoomSaveData`: `[JsonProperty("impurityCount")] double` / `[JsonProperty("className")] string`（行 index はマスタ行順依存の揮発値のため保存禁止。クラス名で保存しロード時に `CleanRoomMaster.TryGetThresholdIndexByClassName` で解決、未解決なら Out）/ `[JsonProperty("cells")] List<int[]>`
- `WorldSaveAllInfoV1`: コンストラクタ末尾引数 + `[JsonProperty("cleanRoomRooms")] public List<CleanRoomSaveData> CleanRoomRooms { get; }`（null なら空リスト化。`railSegments` の null 対応と同じ形）
- `WorldLoaderFromJson.LoadOrInitialize`: `LoadBlockDataList` の直後に `_cleanRoomDatastore.RebuildAll(); _cleanRoomDatastore.Restore(load.CleanRoomRooms);`
- `Restore`: 各レコードを再検出済み部屋と最大セル重なりで照合。マッチした部屋に N を合算・行は最大重なりレコードを採用。マッチしないレコードは破棄
- 機械プロセッサのサイクルカウンタと Halted 状態は Task 8/9 のコンポーネントのセーブ JsonObject に含める（`VanillaMachineProcessorSaveJsonObject` の形式を踏襲した専用 JsonObject）
- コンストラクタ呼び出し側（AssembleSaveJsonText と、`WorldSaveAllInfoV1` を new している全テスト）を grep して引数追加

- [ ] **Step 1: 失敗するテストを書く**
  - 部屋を作り N と行を確定 → セーブ文字列生成 → 新規DIでロード → 部屋が再検出され N・行が一致
  - cleanRoomRooms キーの無い旧セーブ JSON（既存テストの最小セーブ文字列を流用）がロードでき、部屋は空状態から再検出される
  - 加工途中の機械（Processing、残り時間あり）がセーブ→ロードで残り時間ごと復元される
- [ ] **Step 2: 失敗確認 → 実装 → PASS → Commit**（`feat(cleanroom): persist room purity across save/load`）

---

### Task 11: 全体回帰・規約チェック

**Files:** なし（検証タスク）

- [ ] **Step 1:** `uloop run-tests --filter-value "CleanRoom"` 全PASS
- [ ] **Step 2:** 全体テスト `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests\."` （時間がかかる場合は少なくとも `CombinedTest` 全体）で既存回帰ゼロを確認
- [ ] **Step 3:** 規約 grep 監査（すべてヒット0であること）:
```bash
grep -rn "ForTest" moorestech_server/Assets/Scripts/Game.CleanRoom moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom --include="*.cs"
grep -rn "partial " moorestech_server/Assets/Scripts/Game.CleanRoom moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom --include="*.cs"
grep -rn "event Action" moorestech_server/Assets/Scripts/Game.CleanRoom moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom --include="*.cs"
grep -rnE "Task [0-9]|計画書|§" moorestech_server/Assets/Scripts/Game.CleanRoom moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom --include="*.cs"
awk 'END{}' # 200行超ファイルの検出:
find moorestech_server/Assets/Scripts/Game.CleanRoom moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom -name "*.cs" | xargs wc -l | awk '$1>200 && $2!="total"'
```
- [ ] **Step 4:** Commit（修正があれば）

---

### Task 12: AlphaMod 実データ（../moorestech_master）

**Files:**（別リポジトリ `/Users/katsumi/moorestech_master`）
- Modify: `server_v8` の Mod（`moorestechAlphaMod_8`）の `blocks.json` / `items.json` / `machineRecipes.json`
- Create: 同 Mod の `cleanRoom.json`

**手順:**
- [ ] items.json に半導体チップ系アイテムが既にあるか確認（`grep -i "チップ\|wafer\|半導体" items.json`）。無ければ ICチップLv1〜Lv4・クリーンルームフィルターを追加（既存アイテムの書式・画像パスは既存の類似アイテムを流用）
- [ ] blocks.json にクリーンルーム壁/ドア/アイテムハッチ/パイプハッチ/空気清浄機/EUV露光装置（CleanRoomMachine）を追加。モデルパスは既存の類似ブロック（壁系・機械系）のものを暫定流用し、`name` に (仮モデル) と書かない（表示名は正式名）
- [ ] machineRecipes.json に EUV露光装置のチップレシピを1件追加、cleanRoom.json に閾値・係数・チップ分布を記述
- [ ] tree2 のサーバーを AlphaMod でブート（テスト or uloop）してマスタロードエラーが無いことを確認
- [ ] moorestech_master リポジトリでコミット（`git -C /Users/katsumi/moorestech_master add/commit`。push はユーザー確認後）

---

### Task 13: 録画プレイテスト（動画提出物）

メインセッションが `unity-playmode-recorded-playtest` スキルを起動して実施（サブエージェントに委譲しない）。シナリオ: 部屋建設 → 清浄機設置・電力接続 → クラス成立 → 機械稼働・チップ産出 → 壁破壊で Halted → 再密閉+浄化で復帰。動画は `docs/superpowers/evidence/2026-07-08-cleanroom-v2-playtest.mp4` に保存。

---

## Self-Review 済み事項

- 仕様 §2〜§8 の全要求にタスクが対応（検出=T3/4、純度=T5、ハッチ=T6、清浄機=T7、機械=T8、抽選=T9、永続化=T10、マスタ=T1/2/12、受け入れ条件3=T13）
- 型名の一貫性: `CleanRoomEffect`/`ICleanRoomMachine`/`CleanRoomThresholdRow` は T5 で定義し T8/9 が消費
- 仕様 §9 受け入れ条件2（テストからしか呼ばれない機能の禁止）は T11 の grep 監査と T13 の実プレイで担保
