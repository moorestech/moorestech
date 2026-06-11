# クリーンルーム フェーズ3（空気清浄機ブロック＋フィルター＋電力＋汚染源）実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **moorestech 固有の必須ルール:**
> - `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認する。
> - テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"` で実行（クライアントプロジェクトからサーバーテストも走る）。
> - 新規サーバー `.cs`／新規 blockType／新規スキーマ生成型を認識させるには Unity の **再起動**が要る場合がある（Refresh では不足）。「型が見つからない」で失敗したら uloop で Unity 再起動してから再試行。「Domain Reload in progress」なら45秒待って再試行。
> - blockType スキーマ追加・SourceGenerator の手順は `edit-schema` スキルに従う。テスト作成は `creating-server-tests` スキルに従う。
> - 非ASCIIファイル編集時は AGENTS.md の「文字化け防止ワークフロー」を順守。`blocks.json`/`items.json`/`blocks.yml` は UTF-8 系。
> - **APIシグネチャ確認の原則:** 本プランのコードは既存コードベースのパターン（`VanillaMachineTemplate`/`VanillaElectricMachineComponent`/`VanillaMachineProcessorComponent`/`FuelGearGeneratorItemComponent`）から書いているが、メソッド名・名前空間・引数順は推定を含む。各 `.cs` を書く前に、本文で「確認」と指示した既存ファイルを開いて実シグネチャに合わせること。コンパイル/テストのチェックポイントが安全網。

**Goal:** フェーズ2で用意した注入インターフェース（`CleanRoomPollutionInput` の A_total、`ICleanRoomPurificationSource` の n·q）を実供給する。電力で動く空気清浄機ブロック（`CleanRoomAirPurifier`）を作り、満電時 q=5 m³/秒の除去能力・電力割合での減衰・フィルター仕事量消費（除去不純物の累計が filterCapacity=5000 ごとに1個消費・残0で除去停止）を実装する。同時に汚染源（`a_volume·V + a_surface·S + a_connector·接続点数 + A_machine·稼働機械数`、ハッチ/ドアは0スタブ）を `A_total` に実供給する `CleanRoomPollutionCalculator` を作る。基準部屋（V=75, S=110, 接続点2, 稼働機械1, 清浄機1台満電）で `A_total=16.0` → `C_eq=3.2` → クラスAが成立することを統合テストで固定する。

**Architecture:** 清浄機は `VanillaMachineTemplate` を雛形にした `VanillaAirPurifierTemplate`（New/Load）が組み立てる4コンポーネント構成。`AirPurifierProcessorComponent`（`IUpdatableBlockComponent, ICleanRoomPurificationSource`）が本体で、満電時 q × 電力割合 × (フィルター残>0?1:0) を `RemovalVolumePerSecond` として公開する。`AirPurifierElectricComponent`（`IElectricConsumer`）は `VanillaElectricMachineComponent` と同型で、設置時に既存 `ConnectMachineToElectricSegment` が近傍ポールの `EnergySegment` へ自動登録する（新規配線コード不要）。フィルター摩耗は「清浄機が C を知らない」ため**サービス側からのプッシュ**で行う：`CleanRoomPurityService` が毎tick各清浄機の除去寄与を計算して `ICleanRoomPurificationSource.ApplyRemovedImpurity(removed)` を呼び、清浄機は累計してフィルターを消費する（asmdef 依存方向 `Game.CleanRoom → Game.Block.Interface` を保つ）。`CleanRoomPollutionCalculator` は `CleanRoomDetectionSystem` の部屋ジオメトリ（V/S/境界種別）と部屋内の稼働機械数から A_total を純関数で算出する。`CleanRoomPurityService` は部屋内清浄機を `TryGetRoomContainingBlock` で集めて n·q を供給する。

**Tech Stack:** C# (Unity, moorestech_server), R3/UniRx `IObservable`（`GameUpdater.UpdateObservable`）, NUnit (Server.Tests), Newtonsoft.Json（ブロックstate保存）, Mooresmaster Source Generator (blocks.yml → BlocksModule), `Game.EnergySystem`（`IElectricConsumer`/`EnergySegment`）。

---

## 依存・前提（このプランの外）

| 前提 | 内容 | 影響 |
|---|---|---|
| **フェーズ1完了** | `CleanRoom`（Id, `Cells`, `Volume` V, `SurfaceArea` S）／`CleanRoomDetectionSystem`（`TryGetRoomAt`/`TryGetRoomContainingBlock`/再検出）／`ICleanRoomBoundaryComponent`（`CleanRoomBoundaryKind`）／asmdef `Game.CleanRoom` | 本プランの汚染計算・清浄機集計はこの土台に載る |
| **フェーズ2完了** | `CleanRoomPurityState`（N/C/クラス/段階）／`CleanRoomPurityService`（DI singleton, eager, tick更新）／`CleanRoomClass` 列挙＋判定ヘルパ／`CleanRoomPollutionInput`（A_total 注入インターフェース）／`ICleanRoomPurificationSource`（n·q 注入インターフェース）／`CleanRoomClassMaster`（`cleanRoomClasses.yml`）／セーブ統合 | **本プランはこの2つの注入インターフェースを「埋める」段**。インターフェース自体は再定義しない。供給実装と清浄機実装を足す |

> **⚠ クロスフェーズの調整事項（実装着手前に人間が確認すること。Self-Review §再掲）**
> 1. **`ICleanRoomPurificationSource` の所在と署名**: コードマップ§2は `Game.CleanRoom/Purity/ICleanRoomPurificationSource.cs` に置くと書くが、清浄機は `Game.Block` のコンポーネントであり、asmdef 依存方向は `Game.CleanRoom → Game.Block.Interface`（逆ではない）。フェーズ1が `ICleanRoomBoundaryComponent` を `Game.Block.Interface` に置いたのと同じ理由で、**`ICleanRoomPurificationSource` も `Game.Block.Interface/Component` に置く**必要がある。さらにフィルター摩耗を「除去不純物量に比例」させるには、清浄機が知らない C をサービスが押し込む必要があるので、フェーズ2の署名へ **`void ApplyRemovedImpurity(double removed)` を追加**する。
> 2. **`CleanRoomPurityService` の清浄機注入**: コードマップ§2のコンストラクタは `IReadOnlyList<ICleanRoomPurificationSource>` を DI 注入する形だが、清浄機は実行時設置ブロックなので**静的注入では集まらない**。本プランは `TryGetRoomContainingBlock` ＋部屋セル走査で毎tick動的収集する方式に差し替える。フェーズ2のコンストラクタ署名は要見直し。
> 3. **A_total=16 のジオメトリ前提**: バランス§5は「占有セルを V に算入しない」と書くが、フェーズ1の検出器は**境界コンポーネントを持つセルのみ**を除外し、機械占有セルは空気として V に数える。worked example §4 の `A_total=16` は**検出器の挙動とのみ整合**する（機械を部屋内に置いても V=75 のまま）。本プランは検出器に従い「占有セル除外」は実装しない。

---

## File Structure（フェーズ3で作成/変更するファイル）

**スキーマ／マスタ（清浄機ブロック＋フィルターアイテム）**
- Modify: `VanillaSchema/blocks.yml` — `blockType` enum に `CleanRoomAirPurifier` 追加＋blockParam の switch case（`removalVolumePerSecond` / `requestPower` / `filterItemSlotCount`）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json` — 清浄機ブロック追加
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json` — フィルターアイテム＋清浄機ブロックアイテム追加
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs` — `CleanRoomAirPurifierId` / `CleanRoomFilterItemGuid` アクセサ追加

**注入インターフェース拡張（Game.Block.Interface） — フェーズ2の `ICleanRoomPurificationSource` を移設/拡張**
- Modify(または Create): `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomPurificationSource.cs` — `RemovalVolumePerSecond` ＋ `ApplyRemovedImpurity(double)`（調整事項#1）

**清浄機ブロック実装（Game.Block）**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierFilterInventory.cs` — フィルタースロット＋仕事量ベース消費
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierProcessorComponent.cs` — `IUpdatableBlockComponent, ICleanRoomPurificationSource`。実効 q・除去量計上・フィルター消費
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierElectricComponent.cs` — `IElectricConsumer`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierSaveComponent.cs` — `IBlockSaveState`（フィルター残＋進捗）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaAirPurifierTemplate.cs` — `IBlockTemplate`（New/Load）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs` — `CleanRoomAirPurifier` を登録

**汚染計算＋部屋接続（Game.CleanRoom）**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPollutionCalculator.cs` — `CleanRoomPollutionInput` 実装＋純関数 `ComputeATotal`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityService.cs` — 部屋内清浄機を動的収集して n·q 供給＋除去寄与を `ApplyRemovedImpurity` で配分

**テスト**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPurifierTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPollutionTest.cs`

> 各 `.cs` 新規ファイルは Unity が `.meta` を自動生成する。`.meta` は手動作成禁止。

---

## 数値の唯一ソース（`docs/superpowers/plans/2026-06-06-cleanroom-balance-parameters.md`）

| パラメータ | 値 | 出典 |
|---|---|---|
| `q`（処理体積, 満電1台） | 5.0 m³/秒 | §3 |
| `requestPower` | 100 | §3 |
| `filterCapacity` | 5000 個/フィルター | §3 |
| `a_volume` | 0.10 個/(m³·秒) | §2 |
| `a_surface` | 0.05 個/(m²·秒) | §2 |
| `a_connector` | 0.50 個/(接続点·秒) | §2 |
| `A_machine` | 2.0 個/(稼働機械·秒) | §2 |
| `k_hatch` | 0.30（フェーズ3では搬送0なので寄与0） | §2 |
| `burst_door` | 15（フェーズ3では通過0なので寄与0） | §2 |
| tick | 50ms（secondsPerTick=0.05） | §0 |

**基準部屋（worked example §4）**: V=75, S=110, 接続点 2（**ハッチ1＋パイプコネクタ1**、Wall は接続点に数えない）, 稼働機械 1, 清浄機 1台満電。

> **接続点はドアを使わない理由（調整事項#5）**: フェーズ1プラン Task5 は「`ICleanRoomBoundaryComponent` を持つブロック＝密閉面」（ドアも密閉）と書くが、コードマップ§5 は `CleanRoomDoorComponent` を「flood-fill 上は貫通可能境界」と書く。両者が食い違うため、ドアを接続点に使うと検出器がドアからリークして部屋が成立しない可能性がある。本プランの統合テストは**ハッチ＋パイプコネクタ**（どちらの解釈でも確実に密閉）で接続点2を作る。接続点数・S・A_total は不変（16.0）。ドアの flood-fill 上の扱いは reconcile #5 として要確定。
```
A_total = A_machine·1 + a_volume·75 + a_surface·110 + a_connector·2
        = 2.0 + 7.5 + 5.5 + 1.0 = 16.0 個/秒
C_eq    = A_total / (n·q) = 16.0 / 5 = 3.2 個/m³  → クラスA域（≤10）
ACH     = n·q/V = 5/75 = 0.067 /秒 ≥ A要求 0.017 → クラスA成立
```

---

## Task 1: 清浄機 blockType ＋ フィルターアイテムをスキーマ／テストmodに追加

清浄機ブロックの blockType・param と、フィルターアイテムをテスト用 mod に足す。コード生成のみ。`edit-schema` スキルの手順に従うこと。

**Files:**
- Modify: `VanillaSchema/blocks.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `.../ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `.../ForUnitTest/mods/forUnitTest/master/items.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`

- [ ] **Step 1: blocks.yml の blockType enum に追加**

`VanillaSchema/blocks.yml` の `blockType` の `options:` 配列末尾へ:

```yaml
      - CleanRoomAirPurifier
```

- [ ] **Step 2: blockParam の switch/cases に追加**

既存の param 付き blockType（例 `ElectricMachine` の `when:` case）の書き方を確認してから、`removalVolumePerSecond`・`requestPower`・フィルタースロット・インベントリコネクタを持つ case を追加する。`ElectricMachineBlockParam` の `inventoryConnectors` の schema 記法をコピーすること（フィルター搬入をベルト等から受けられるように）:

```yaml
      - when: CleanRoomAirPurifier
        type: object
        properties:
          removalVolumePerSecond:
            type: number
          requestPower:
            type: number
          filterItemSlotCount:
            type: integer
          inventoryConnectors:
            # ElectricMachine の inventoryConnectors と同じ schema をコピー
            # Copy the same schema as ElectricMachine's inventoryConnectors
            ...
```

> 生成される型名は `CleanRoomAirPurifierBlockParam`、プロパティは `RemovalVolumePerSecond`(float)/`RequestPower`(float)/`FilterItemSlotCount`(int)/`InventoryConnectors` になる想定。Task 5/6 で参照確認する。

- [ ] **Step 3: SourceGenerator をトリガ**

`Core.Master/_CompileRequester.cs` の `dummyText` 定数の値を変更:

```csharp
private const string dummyText = "regenerate-cleanroom-phase3";
```

- [ ] **Step 4: テストmod の items.json にフィルターアイテム＋清浄機ブロックアイテムを追加**

既存末尾の itemGuid 連番に続けて2件追加（既存エントリの形式に合わせる。`maxStack`/`name`/`itemGuid`/`imagePath`/`sortPriority`/`recipeViewType`/`initialUnlocked`）:

```json
    {
      "maxStack": 100,
      "name": "TestCleanRoomFilter",
      "itemGuid": "00000000-0000-0000-1234-0000000000f1",
      "imagePath": "TestCleanRoomFilter",
      "sortPriority": 100,
      "recipeViewType": "ForceView",
      "initialUnlocked": true
    },
    {
      "maxStack": 100,
      "name": "TestCleanRoomAirPurifier",
      "itemGuid": "00000000-0000-0000-1234-0000000000f2",
      "imagePath": "TestCleanRoomAirPurifier",
      "sortPriority": 100,
      "recipeViewType": "ForceView",
      "initialUnlocked": true
    }
```

> ブロックは対応するブロックアイテムを要する慣習。既存 `blocks.json` のブロックがどの itemGuid と結びつくか（`itemGuid` フィールド有無）を確認し、清浄機ブロックエントリにも同様に紐づける。

- [ ] **Step 5: テストmod の blocks.json に清浄機ブロックを追加**

`ElectricMachine` エントリ（`requiredPower:100`＋`inventoryConnectors`）を雛形に、清浄機ブロックを末尾へ追加。param 値は本プランの数値ソースに合わせる:

```json
    {
      "maxStack": 100,
      "blockSize": [1, 1, 1],
      "name": "TestCleanRoomAirPurifier",
      "blockGuid": "00000000-0000-0000-0000-0000000000f2",
      "itemGuid": "00000000-0000-0000-1234-0000000000f2",
      "blockType": "CleanRoomAirPurifier",
      "blockParam": {
        "removalVolumePerSecond": 5.0,
        "requestPower": 100,
        "filterItemSlotCount": 1,
        "inventoryConnectors": { "...": "ElectricMachine と同じ inventoryConnectors をコピー" }
      }
    }
```

> `blockGuid`/`itemGuid` の実際の必須キー名・ネスト形は既存 blocks.json の `ElectricMachine` エントリを開いて一致させること。

- [ ] **Step 6: ForUnitTestModBlockId にアクセサを追加**

`Tests.Module/TestMod/ForUnitTestModBlockId.cs` に追加:

```csharp
        public static BlockId CleanRoomAirPurifierId => GetBlock("00000000-0000-0000-0000-0000000000f2");
        public static System.Guid CleanRoomFilterItemGuid => System.Guid.Parse("00000000-0000-0000-1234-0000000000f1");
```

- [ ] **Step 7: 再生成を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。`Mooresmaster.Model.BlocksModule.BlockTypeConst.CleanRoomAirPurifier` と `CleanRoomAirPurifierBlockParam` が生成される。

> 型未検出なら Unity 再起動（新規 blockType＋生成型のため Refresh では不足しうる）。「Domain Reload in progress」なら45秒待つ。

- [ ] **Step 8: Commit**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs moorestech_server/Assets/Scripts/Tests.Module/TestMod/
git commit -m "feat(cleanroom): 空気清浄機blockTypeとフィルターアイテムをスキーマ/テストmodに追加"
```

---

## Task 2: `ICleanRoomPurificationSource` を Game.Block.Interface へ移設/拡張

清浄機は `Game.Block` のコンポーネント。`Game.CleanRoom` が `Game.Block.Interface` を参照する一方向を保つため、このインターフェースは `Game.Block.Interface/Component` に置く（調整事項#1）。フィルター摩耗のためサービスが除去量を押し込む `ApplyRemovedImpurity` を足す。

**Files:**
- Modify(or Create): `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomPurificationSource.cs`

> フェーズ2で `Game.CleanRoom/Purity/ICleanRoomPurificationSource.cs` に作られている場合は**こちらへ移設**し、`CleanRoomPurityService` の `using` を更新する（Task 8 で実施）。`Game.CleanRoom.asmdef` は既に `Game.Block.Interface` を参照しているので新規参照追加は不要。

- [ ] **Step 1: インターフェースを定義**

`Game.Block.Interface/Component/ICleanRoomPurificationSource.cs`:

```csharp
namespace Game.Block.Interface.Component
{
    // 部屋の不純物を除去する供給源（空気清浄機）。CleanRoomPurityService が集計に使う。
    // A source that removes room impurity (air purifier); aggregated by CleanRoomPurityService.
    public interface ICleanRoomPurificationSource : IBlockComponent
    {
        // 満電時 q × 電力割合 × (フィルター残>0 ? 1 : 0)。n·q の自台寄与。
        // q × power-ratio × (filterRemaining>0 ? 1 : 0); this unit's contribution to n·q.
        double RemovalVolumePerSecond { get; }

        // サービスがこの台の今tickの除去不純物量を押し込む。フィルター摩耗に使う。
        // Service pushes this unit's removed-impurity for this tick; used for filter wear.
        void ApplyRemovedImpurity(double removed);
    }
}
```

> `IBlockComponent` の名前空間・必須メンバは `Game.Block.Interface/Component/IBlockComponent.cs` で確認（`bool IsDestroy`/`void Destroy()`）。

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomPurificationSource.cs
git commit -m "feat(cleanroom): ICleanRoomPurificationSourceをGame.Block.Interfaceへ移設しApplyRemovedImpurityを追加"
```

---

## Task 3: フィルターインベントリ（仕事量ベース消費）

フィルタースロットを保持し、累計除去量が `filterCapacity` に達するごとにフィルターを1個消費する。残0でフィルター無しを通知。`FuelGearGeneratorItemComponent`（`OpenableInventoryItemDataStoreService` 利用）を雛形にする。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierFilterInventory.cs`

- [ ] **Step 1: 失敗するテストを書く（除去累計でフィルターが減る／残0で HasFilter=false）**

`Tests/CombinedTest/Core/CleanRoomPurifierTest.cs` を新規作成:

```csharp
using System;
using Core.Master;
using Game.Block.Blocks.CleanRoom;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPurifierTest
    {
        private const double FilterCapacity = 5000;

        [Test]
        public void FilterInventory_ConsumesOneFilterPerCapacityOfRemovedImpurity()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            // フィルター2個でインベントリを作る。
            // Build an inventory holding 2 filters.
            var inventory = new AirPurifierFilterInventory(slotCount: 1, filterCapacity: FilterCapacity);
            inventory.InsertItem(itemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 2));

            Assert.IsTrue(inventory.HasFilter, "2 filters present");

            // capacity 未満の摩耗ではまだ消費しない。
            // No consumption below one capacity worth of wear.
            inventory.AddRemovedImpurity(FilterCapacity - 1);
            Assert.AreEqual(2, inventory.FilterRemaining);

            // capacity を跨いだら1個消費。
            // Crossing one capacity consumes exactly one filter.
            inventory.AddRemovedImpurity(2);
            Assert.AreEqual(1, inventory.FilterRemaining);

            // もう1個分摩耗させて残0、HasFilter=false。
            // Wear another full capacity to deplete, HasFilter becomes false.
            inventory.AddRemovedImpurity(FilterCapacity);
            Assert.AreEqual(0, inventory.FilterRemaining);
            Assert.IsFalse(inventory.HasFilter, "no filters remaining");
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "FilterInventory_ConsumesOneFilterPerCapacity"`
Expected: FAIL（`AirPurifierFilterInventory` 未定義）。型未検出なら Unity 再起動。

- [ ] **Step 3: フィルターインベントリを実装**

`Game.Block/Blocks/CleanRoom/AirPurifierFilterInventory.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Game.Context;

namespace Game.Block.Blocks.CleanRoom
{
    // フィルタースロット。除去した不純物の累計が容量に達するごとにフィルターを1個消費する。
    // Filter slots; consumes one filter each time accumulated removed-impurity reaches capacity.
    public class AirPurifierFilterInventory
    {
        public bool HasFilter => FilterRemaining > 0;
        public int FilterRemaining => CountFilters();

        private readonly double _filterCapacity;
        private readonly OpenableInventoryItemDataStoreService _inventory;
        private double _wearProgress;

        public AirPurifierFilterInventory(int slotCount, double filterCapacity)
        {
            _filterCapacity = filterCapacity;
            _inventory = new OpenableInventoryItemDataStoreService(_ => { }, ServerContext.ItemStackFactory, Math.Max(1, slotCount));
        }

        // 除去した不純物量を累計し、容量ごとにフィルターを1個減らす。
        // Accumulate removed impurity and drop one filter per capacity crossed.
        public void AddRemovedImpurity(double removed)
        {
            if (removed <= 0 || !HasFilter) return;
            _wearProgress += removed;

            while (_wearProgress >= _filterCapacity && HasFilter)
            {
                _wearProgress -= _filterCapacity;
                ConsumeOneFilter();
            }
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _inventory.InsertItem(itemStack);
        }

        // セーブ/ロード復元用。摩耗進捗とスロットを直接読む/書く。
        // For save/load restore: read/write the wear progress and slots directly.
        public double WearProgress => _wearProgress;
        public void SetWearProgress(double progress) => _wearProgress = progress;
        public IReadOnlyList<IItemStack> InventoryItems => _inventory.InventoryItems;
        public void SetItem(int slot, IItemStack itemStack) => _inventory.SetItem(slot, itemStack);
        public int SlotSize => _inventory.GetSlotSize();
        public IItemStack GetItem(int slot) => _inventory.GetItem(slot);

        #region Internal

        int CountFilters()
        {
            var count = 0;
            for (var i = 0; i < _inventory.GetSlotSize(); i++) count += _inventory.GetItem(i).Count;
            return count;
        }

        void ConsumeOneFilter()
        {
            for (var i = 0; i < _inventory.GetSlotSize(); i++)
            {
                var item = _inventory.GetItem(i);
                if (item.Count <= 0) continue;
                _inventory.SetItem(i, item.SubItem(1));
                return;
            }
        }

        #endregion
    }
}
```

> `OpenableInventoryItemDataStoreService` のコンストラクタ引数順・`InsertItem`/`GetItem`/`SetItem`/`GetSlotSize`/`InventoryItems`・`IItemStack.SubItem(int)`/`Count` は `FuelGearGeneratorItemComponent.cs` と `Core.Item.Interface/IItemStack.cs` で確認。`SubItem` が無ければ `ServerContext.ItemStackFactory.Create(item.Id, item.Count-1)` で代替。

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "FilterInventory_ConsumesOneFilterPerCapacity"`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierFilterInventory.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPurifierTest.cs
git commit -m "feat(cleanroom): フィルター仕事量ベース消費のAirPurifierFilterInventoryを追加"
```

---

## Task 4: 清浄機プロセッサ（実効q・電力割合・除去寄与・フィルター消費）

清浄機本体。`IUpdatableBlockComponent, ICleanRoomPurificationSource`。`SupplyPower` で電力を受け、`RemovalVolumePerSecond = q × (currentPower/RequestPower クランプ1) × (フィルター残>0?1:0)`。サービスが押し込む `ApplyRemovedImpurity` で摩耗を計上する。`VanillaMachineProcessorComponent` の `_usedPower`/`_currentPower`/`SupplyPower`/`Update` の電力保持パターンを踏襲。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierProcessorComponent.cs`

- [ ] **Step 1: 失敗するテストを書く（満電でq、半電で半分、フィルター無しで0）**

`CleanRoomPurifierTest.cs` に追加:

```csharp
        [Test]
        public void Processor_RemovalScalesWithPowerRatioAndFilterPresence()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var filter = new AirPurifierFilterInventory(slotCount: 1, filterCapacity: 5000);
            filter.InsertItem(itemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 1));

            // q=5, requestPower=100。
            // q=5, requestPower=100.
            var processor = new AirPurifierProcessorComponent(removalVolumePerSecond: 5.0, requestPower: 100f, filter);

            // 給電前は0（_usedPower 初期/未給電）。
            // No power supplied yet => 0.
            Assert.AreEqual(0.0, processor.RemovalVolumePerSecond, 1e-9);

            // 満電で q=5.0。
            // Full power => q=5.0.
            processor.SupplyPower(100f);
            Assert.AreEqual(5.0, processor.RemovalVolumePerSecond, 1e-9);

            // 半電で 2.5（割合 0.5）。
            // Half power => 2.5 (ratio 0.5).
            processor.SupplyPower(50f);
            Assert.AreEqual(2.5, processor.RemovalVolumePerSecond, 1e-9);

            // 過給電は1にクランプ（q を超えない）。
            // Over-supply clamps ratio to 1 (never exceeds q).
            processor.SupplyPower(1000f);
            Assert.AreEqual(5.0, processor.RemovalVolumePerSecond, 1e-9);

            // 給電が来ないtickでUpdateを回すと電力が落ち、除去も0になる（_usedPower decay経路を固定）。
            // An Update tick with no fresh supply decays power to 0, so removal becomes 0 (pins _usedPower decay path).
            processor.SupplyPower(100f);
            processor.Update(); // この回は給電があったので維持
            Assert.AreEqual(5.0, processor.RemovalVolumePerSecond, 1e-9);
            processor.Update(); // 給電が無いまま2回目のUpdateで電力decay
            Assert.AreEqual(0.0, processor.RemovalVolumePerSecond, 1e-9);
        }

        [Test]
        public void Processor_NoFilter_RemovalIsZero()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // フィルター無しのインベントリ。
            // Inventory with no filters.
            var filter = new AirPurifierFilterInventory(slotCount: 1, filterCapacity: 5000);
            var processor = new AirPurifierProcessorComponent(removalVolumePerSecond: 5.0, requestPower: 100f, filter);

            processor.SupplyPower(100f);
            Assert.AreEqual(0.0, processor.RemovalVolumePerSecond, 1e-9);
        }
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Processor_RemovalScalesWithPowerRatioAndFilterPresence|Processor_NoFilter_RemovalIsZero"`
Expected: FAIL（`AirPurifierProcessorComponent` 未定義）。

- [ ] **Step 3: プロセッサを実装**

`Game.Block/Blocks/CleanRoom/AirPurifierProcessorComponent.cs`:

```csharp
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // 清浄機本体。電力割合で実効除去能力が決まり、除去量に比例してフィルターを摩耗させる。
    // Purifier core; effective removal scales with power ratio, filter wears by removed amount.
    public class AirPurifierProcessorComponent : IUpdatableBlockComponent, ICleanRoomPurificationSource
    {
        public readonly float RequestPower;

        private readonly double _removalVolumePerSecond; // 満電1台の q
        private readonly AirPurifierFilterInventory _filter;

        // VanillaMachineProcessorComponent と同じ電力保持パターン。
        // Same power-retention pattern as VanillaMachineProcessorComponent.
        private bool _usedPower;
        private float _currentPower;

        public bool IsDestroy { get; private set; }

        public AirPurifierProcessorComponent(double removalVolumePerSecond, float requestPower, AirPurifierFilterInventory filter)
        {
            _removalVolumePerSecond = removalVolumePerSecond;
            RequestPower = requestPower;
            _filter = filter;
        }

        // q × 電力割合(≤1) × (フィルター残>0 ? 1 : 0)。
        // q × power-ratio(≤1) × (filter present ? 1 : 0).
        public double RemovalVolumePerSecond
        {
            get
            {
                if (!_filter.HasFilter) return 0.0;
                if (RequestPower <= 0f) return _removalVolumePerSecond;
                var ratio = _currentPower / RequestPower;
                if (ratio > 1f) ratio = 1f;
                if (ratio < 0f) ratio = 0f;
                return _removalVolumePerSecond * ratio;
            }
        }

        // サービスがこの台の今tickの除去不純物量を押し込む。フィルター摩耗。
        // Service pushes this unit's removed-impurity for this tick; filter wear.
        public void ApplyRemovedImpurity(double removed)
        {
            BlockException.CheckDestroy(this);
            _filter.AddRemovedImpurity(removed);
        }

        // VanillaMachineProcessorComponent.SupplyPower と同じく次tick/次給電まで保持。
        // Hold power until next supply/update, like VanillaMachineProcessorComponent.SupplyPower.
        public void SupplyPower(float power)
        {
            BlockException.CheckDestroy(this);
            _usedPower = false;
            _currentPower = power;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (_usedPower)
            {
                _usedPower = false;
                _currentPower = 0f;
            }
            _usedPower = true;
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
```

> `BlockException.CheckDestroy(this)` の名前空間・存在は `VanillaMachineProcessorComponent.cs` で確認。`_usedPower` の意味（給電が来ない tick で電力を0へ落とす）も同ファイルで確認すること。

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Processor_RemovalScalesWithPowerRatioAndFilterPresence|Processor_NoFilter_RemovalIsZero"`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierProcessorComponent.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPurifierTest.cs
git commit -m "feat(cleanroom): 電力割合とフィルター連動のAirPurifierProcessorComponentを追加"
```

---

## Task 5: 電力コンポーネント（IElectricConsumer）

`VanillaElectricMachineComponent` と同型。`RequestEnergy => new ElectricPower(processor.RequestPower)`、`SupplyEnergy => processor.SupplyPower(power.AsPrimitive())`。これにより設置時に既存 `ConnectMachineToElectricSegment` が自動配線する（Task 8 の統合テストで検証）。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierElectricComponent.cs`

> このコンポーネントは Task 8 の「ポール＋発電機で自動給電される」統合テストで初めて検証する（ここでは単体テストを増やさず、コンパイルのみ）。

- [ ] **Step 1: 実装**

`Game.Block/Blocks/CleanRoom/AirPurifierElectricComponent.cs`:

```csharp
using Game.Block.Interface;
using Game.EnergySystem;

namespace Game.Block.Blocks.CleanRoom
{
    // 電力で動く清浄機の消費口。VanillaElectricMachineComponent と同じ IElectricConsumer 経路。
    // Electric consumer for the purifier; same IElectricConsumer path as VanillaElectricMachineComponent.
    public class AirPurifierElectricComponent : IElectricConsumer
    {
        private readonly AirPurifierProcessorComponent _processor;

        public AirPurifierElectricComponent(BlockInstanceId blockInstanceId, AirPurifierProcessorComponent processor)
        {
            BlockInstanceId = blockInstanceId;
            _processor = processor;
        }

        public BlockInstanceId BlockInstanceId { get; }
        public bool IsDestroy { get; private set; }

        public ElectricPower RequestEnergy => new ElectricPower(_processor.RequestPower);

        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            _processor.SupplyPower(power.AsPrimitive());
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
```

> 名前空間・`ElectricPower` のコンストラクタ・`AsPrimitive()`・`BlockException.CheckDestroy` は `VanillaElectricMachineComponent.cs` で確認。

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierElectricComponent.cs
git commit -m "feat(cleanroom): IElectricConsumerのAirPurifierElectricComponentを追加"
```

---

## Task 6: セーブコンポーネント（IBlockSaveState）round-trip

フィルター残＋摩耗進捗を保存/復元する。`FuelGearGeneratorItemComponent` の `IBlockSaveState` 実装と `VanillaMachineSaveComponent` の JSON 形式を参考にする。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierSaveComponent.cs`

- [ ] **Step 1: 失敗するテストを書く（state を書き→新インベントリへ復元→残量と進捗が一致）**

`CleanRoomPurifierTest.cs` に追加:

```csharp
        [Test]
        public void SaveComponent_RoundTripsFilterCountAndWearProgress()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            var filter = new AirPurifierFilterInventory(slotCount: 1, filterCapacity: 5000);
            filter.InsertItem(itemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 3));
            filter.AddRemovedImpurity(1234); // 進捗を残す（消費は跨がない）

            var save = new AirPurifierSaveComponent(filter);
            var json = save.GetSaveState();

            // 別インベントリへ復元。
            // Restore into a fresh inventory.
            var restored = new AirPurifierFilterInventory(slotCount: 1, filterCapacity: 5000);
            AirPurifierSaveComponent.Restore(restored, json);

            Assert.AreEqual(3, restored.FilterRemaining);
            Assert.AreEqual(1234, restored.WearProgress, 1e-6);
        }
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveComponent_RoundTripsFilterCountAndWearProgress"`
Expected: FAIL（`AirPurifierSaveComponent` 未定義）。

- [ ] **Step 3: セーブコンポーネントを実装**

`Game.Block/Blocks/CleanRoom/AirPurifierSaveComponent.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // 清浄機のフィルター残量と摩耗進捗を保存/復元する。
    // Saves/restores the purifier's filter slots and wear progress.
    public class AirPurifierSaveComponent : IBlockSaveState
    {
        public string SaveKey => "cleanRoomAirPurifier";
        public bool IsDestroy { get; private set; }

        private readonly AirPurifierFilterInventory _filter;

        public AirPurifierSaveComponent(AirPurifierFilterInventory filter)
        {
            _filter = filter;
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var json = new AirPurifierSaveJsonObject
            {
                WearProgress = _filter.WearProgress,
                Filters = _filter.InventoryItems.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
            };
            return JsonConvert.SerializeObject(json);
        }

        // ロード時、保存JSONから既存インベントリへ書き戻す。
        // On load, write the saved JSON back into the given inventory.
        public static void Restore(AirPurifierFilterInventory filter, string stateRaw)
        {
            var json = JsonConvert.DeserializeObject<AirPurifierSaveJsonObject>(stateRaw);
            filter.SetWearProgress(json.WearProgress);
            for (var i = 0; i < json.Filters.Count && i < filter.SlotSize; i++)
            {
                filter.SetItem(i, json.Filters[i].ToItemStack());
            }
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }

    public class AirPurifierSaveJsonObject
    {
        [JsonProperty("wearProgress")] public double WearProgress;
        [JsonProperty("filters")] public List<ItemStackSaveJsonObject> Filters;
    }
}
```

> `ItemStackSaveJsonObject` のコンストラクタ（`new ItemStackSaveJsonObject(IItemStack)`）と `IItemStack` への復元方法（`.ToItemStack()` か `ServerContext.ItemStackFactory.Create(...)`）は `VanillaMachineSaveComponent.cs`/`FuelGearGeneratorItemComponent.cs` で確認し一致させる。復元メソッド名が違えば置換。

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveComponent_RoundTripsFilterCountAndWearProgress"`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/AirPurifierSaveComponent.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPurifierTest.cs
git commit -m "feat(cleanroom): フィルター残/進捗をround-tripするAirPurifierSaveComponentを追加"
```

---

## Task 7: 清浄機テンプレートと登録

4コンポーネントを組み立てる `VanillaAirPurifierTemplate`（New/Load）。`VanillaMachineTemplate` を雛形にする。`VanillaIBlockTemplates` に登録すると、設置→`CleanRoomAirPurifier` ブロックが生成され、`IElectricConsumer` の自動配線が効くようになる。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaAirPurifierTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`

- [ ] **Step 1: 失敗するテストを書く（設置すると清浄機コンポーネントが揃う）**

`CleanRoomPurifierTest.cs` に追加:

```csharp
        [Test]
        public void Template_PlacesPurifierWithAllComponents()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomAirPurifierId, UnityEngine.Vector3Int.one,
                BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var block);

            Assert.IsTrue(block.ExistsComponent<AirPurifierProcessorComponent>());
            Assert.IsTrue(block.ExistsComponent<AirPurifierElectricComponent>());
            Assert.IsTrue(block.ExistsComponent<Game.Block.Interface.Component.ICleanRoomPurificationSource>());
            Assert.IsTrue(block.ExistsComponent<Game.Block.Interface.Component.IBlockSaveState>());
        }
```

> `ExistsComponent<T>`/`GetComponent<T>` の正確な API は `Game.Block.Interface/IBlock.cs` で確認。`using Game.Block.Interface;`/`Game.Block.Interface.Extension;` が要る場合がある。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Template_PlacesPurifierWithAllComponents"`
Expected: FAIL（`CleanRoomAirPurifier` 未登録で生成不可、または `VanillaAirPurifierTemplate` 未定義）。

- [ ] **Step 3: テンプレートを実装**

`Game.Block/Factory/BlockTemplate/VanillaAirPurifierTemplate.cs`:

```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    // 空気清浄機ブロックを組み立てる。4コンポーネント（プロセッサ/電力/セーブ/コネクタ）。
    // Builds the air purifier block: processor / electric / save / connector components.
    public class VanillaAirPurifierTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Build(blockMasterElement, blockInstanceId, blockPositionInfo, componentStates: null);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Build(blockMasterElement, blockInstanceId, blockPositionInfo, componentStates);
        }

        private IBlock Build(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo, Dictionary<string, string> componentStates)
        {
            var param = blockMasterElement.BlockParam as CleanRoomAirPurifierBlockParam;

            // フィルタースロット＋プロセッサ。Load 時は保存stateを復元。
            // Filter slots + processor; restore saved state on Load.
            var filter = new AirPurifierFilterInventory(param.FilterItemSlotCount, AirPurifierProcessorComponent.FilterCapacity);
            var processor = new AirPurifierProcessorComponent(param.RemovalVolumePerSecond, param.RequestPower, filter);
            var save = new AirPurifierSaveComponent(filter);
            var electric = new AirPurifierElectricComponent(blockInstanceId, processor);

            if (componentStates != null && componentStates.TryGetValue(save.SaveKey, out var stateRaw))
            {
                AirPurifierSaveComponent.Restore(filter, stateRaw);
            }

            // ベルト等からフィルターを搬入できるよう inventory コネクタを付ける。
            // Attach an inventory connector so belts can feed filters in.
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);

            var components = new List<IBlockComponent>
            {
                processor,
                electric,
                save,
                connector,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
```

> 確認: `BlockTemplateUtil.CreateInventoryConnector` の引数型（`InventoryConnectors`）と `BlockSystem` のコンストラクタ署名は `VanillaMachineTemplate.cs` と一致させる。`CleanRoomAirPurifierBlockParam` のプロパティ名は Task 1 の生成型に合わせる。`AirPurifierProcessorComponent.FilterCapacity` は Task 4 のクラスに `public const double FilterCapacity = 5000;` を足すか、テンプレートで `5000` 定数を直接渡す（数値ソース§3）。フィルターを `IOpenableBlockInventoryComponent` 経由で外部から入れたい場合は `AirPurifierFilterInventory` に `IOpenableBlockInventoryComponent` を実装して components に足す（フェーズ3では搬入テストは必須でないため最小構成でよい）。

- [ ] **Step 4: VanillaIBlockTemplates に登録**

`Game.Block/Factory/VanillaIBlockTemplates.cs` のコンストラクタ内 `BlockTypesDictionary.Add(...)` 群の末尾へ:

```csharp
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomAirPurifier, new VanillaAirPurifierTemplate());
```

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Template_PlacesPurifierWithAllComponents"`
Expected: PASS。型未検出なら Unity 再起動。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaAirPurifierTemplate.cs moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPurifierTest.cs
git commit -m "feat(cleanroom): VanillaAirPurifierTemplateを追加しblockType登録"
```

---

## Task 8: 汚染計算（CleanRoomPollutionCalculator）と純関数 ComputeATotal

`CleanRoomPollutionInput` を実装し、部屋の V/S・境界種別の接続点数・部屋内稼働機械数から A_total を算出する。ハッチ/ドアは0スタブ（フェーズ5で実装）。worked example の `A_total=16` を**純関数 `ComputeATotal` で固定**する（決定的・部屋構築不要）。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPollutionCalculator.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPollutionTest.cs`

- [ ] **Step 1: 失敗するテストを書く（純関数で基準部屋の A_total=16.0）**

`Tests/CombinedTest/Core/CleanRoomPollutionTest.cs` を新規作成:

```csharp
using NUnit.Framework;
using Game.CleanRoom.Purity;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPollutionTest
    {
        [Test]
        public void ComputeATotal_ReferenceRoom_Is16()
        {
            // 基準部屋: V=75, S=110, 接続点2(ハッチ1+ドア1), 稼働機械1, ハッチ搬送0, ドア通過0。
            // Reference room: V=75, S=110, connectors=2, running machines=1, hatch=0, door=0.
            var aTotal = CleanRoomPollutionCalculator.ComputeATotal(
                volume: 75, surfaceArea: 110, connectorCount: 2, runningMachineCount: 1,
                hatchThroughputPerSecond: 0.0, doorBurst: 0.0);

            // 2.0 + 0.10*75 + 0.05*110 + 0.50*2 = 16.0
            Assert.AreEqual(16.0, aTotal, 1e-9);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ComputeATotal_ReferenceRoom_Is16"`
Expected: FAIL（`CleanRoomPollutionCalculator` 未定義）。

- [ ] **Step 3: 汚染計算を実装**

`Game.CleanRoom/Purity/CleanRoomPollutionCalculator.cs`:

```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using UnityEngine;

namespace Game.CleanRoom.Purity
{
    // A_total を部屋ジオメトリ・接続点・稼働機械から算出する CleanRoomPollutionInput 実装。
    // CleanRoomPollutionInput impl that computes A_total from geometry, connectors, running machines.
    public class CleanRoomPollutionCalculator : CleanRoomPollutionInput
    {
        // 数値ソース §2（balance-parameters）。
        // Coefficients from balance-parameters §2.
        private const double AVolume = 0.10;
        private const double ASurface = 0.05;
        private const double AConnector = 0.50;
        private const double AMachine = 2.0;
        private const double KHatch = 0.30;

        private readonly CleanRoomDetectionSystem _detection;

        public CleanRoomPollutionCalculator(CleanRoomDetectionSystem detection)
        {
            _detection = detection;
        }

        // 部屋ごとの A_total。フェーズ2の OnTick がこれを使う。
        // A_total per room; consumed by phase-2 OnTick.
        public double GetATotal(CleanRoom room)
        {
            var connectorCount = CountConnectors(room);
            var runningMachines = CountRunningMachines(room);

            // ハッチ/ドアはフェーズ5で計量。フェーズ3は0。
            // Hatch/door metering arrives in phase 5; 0 here.
            return ComputeATotal(room.Volume, room.SurfaceArea, connectorCount, runningMachines,
                hatchThroughputPerSecond: 0.0, doorBurst: 0.0);
        }

        // 純関数。worked example の固定アサーションはここを叩く。
        // Pure function; the worked-example assertion targets this.
        public static double ComputeATotal(int volume, int surfaceArea, int connectorCount, int runningMachineCount,
            double hatchThroughputPerSecond, double doorBurst)
        {
            return AMachine * runningMachineCount
                   + KHatch * hatchThroughputPerSecond
                   + doorBurst
                   + AVolume * volume
                   + ASurface * surfaceArea
                   + AConnector * connectorCount;
        }

        #region Internal

        // 部屋セルに面する境界ブロックのうち、Wall 以外（ハッチ/ドア/パイプコネクタ）を接続点として数える。
        // Count boundary blocks adjacent to the room that are non-Wall (hatch/door/pipe) as connectors.
        int CountConnectors(CleanRoom room)
        {
            var seen = new HashSet<Vector3Int>();
            var world = ServerContext.WorldBlockDatastore;
            var count = 0;
            foreach (var cell in room.Cells)
            foreach (var n in SixNeighbors(cell))
            {
                if (room.Contains(n) || seen.Contains(n)) continue;
                if (!world.TryGetBlock(n, out var block)) continue;
                if (!block.TryGetComponent<ICleanRoomBoundaryComponent>(out var boundary)) continue;
                seen.Add(n);
                if (boundary.BoundaryKind != CleanRoomBoundaryKind.Wall) count++;
            }
            return count;
        }

        // 部屋内（占有セルが部屋に属する）の稼働中製造機の台数。
        // Number of running machines whose occupied cells belong to the room.
        int CountRunningMachines(CleanRoom room)
        {
            var world = ServerContext.WorldBlockDatastore;
            var counted = new HashSet<BlockInstanceId>();
            var count = 0;
            foreach (var cell in room.Cells)
            {
                if (!world.TryGetBlock(cell, out var block)) continue;
                if (counted.Contains(block.BlockInstanceId)) continue;
                counted.Add(block.BlockInstanceId);
                if (IsRunningMachine(block)) count++;
            }
            return count;
        }

        bool IsRunningMachine(IBlock block)
        {
            // フェーズ4で機械側に「稼働中か」を公開予定。現状は Processing 状態を見る。
            // Phase 4 will expose a running flag on the machine; for now inspect Processing state.
            if (!block.TryGetComponent<Game.Block.Blocks.Machine.VanillaMachineProcessorComponent>(out var proc)) return false;
            return proc.CurrentState == Game.Block.Interface.State.ProcessState.Processing;
        }

        IEnumerable<Vector3Int> SixNeighbors(Vector3Int p)
        {
            yield return new Vector3Int(p.x + 1, p.y, p.z);
            yield return new Vector3Int(p.x - 1, p.y, p.z);
            yield return new Vector3Int(p.x, p.y + 1, p.z);
            yield return new Vector3Int(p.x, p.y - 1, p.z);
            yield return new Vector3Int(p.x, p.y, p.z + 1);
            yield return new Vector3Int(p.x, p.y, p.z - 1);
        }

        #endregion
    }
}
```

> 確認: `CleanRoomPollutionInput` の実メンバ（メソッド名・引数）はフェーズ2の `Game.CleanRoom/Purity/CleanRoomPollutionInput.cs` を開いて一致させる（`GetATotal(CleanRoom)` か別名か、抽象クラスかインターフェースか）。`world.TryGetBlock`/`block.TryGetComponent<T>`/`block.BlockInstanceId`/`CleanRoom.Cells`/`Contains`/`Volume`/`SurfaceArea` は `Game.CleanRoom/CleanRoom.cs` と `IWorldBlockDatastore`/`IBlock` で確認。`Game.CleanRoom.asmdef` が `Game.Block`（実装側、`VanillaMachineProcessorComponent` 参照のため）を参照する必要がある。参照していなければ追加するか、稼働判定を `IBlockStateObservable` 等のインターフェース経由へ変える（フェーズ4で `ICleanRoomMachineGate` 的な疎結合へ寄せる前提）。

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ComputeATotal_ReferenceRoom_Is16"`
Expected: PASS。型未検出なら Unity 再起動。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPollutionCalculator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPollutionTest.cs
git commit -m "feat(cleanroom): A_totalを算出するCleanRoomPollutionCalculatorと純関数ComputeATotalを追加"
```

---

## Task 9: 清浄機を CleanRoomPurityService へ配線し、基準部屋で平衡＝クラスA を統合テスト

`CleanRoomPurityService` を改修し、毎tick各部屋について (a) `CleanRoomPollutionCalculator.GetATotal` で A_total、(b) 部屋内清浄機を `TryGetRoomContainingBlock` ＋セル走査で動的収集して `Σ RemovalVolumePerSecond = n·q`、(c) 各清浄機へ除去寄与を `ApplyRemovedImpurity` で配分（フィルター摩耗）。統合テストはポール＋無限発電機で清浄機と部屋内機械を給電し（`IElectricConsumer` 自動配線の検証）、平衡で `C_eq≈3.2`・クラスA を固定する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityService.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPollutionTest.cs`

### サービス改修の要点（フェーズ2の OnTick に差し込む）

各部屋 room について毎tick:
1. `A_total = pollution.GetATotal(room)`。
2. 清浄機収集: `room.Cells` を走査し `ICleanRoomPurificationSource` を持つブロックを集める（`TryGetRoomContainingBlock` で所属確認。重複は `BlockInstanceId` で排除）。`nq = Σ src.RemovalVolumePerSecond`。
3. `C = state.Concentration(V)`、`removedTotal = min(nq·C, N/secondsPerTick) · secondsPerTick`（N をマイナスにしない）。
4. `dN = (A_total·secondsPerTick) − removedTotal` を `state.AddImpurity/RemoveImpurity`。
5. **除去寄与の配分**: 各清浄機へ `src.ApplyRemovedImpurity(removedTotal · src.RemovalVolumePerSecond / nq)`（nq>0 のとき）。これでフィルターが汚染レートに比例して摩耗。
6. クラス/段階判定はフェーズ2の既存ロジック（`C ≤ classThreshold` かつ `ACH=nq/V ≥ requiredAirChangeRate`、ヒステリシス）。

> 注入の入れ替え（調整事項#2）: フェーズ2コンストラクタが `IReadOnlyList<ICleanRoomPurificationSource>` を取る形なら、本タスクで「毎tick動的収集」へ差し替える（静的注入は捨てる）。`CleanRoomPollutionInput` はDI注入のまま（`CleanRoomPollutionCalculator` を DI 登録）。

- [ ] **Step 1: DI登録を追加（必要時）**

`Server.Boot/MoorestechServerDIContainerGenerator.cs` で `CleanRoomPollutionInput` の実装として `CleanRoomPollutionCalculator` を登録（フェーズ2が抽象だけ登録している場合）。`CleanRoomPurityService` が `CleanRoomPollutionInput` と `CleanRoomDetectionSystem` を受ける形を確認。

- [ ] **Step 2: 失敗する統合テストを書く（基準部屋・ポール給電・平衡でクラスA）**

`CleanRoomPollutionTest.cs` に追加。基準部屋は内寸 5×5×3=75 の空洞を壁で囲い、機械1台＋清浄機1台を**内部に**置く（占有セルは空気として V に算入＝V=75 維持。調整事項#3）。電柱1本＋無限発電機を部屋外（または貫通しない位置）に置き、`ConnectMachineToElectricSegment` で清浄機・機械の両方へ給電させる:

```csharp
        [Test]
        public void Purifier_PoweredInSealedReferenceRoom_EquilibratesToClassA()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var purityService = serviceProvider.GetService<Game.CleanRoom.Purity.CleanRoomPurityService>();

            // 内寸 5x5x3 の空洞を壁で囲う(外殻)。接続点はハッチ1+パイプコネクタ1(ドアは検出曖昧のため不使用)。
            // Seal a 5x5x3 cavity with walls; connectors = 1 hatch + 1 pipe-connector (door avoided: ambiguous sealing).
            BuildReferenceRoom(world); // V=75, S=110, 接続点2 になるよう壁/ハッチ/パイプコネクタを配置

            // 部屋内に機械1台と清浄機1台を置く(占有セルは空気としてVに算入)。
            // Place 1 machine + 1 purifier inside (occupied cells still count as air toward V).
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, InnerMachinePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomAirPurifierId, InnerPurifierPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var purifier);

            // 機械を Processing し続けさせる(A_machine=2.0 を成立させる)。長いレシピ+十分な入力。
            // Keep the machine Processing so A_machine=2.0 holds: long recipe + ample inputs.
            FeedMachineForLongRun(machine);

            // 清浄機に満タンのフィルターを入れる(5000上限で枯渇しないよう多めに)。
            // Load plenty of filters so the 5000-cap doesn't deplete mid-run.
            var filterInv = GetFilterInventory(purifier);
            filterInv.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 100));

            // 電柱1本+無限発電機で清浄機・機械を自動給電(IElectricConsumer 自動配線の検証)。
            // One pole + infinite generator auto-powers purifier & machine (verifies IElectricConsumer auto-wiring).
            world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, PolePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.InfinityGeneratorId, GeneratorPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            // τ=V/(nq)=75/5=15s。平衡まで十分tickを回す(1500 tick=75s ≈ 5τ)。
            // τ=15s; tick well past equilibrium (1500 ticks = 75s ≈ 5τ).
            for (var i = 0; i < 1500; i++) GameUpdater.UpdateOneTick();

            // 部屋取得は2段: ブロック→室(TryGetRoomContainingBlock)→純度状態(TryGetState)。
            // Room lookup is two steps: block→room→purity state. The service takes a CleanRoom, not a block.
            var detection = serviceProvider.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();
            Assert.IsTrue(detection.TryGetRoomContainingBlock(world.GetBlock(InnerMachinePos), out var room), "room exists");
            Assert.AreEqual(75, room.Volume, "V=75");
            Assert.AreEqual(110, room.SurfaceArea, "S=110");
            Assert.IsTrue(purityService.TryGetState(room, out var state), "room purity state exists");
            var c = state.Concentration(room.Volume);

            // C_eq = 16/5 = 3.2。クラスA(≤10)かつ ACH=5/75=0.067≥0.017。
            // C_eq = 3.2; class A (≤10) with ACH satisfied.
            Assert.AreEqual(3.2, c, 0.3, "equilibrium concentration ~3.2");
            Assert.AreEqual(Game.CleanRoom.Purity.CleanRoomClass.A, state.CurrentClass);
        }
```

> ヘルパ（`BuildReferenceRoom`/`InnerMachinePos`/`InnerPurifierPos`/`PolePos`/`GeneratorPos`/`FeedMachineForLongRun`/`GetFilterInventory`）は実コードに合わせて埋める。**`TryGetState` は名前だけでなく形が違う**: フェーズ2 `CleanRoomPurityService.TryGetState(CleanRoom room, out CleanRoomPurityState state)` は**部屋**を取る。テストはブロックから `detection.TryGetRoomContainingBlock(block, out room)` で室を引いてから `service.TryGetState(room, out state)` を呼ぶ2段にする（上記コード参照）。部屋形状（接続点をちょうど2にする壁/ハッチ/ドア配置・S=110 になる内寸）は `CleanRoomDetector` の S 定義（部屋セル面が境界に接する数）と整合させ、必要なら検出結果を一度アサートして V=75/S=110 を確認してからクラスを見る。ポール/発電機の接続レンジ（`MaxElectricPoleMachineConnectionRange`）内に清浄機・機械が入る座標を選ぶ（`DisconnectElectricSegmentTest` の配置レンジを参考）。

- [ ] **Step 3: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Purifier_PoweredInSealedReferenceRoom_EquilibratesToClassA"`
Expected: FAIL（サービス未配線で nq=0 → C 発散、またはクラスがA以外）。

- [ ] **Step 4: CleanRoomPurityService を改修（清浄機収集＋除去配分）**

フェーズ2の `OnTick` 内、各部屋ループに上記「要点 1〜5」を実装する。清浄機収集と配分の中核:

```csharp
        // 部屋内の清浄機を集めて n·q を得る。重複は BlockInstanceId で排除。
        // Collect in-room purifiers to get n·q; dedupe by BlockInstanceId.
        private List<ICleanRoomPurificationSource> CollectPurifiers(CleanRoom room)
        {
            var world = ServerContext.WorldBlockDatastore;
            var seen = new HashSet<BlockInstanceId>();
            var result = new List<ICleanRoomPurificationSource>();
            foreach (var cell in room.Cells)
            {
                if (!world.TryGetBlock(cell, out var block)) continue;
                if (!seen.Add(block.BlockInstanceId)) continue;
                if (!block.TryGetComponent<ICleanRoomPurificationSource>(out var src)) continue;

                // 所属確認: その清浄機の室がこの room であること。
                // Confirm the purifier's room is this room.
                if (_detection.TryGetRoomContainingBlock(block, out var owner) && owner.Id == room.Id)
                    result.Add(src);
            }
            return result;
        }
```

そして OnTick の積分（フェーズ2の dN 計算箇所）を:

```csharp
            const double secondsPerTick = 0.05;
            var aTotal = _pollution.GetATotal(room);

            var purifiers = CollectPurifiers(room);
            double nq = 0;
            foreach (var p in purifiers) nq += p.RemovalVolumePerSecond;

            var c = state.Concentration(room.Volume);
            // 除去はNを負にしない範囲で。
            // Removal cannot drive N below zero.
            var removeRate = nq * c;
            var removedTotal = removeRate * secondsPerTick;
            if (removedTotal > state.ImpurityCount) removedTotal = state.ImpurityCount;

            state.AddImpurity(aTotal * secondsPerTick);
            state.RemoveImpurity(removedTotal);

            // 除去寄与をフィルターへ配分(汚染レート比例の摩耗)。
            // Distribute removed impurity to filters (wear proportional to pollution rate).
            if (nq > 0 && removedTotal > 0)
                foreach (var p in purifiers)
                    p.ApplyRemovedImpurity(removedTotal * (p.RemovalVolumePerSecond / nq));
```

> 確認: フェーズ2 `CleanRoomPurityState` の `AddImpurity`/`RemoveImpurity`/`ImpurityCount`/`Concentration` と、`CleanRoomDetectionSystem.TryGetRoomContainingBlock` のシグネチャに一致させる。`_pollution`（`CleanRoomPollutionInput`）と `_detection` はフェーズ2のフィールドを再利用。`secondsPerTick=0.05` は `GameUpdater` の定数（`TicksToSeconds(1)` 等）があればそれを使う。

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Purifier_PoweredInSealedReferenceRoom_EquilibratesToClassA"`
Expected: PASS。落ちる場合は (a) 検出 V/S が 75/110 か、(b) 給電が届いているか（清浄機 `RemovalVolumePerSecond>0`）、(c) 機械が Processing 継続か、(d) tick 数が 5τ 以上か、を切り分ける。型未検出なら Unity 再起動。

- [ ] **Step 6: CleanRoom 全体回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: フェーズ1〜3の全テストPASS。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPurityService.cs moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPollutionTest.cs
git commit -m "feat(cleanroom): 清浄機をCleanRoomPurityServiceへ配線し基準部屋でクラスA平衡を検証"
```

---

## Self-Review

### コードマップ §3 の網羅

| §3 の項目 | 対応タスク |
|---|---|
| blocks.yml `CleanRoomAirPurifier`＋param、`_CompileRequester` トリガ、テストmod | Task 1 |
| `AirPurifierProcessorComponent`（`IUpdatableBlockComponent, ICleanRoomPurificationSource`、RequestPower/SupplyPower/RemovalVolumePerSecond/Update） | Task 4 |
| `AirPurifierElectricComponent`（`IElectricConsumer`、RequestEnergy/SupplyEnergy） | Task 5 |
| `AirPurifierFilterInventory`（仕事量ベース消費） | Task 3 |
| `AirPurifierSaveComponent`（`IBlockSaveState`、フィルター残＋進捗） | Task 6 |
| `VanillaAirPurifierTemplate`（New/Load）＋ `VanillaIBlockTemplates` 登録 | Task 7 |
| `CleanRoomPollutionCalculator`（`CleanRoomPollutionInput` 実装、V/S/接続点/機械） | Task 8 |
| `CleanRoomPurityService` へ清浄機配線（`TryGetRoomContainingBlock` で n·q 集計） | Task 9 |
| 電力割合換算（`MachineCurrentPowerToSubSecond` 流儀）／自動電力接続（`ConnectMachineToElectricSegment`） | Task 4（割合）/ Task 9（自動接続を統合テストで検証） |

### 設計書 §5/§6 の網羅

- §5 汚染源: `a_volume·V`/`a_surface·S`/`a_connector·接続点`/`A_machine·稼働機械` を Task 8 で実供給。`A_hatch`（レート換算）/`A_door`（バースト）は `ComputeATotal` の引数に口だけ用意し0スタブ（§5「レート換算」の意図を将来差せる形に）。フェーズ5で計量フックを実装。
- §6 空気清浄機＋フィルター: 電力消費（Task 5）、仕事量ベースのフィルター消費＝除去量比例（Task 3＋Task 9の配分）、複数台で n·q 加算（Task 9 の `Σ RemovalVolumePerSecond`）、フィルター切れで除去0（Task 4）。汚い部屋ほどフィルターを食う＝平衡時消費が A_total 比例（Task 9 の配分が removedTotal を全台へ按分するため成立）。高級フィルター種別は後追い（§6 の「後から足せる」に従い未実装）。

### プレースホルダ・スキャン

- 本文に `TODO`/未確定の擬似値は残していない。`...`（省略記法）は Task 1 の `inventoryConnectors` スキーマ／blocks.json／items.json で「既存エントリをコピー」と明記した箇所のみ＝コピー元が実在する。
- 数値は全て balance-parameters の値（q=5.0/requestPower=100/filterCapacity=5000/a_volume=0.10/a_surface=0.05/a_connector=0.50/A_machine=2.0/k_hatch=0.30/burst_door=15/secondsPerTick=0.05）。worked example の 16.0 と 3.2 をテストで固定。

### 型整合（コードマップ契約との一致）

- 型名は契約どおり: `CleanRoomPurityService`/`CleanRoomPollutionInput`/`ICleanRoomPurificationSource`/`CleanRoomPollutionCalculator`/`AirPurifierProcessorComponent`/`AirPurifierElectricComponent`/`AirPurifierFilterInventory`/`AirPurifierSaveComponent`/`VanillaAirPurifierTemplate`。
- `IElectricConsumer`/`ElectricPower`/`IBlockSaveState`/`IUpdatableBlockComponent`/`IBlockComponent`/`IBlockTemplate` の実シグネチャは本プラン作成時に実ファイルで確認済み（`SupplyEnergy(ElectricPower)`、`RequestEnergy`、`ApplyRemovedImpurity` は新規追加メンバ）。

### ⚠ 人間が着手前に確定すべきクロスフェーズ事項（再掲）

1. **`ICleanRoomPurificationSource` の所在**: コードマップは `Game.CleanRoom` 配下に置くが、本プランは asmdef 依存方向のため `Game.Block.Interface/Component` へ移設し、`ApplyRemovedImpurity(double)` を追加する（フェーズ2の署名変更）。
2. **`CleanRoomPurityService` の清浄機注入**: コードマップのコンストラクタ `IReadOnlyList<ICleanRoomPurificationSource>` 静的注入では実行時設置ブロックが集まらない。本プランは毎tick `TryGetRoomContainingBlock`＋セル走査で動的収集に差し替える（コンストラクタ署名要見直し）。
3. **A_total=16 の前提**: balance §5「占有セルを V に算入しない」と検出器実装（境界コンポーネントのみ除外＝機械セルは空気）が食い違う。worked example は**検出器に整合**。本プランは占有セル除外を実装しない。Task 9 の部屋構築は機械・清浄機を内部に置いても V=75 のままになることを前提とする。
4. **`Game.CleanRoom.asmdef` の `Game.Block` 参照**: Task 8 の稼働機械判定が `VanillaMachineProcessorComponent`（`Game.Block` 実装側）を直参照する。実装側参照を避けたい場合は、機械の稼働状態をインターフェース（例 `IBlockStateObservable` 由来 or フェーズ4の `ICleanRoomMachineGate`）経由に寄せる選択肢を Task 8 着手時に確定する。
5. **ドアの flood-fill 上の扱い**: フェーズ1プラン Task5「`ICleanRoomBoundaryComponent` 持ち＝密閉面（ドアも密閉）」とコードマップ§5「ドアは貫通可能境界」が食い違う。本プランの統合テスト（Task 9）はドアを避け**ハッチ＋パイプコネクタ**で接続点2を作るため keystone テストはこの未確定に依存しない。ただしドアが密閉/貫通どちらか（＝ドアで囲った部屋が成立するか）はフェーズ5前に確定が必要。
