# クリーンルーム フェーズ5（I/O境界ブロック挙動 ＋ 永続化仕上げ）実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **moorestech 固有の必須ルール:**
> - `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認する。
> - テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"` で実行（クライアントプロジェクトからサーバーテストも走る）。
> - 新規サーバー `.cs`／新規 blockType／コネクタスキーマ追加を認識させるには Unity の **再起動**が要る場合がある（Refresh では不足）。「型が見つからない」「BlockType が無い」で失敗したら uloop で Unity を再起動してから再試行。各チェックポイントに再起動の注記を置く。
> - blockType／コネクタパラメータのスキーマ追加・SourceGenerator の手順は `edit-schema` スキルに従う。テスト作成は `creating-server-tests` スキルに従う。
> - 非ASCIIファイル編集時は AGENTS.md の「文字化け防止ワークフロー」を順守。`.cs` は UTF-16 LE BOM が多い。`縺`/`繧`/`繝` が連続したら破棄して読み直す。
> - **APIシグネチャ確認の原則:** 本プランのコードは実コードベース（`VanillaBeltConveyorBlockInventoryInserter` / `FluidPipeComponent` / `FuelGearGeneratorItemComponent` / `BlockConnectorComponent` 等）を読んで書いているが、メソッド名・名前空間・引数順に推定が残りうる。各 `.cs` を書く前に、本文で「確認」と指示した既存ファイルを開いて実シグネチャに合わせること。コンパイル/テストのチェックポイントが安全網。

**Goal:** クリーンルーム境界3種の搬送挙動を実装する。アイテムハッチ（`CleanRoomItemHatchComponent`）は壁を貫通して外↔内のインベントリを中継し搬送レート `RecentThroughputPerSecond` を公開する。パイプコネクタ（`CleanRoomPipeConnectorComponent`）は壁を貫通して流体を中継する。ドア（`CleanRoomDoorComponent`）はプレイヤー通過イベントを受けて `A_door` バーストを計上する。これらの計量を `CleanRoomPollutionCalculator`（フェーズ3）へ配線し（`A_hatch` レート＋`A_door` バースト）、I/O 固有状態を各ブロックの `IBlockSaveState` で round-trip させてフェーズ2導入の永続化を完結させる。

**Architecture:** 3種の I/O ブロックは、フェーズ1の境界テンプレート `VanillaCleanRoomBoundaryTemplate` の各 `CleanRoomBoundaryKind` 分岐に **挙動コンポーネントを合成**して作る。各ブロックには (a) フェーズ1の汎用 `CleanRoomBoundaryComponent(kind)` マーカー（密閉境界として flood-fill 検出に見える）と (b) フェーズ5の挙動コンポーネント（＋ハッチ/パイプはコネクタ）が **両方**付く。マーカーが「密閉」を担い、挙動コンポーネントが「離散イベントでの物の出入り」を担う。ハッチは `VanillaBeltConveyorBlockInventoryInserter` のレイ機構（`BlockConnectorComponent<IBlockInventory>.ConnectedTargets` → `InsertItemContext` → `target.InsertItem`）を踏襲して毎tick中継しレート窓を更新する。パイプコネクタは `FluidPipeComponent`（`FluidContainer` ＋ `BlockConnectorComponent<IFluidInventory>` ＋ push型 `Update`）を踏襲する。ドアは離散メソッド `NotifyPlayerPassage()` でバーストを溜め、計算側が tick で消費してリセットする。永続化は各ブロックの `IBlockSaveState`（中継中アイテム/流体）で行い、グローバルセーブスキーマ（`WorldSaveAllInfoV1` 等）には触れない。

**Tech Stack:** C# (Unity, moorestech_server), R3/UniRx, NUnit (Server.Tests), Mooresmaster Source Generator (blocks.yml / inventoryConnects / fluidInventoryConnects → 自動生成モジュール)。

---

## 0. 前提・依存と「2つの仕様矛盾の確定的解消」

### 0.1 依存フェーズ（本プランの土台。**現ブランチ未マージ前提**）

本プランは **フェーズ1〜4の成果物が存在する前提**で書く。フェーズ1（境界ブロック＋3D密閉検出）／フェーズ2（純度シミュ＋永続化3点改修）／フェーズ3（清浄機＋`CleanRoomPollutionCalculator`）／フェーズ4（製造機統合）。これらは別ブランチ（`feature/cleanroom-design` の codemap／phase1 プラン）で定義されており、**本プランの `uloop` コマンドはフェーズ1〜4がマージ済みであることを前提とする**。コードマップ §5（フェーズ5）が本プランの仕様、§1 が横断判断。フェーズ1〜4の契約名（`CleanRoomBoundaryComponent` / `CleanRoomBoundaryKind` / `VanillaCleanRoomBoundaryTemplate` / `CleanRoomPollutionCalculator` / `CleanRoomDetectionSystem` / `CleanRoomPurityService` / `CleanRoomPuritySaveData`）はそのまま使う。NEW コードは本プランで読んだ実 API（belt/fluid/save 等）に接地している。

**確定数値（`2026-06-06-cleanroom-balance-parameters.md` §2／§6 が唯一のソース）:**
- `k_hatch = 0.30`（`A_hatch = k_hatch · RecentThroughputPerSecond[個/秒]`）
- `burst_door = 15`（プレイヤー1通過あたりの瞬間加算、多重通過は合算）
- `a_connector = 0.50`（接続点1個あたりの恒常項。ハッチ/ドア/パイプコネクタ数に比例）
- レート窓長 `HatchRateWindowTicks = 20`（= 1.0秒、20tick。`RecentThroughputPerSecond = (直近20tickで中継した個数) / (20 · GameUpdater.SecondsPerTick)`）

### 0.2 仕様矛盾の解消①：ドアの「空気密閉」セマンティクス（フェーズ1と整合）

設計書 §7 の「ドアは貫通してよい」は **「ドアは正当な密閉境界である（隙間/リークとしてカウントしない）」** の意味であり、**「開いた空セルとして空気が流れる」意味ではない**。4種すべて（Wall / Door / ItemHatch / PipeConnector）は **壁と同一の air-sealing flood-fill 境界**であり、閉じたドアは空気を漏らさない。アイテム/流体/プレイヤーは **コンポーネントの離散ロジックと離散イベント**でのみ越える（空セル経由では決して越えない）。

→ したがってフェーズ1の「ドアは部屋を密閉する」は **正しいまま**。本プランで各 I/O ブロックに付く `CleanRoomBoundaryComponent(kind)` マーカーが密閉を担う。ドアのプレイヤー通過は **離散イベント**で `A_door` バーストを足すだけで、密閉性には影響しない。フェーズ1との矛盾は無い。

### 0.3 仕様矛盾の解消②：占有セルと V（再litigationしない）

バランス §5: 機械/清浄機等の**占有セルは V に算入しない**。フェーズ3 worked example の V=75 は**空セルだけの基準部屋**。本プランの I/O 境界ブロック（ハッチ/パイプ/ドア）は **境界（boundary）であって内部（interior）ではない**ので、内部 V には影響しない。これらは **`a_connector · 接続点数`** の項として `A_total` に寄与する（占有セルとしてではない）。本プランで V の定義は変更しない。

### 0.4 合成設計（最上位の前提として明記）

codemap §5 のシグネチャどおり、`CleanRoomItemHatchComponent` / `CleanRoomPipeConnectorComponent` / `CleanRoomDoorComponent` は **`ICleanRoomBoundaryComponent` を実装しない**（それぞれ `IBlockInventory` / `IFluidInventory` / 独自）。よってテンプレートは各 kind について **2つ（以上）** のコンポーネントを付ける:

| kind | 付与コンポーネント |
|---|---|
| `Wall` | `CleanRoomBoundaryComponent(Wall)` のみ（フェーズ1のまま） |
| `Door` | `CleanRoomBoundaryComponent(Door)` ＋ `CleanRoomDoorComponent` |
| `ItemHatch` | `CleanRoomBoundaryComponent(ItemHatch)` ＋ `CleanRoomItemHatchComponent` ＋ `BlockConnectorComponent<IBlockInventory>` |
| `PipeConnector` | `CleanRoomBoundaryComponent(PipeConnector)` ＋ `CleanRoomPipeConnectorComponent` ＋ `BlockConnectorComponent<IFluidInventory>`（`IFluidInventory.CreateFluidInventoryConnector` で生成） |

マーカー＝密閉、挙動コンポーネント＝離散搬送。これが 0.2 のドア整合の実装上の担保。

### 0.5 コネクタ面のジオメトリ（中継テストの load-bearing 仕様）

ハッチ/パイプは「外から受けて内へ流す」中継。**接続面の入出力方向を具体的に固定する**:
- ハッチ: `inputConnects` = 外側面（例 −X 側 `[-1,0,0]`）、`outputConnects` = 内側面（例 +X 側 `[1,0,0]`）。外のソースが `InsertItem` でハッチへ入れ、ハッチが `outputConnects` で接続された室内インベントリへ `InsertItem` する。
- パイプ: `inflowConnects` = 外側面、`outflowConnects` = 内側面。テストでは外パイプ→コネクタ→内タンク/パイプの直線配置にする。

テストは壁シェルの1面をハッチ/パイプに置換し、外側に隣接1セルへソース、内側に隣接1セル（室内）へターゲットを置く。面が噛み合わないとコネクタが繋がらずアサートが空振りするので、**配置座標と向きをテスト本文で具体値にする**。

---

## File Structure（フェーズ5で作成/変更するファイル）

**スキーマ／テスト用 mod（コネクタパラメータ追加）**
- Modify: `VanillaSchema/blocks.yml` — フェーズ1で追加済みの `CleanRoomItemHatch` 分岐に `inventoryConnectors`（ref: inventoryConnects）、`CleanRoomPipeConnector` 分岐に `fluidInventoryConnectors`（ref: fluidInventoryConnects）を追加
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ
- Modify: テスト用 mod `.../ForUnitTest/mods/forUnitTest/master/blocks.json` — `TestCleanRoomItemHatch`/`TestCleanRoomPipeConnector` に `inventoryConnectors`/`fluidInventoryConnectors` の入出力面を付与

**I/O 挙動コンポーネント（Game.Block）**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomItemHatchComponent.cs` — `IBlockInventory, IUpdatableBlockComponent, IBlockSaveState`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomPipeConnectorComponent.cs` — `IFluidInventory, IUpdatableBlockComponent, IBlockSaveState`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomDoorComponent.cs` — プレイヤー通過バースト計上
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs` — kind 別に挙動コンポーネント＋コネクタを合成（New/Load）

**汚染計量の配線（Game.CleanRoom）**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPollutionCalculator.cs` — `A_hatch`（ハッチレート集計）＋`A_door`（ドアバースト消費）を取り込む

**テスト**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`

> 各 `.cs` 新規ファイルは Unity が `.meta` を自動生成する。`.meta` は手動作成禁止。

---

## Task 1: ハッチ/パイプのコネクタパラメータをスキーマに追加

フェーズ1では境界4種を param 無しで作った。フェーズ5でハッチに inventory コネクタ、パイプコネクタに fluid コネクタのパラメータを足す。`edit-schema` スキルの手順に従う。

**Files:**
- Modify: `VanillaSchema/blocks.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`

- [ ] **Step 1: blocks.yml の該当 blockType 分岐にコネクタ param を追加**

まず `VanillaSchema/blocks.yml` の既存 `FluidPipe` / `BeltConveyor` 分岐を読み、`inventoryConnectors`/`fluidInventoryConnectors` の書式（`ref: inventoryConnects` / `ref: fluidInventoryConnects` と `implementationInterface: - IInventoryConnectors`）を確認する。フェーズ1で追加した `CleanRoomItemHatch` / `CleanRoomPipeConnector` の `when:` 分岐を、空オブジェクトから次へ拡張:

```yaml
      - when: CleanRoomItemHatch
        type: object
        implementationInterface:
        - IInventoryConnectors
        properties:
        - key: inventoryConnectors
          ref: inventoryConnects
      - when: CleanRoomPipeConnector
        type: object
        properties:
        - key: fluidInventoryConnectors
          ref: fluidInventoryConnects
```

> `CleanRoomDoor` / `CleanRoomWall` は param 無しのまま。`IInventoryConnectors` を付ける書式は既存 `Chest` 分岐（`inventoryConnects` 使用）に正確に合わせる。

- [ ] **Step 2: SourceGenerator をトリガ**

`Core.Master/_CompileRequester.cs` の `dummyText` 定数を変更:

```csharp
private const string dummyText = "regenerate-cleanroom-phase5-io-connectors";
```

- [ ] **Step 3: 再生成を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。`Mooresmaster.Model.BlocksModule.CleanRoomItemHatchBlockParam.InventoryConnectors`（型 `InventoryConnects`）、`CleanRoomPipeConnectorBlockParam.FluidInventoryConnectors`（型 `FluidInventoryConnects`）が生成される（Task 3/4 で参照確認）。

> 「Domain Reload in progress」なら45秒待って再試行。型未検出なら Unity 再起動。

- [ ] **Step 4: Commit**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "feat(cleanroom): ハッチ/パイプ境界にI/Oコネクタparamをスキーマ追加"
```

---

## Task 2: テスト用 mod にコネクタ面を付与

NUnit テストは `ForUnitTest` mod のマスタを使う。フェーズ1で追加済みの `TestCleanRoomItemHatch` / `TestCleanRoomPipeConnector` に入出力コネクタ面を付ける。中継方向（外→内）を固定するため、入力面と出力面を反対向きに置く。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`

- [ ] **Step 1: 既存のコネクタ面 JSON 書式を確認**

`.../master/blocks.json` で既存 `BeltConveyor` / `FluidPipe` ブロックの `inventoryConnectors` / `fluidInventoryConnectors` がどう書かれているか（`inputConnects`/`outputConnects` または `inflowConnects`/`outflowConnects` の配列形式、各要素の `directions`/`offset`/`connectType`）を読む。**そのキー名・構造をそのまま踏襲する**。

- [ ] **Step 2: ハッチに入出力面を付与**

`TestCleanRoomItemHatch` の `blockParam` に、外側（−X）入力・内側（+X）出力のコネクタを追加。下は実テスト mod（`TestElectricMachine` の `inventoryConnectors`）の実書式に忠実な完全版。`connectorGuid` は既存と衝突しない新規GUIDを割り当てる:

```json
"blockParam": {
  "inventoryConnectors": {
    "inputConnects": [
      {
        "offset": [0, 0, 0],
        "connectType": "Inventory",
        "directions": [ [-1, 0, 0] ],
        "connectOption": { "inventoryOptions": [] },
        "connectorGuid": "<新規GUID-HatchInput>"
      }
    ],
    "outputConnects": [
      {
        "offset": [0, 0, 0],
        "connectType": "Inventory",
        "directions": [ [1, 0, 0] ],
        "connectOption": { "inventoryOptions": [] },
        "connectorGuid": "<新規GUID-HatchOutput>"
      }
    ]
  }
}
```

- [ ] **Step 3: パイプコネクタに inflow/outflow 面を付与**

`TestCleanRoomPipeConnector` の `blockParam` に、外側（−X）inflow・内側（+X）outflow を追加。下は実テスト mod（`TestElectricGenerator` の `fluidInventoryConnectors`）の実書式に忠実な完全版。**`connectOption.flowCapacity` を必ず含めること**（欠落すると `CleanRoomPipeConnectorComponent.GetMaxFlowRate` が 0 を返し、室内へ流れず中継テストが空振りする）:

```json
"blockParam": {
  "fluidInventoryConnectors": {
    "inflowConnects": [
      {
        "connectType": "Fluid",
        "offset": [0, 0, 0],
        "directions": [ [-1, 0, 0] ],
        "connectOption": { "flowCapacity": 100, "connectTankIndex": 0 },
        "connectorGuid": "<新規GUID-PipeInflow>"
      }
    ],
    "outflowConnects": [
      {
        "connectType": "Fluid",
        "offset": [0, 0, 0],
        "directions": [ [1, 0, 0] ],
        "connectOption": { "flowCapacity": 100, "connectTankIndex": 0 },
        "connectorGuid": "<新規GUID-PipeOutflow>"
      }
    ]
  }
}
```

> パイプは内外双方向に流れて欲しいケースもあるが、本テストは外→内の単方向で「室内へ届く」ことを検証するため inflow=外/outflow=内に固定する。`flowCapacity=100` は既存 FluidPipe／`TestElectricGenerator` と同値。`connectorGuid` は新規GUID。

- [ ] **Step 4: コンパイル（マスタ読み込み確認）**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json
git commit -m "test(cleanroom): テスト用ハッチ/パイプにI/Oコネクタ面を付与"
```

---

## Task 3: アイテムハッチ — 壁貫通中継 ＋ レート窓 ＋ セーブ

`CleanRoomItemHatchComponent` は `IBlockInventory` として外のベルト/機械から `InsertItem` を受け、内部バッファに保持し、`Update` で `BlockConnectorComponent<IBlockInventory>.ConnectedTargets`（= 室内インベントリ）へ中継する。中継した個数をレート窓に記録し `RecentThroughputPerSecond` を公開する。中継待ちアイテムは `IBlockSaveState` で保存する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomItemHatchComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`

- [ ] **Step 1: 失敗テスト（中継到達＋レート）を書く**

`Tests/CombinedTest/Core/CleanRoomIoTest.cs` を新規作成:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomIoTest
    {
        // ハッチが外のソースから受けたアイテムを室内ターゲットへ中継し、レートを公開する
        // Hatch relays an item from the outer source to the inner target and reports throughput
        [Test]
        public void ItemHatch_RelaysItemInwardAndReportsThroughput()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // ハッチを (0,0,0) に、外ソースを (-1,0,0)、内ターゲット(チェスト)を (1,0,0) に置く
            // Place hatch at (0,0,0), outer source at (-1,0,0), inner chest target at (1,0,0)
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatch, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchBlock);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(1, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var innerChest);

            Assert.True(hatchBlock.TryGetComponent<CleanRoomItemHatchComponent>(out var hatch));

            // 外のソース相当として、ハッチに直接 InsertItem する（ベルトの代役）
            // Insert directly into the hatch as a stand-in for the outer source (belt proxy)
            var item = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
            var remain = hatch.InsertItem(item, InsertItemContext.Empty);
            Assert.AreEqual(0, remain.Count, "Hatch accepts the item into its in-transit buffer");

            // 中継が完了するまで tick を回す
            // Tick until the relay completes
            GameUpdater.RunFrames(5);

            // 室内チェストにアイテムが届いている
            // The item has arrived in the inner chest
            Assert.True(innerChest.TryGetComponent<IBlockInventory>(out var chestInv));
            var arrived = Enumerable.Range(0, chestInv.GetSlotSize())
                .Sum(i => chestInv.GetItem(i).Count);
            Assert.AreEqual(1, arrived, "Relayed item reaches the inner inventory");

            // レート窓に搬送が反映されている（直近窓に1個 → >0）
            // Throughput window reflects the relay (1 item in the recent window → >0)
            Assert.Greater(hatch.RecentThroughputPerSecond, 0.0);
        }
    }
}
```

> `ForUnitTestModBlockId.ChestId` / `ForUnitTestItemId.ItemId1` / `MoorestechServerDIContainerOptions` / `IBlock.TryGetComponent<T>` の実名は `GearBeltConveyorTest.cs` と既存テストに合わせる。`InsertItemContext.Empty` は実在（本プランで確認済み）。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ItemHatch_RelaysItemInwardAndReportsThroughput"`
Expected: FAIL（`CleanRoomItemHatchComponent` 未定義）。

- [ ] **Step 3: CleanRoomItemHatchComponent を実装**

`Game.Block/Blocks/CleanRoom/CleanRoomItemHatchComponent.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // 壁貫通アイテムハッチ。外のソースから受け、室内インベントリへ毎tick中継し搬送レートを公開する
    // Wall-piercing item hatch: accepts from the outer source, relays to the inner inventory each tick, reports throughput
    public class CleanRoomItemHatchComponent : IBlockInventory, IUpdatableBlockComponent, IBlockSaveState
    {
        public string SaveKey => SaveKeyStatic;
        public static string SaveKeyStatic { get; } = typeof(CleanRoomItemHatchComponent).FullName;

        // レート窓長（tick）。1.0秒 = 20tick。RecentThroughputPerSecond の分母に使う
        // Rate-window length in ticks (1.0s = 20 ticks); denominator for RecentThroughputPerSecond
        public const int HatchRateWindowTicks = 20;

        // 直近の窓で中継した個数のリングバッファ（合計を分母窓秒で割る）
        // Ring buffer of relayed counts over the recent window (sum divided by window seconds)
        private readonly int[] _relayedPerTick = new int[HatchRateWindowTicks];
        private int _windowCursor;

        // 中継待ちアイテム（外から受けてまだ室内へ流していない分）
        // In-transit items received from outside but not yet pushed inward
        private readonly List<IItemStack> _inTransit = new();

        private readonly BlockInstanceId _blockInstanceId;
        private readonly BlockConnectorComponent<IBlockInventory> _connector;

        // 直近窓の合計搬送個数 / 窓秒。汚染計量 A_hatch = k_hatch · この値
        // Sum of relayed counts over the window / window seconds; pollution A_hatch = k_hatch * this
        public double RecentThroughputPerSecond
        {
            get
            {
                var sum = 0;
                for (var i = 0; i < _relayedPerTick.Length; i++) sum += _relayedPerTick[i];
                return sum / (HatchRateWindowTicks * GameUpdater.SecondsPerTick);
            }
        }

        public CleanRoomItemHatchComponent(BlockInstanceId blockInstanceId, BlockConnectorComponent<IBlockInventory> connector)
        {
            _blockInstanceId = blockInstanceId;
            _connector = connector;
        }

        // セーブからの復元: 中継中アイテムだけ戻す。レート窓は揮発（ロード後0から再充填）
        // Restore from save: only the in-transit items; the rate window is transient (refills from 0 after load)
        public CleanRoomItemHatchComponent(Dictionary<string, string> componentStates, BlockInstanceId blockInstanceId, BlockConnectorComponent<IBlockInventory> connector)
            : this(blockInstanceId, connector)
        {
            if (!componentStates.TryGetValue(SaveKey, out var raw)) return;
            var saved = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(raw);
            if (saved == null) return;
            foreach (var s in saved)
            {
                var stack = s?.ToItemStack();
                if (stack != null && stack.Count > 0) _inTransit.Add(stack);
            }
        }

        // 外のソースから受け取り、中継バッファへ積む（容量は無制限＝低スループット運用前提）
        // Accept from the outer source into the in-transit buffer (unbounded; low-throughput by design)
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            BlockException.CheckDestroy(this);
            if (itemStack == null || itemStack.Count == 0) return itemStack;
            _inTransit.Add(itemStack);
            return ServerContext.ItemStackFactory.CreatEmpty();
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return true; // 低スループット運用。常に受ける
        }

        // 毎tick: 中継待ちを室内ターゲットへ押し出し、押し出した個数をレート窓へ記録
        // Each tick: push in-transit items to inner targets and record the pushed count into the rate window
        public void Update()
        {
            BlockException.CheckDestroy(this);

            var relayedThisTick = AdvanceRelay();
            RecordRate(relayedThisTick);

            #region Internal

            // 中継待ちの各アイテムを接続先(室内)へ InsertItem。受け入れられた個数を返す
            // Push each in-transit item to a connected (inner) inventory; return accepted count
            int AdvanceRelay()
            {
                var targets = _connector.ConnectedTargets;
                if (targets.Count == 0) return 0;

                var relayed = 0;
                for (var idx = _inTransit.Count - 1; idx >= 0; idx--)
                {
                    var stack = _inTransit[idx];
                    var before = stack.Count;
                    var remain = InsertToAnyTarget(stack, targets);
                    relayed += before - remain.Count;
                    if (remain.Count == 0) _inTransit.RemoveAt(idx);
                    else _inTransit[idx] = remain;
                }
                return relayed;
            }

            IItemStack InsertToAnyTarget(IItemStack stack, IReadOnlyDictionary<IBlockInventory, ConnectedInfo> targets)
            {
                var current = stack;
                foreach (var target in targets)
                {
                    if (current.Count == 0) break;
                    var ctx = new InsertItemContext(_blockInstanceId, target.Value.SelfConnector, target.Value.TargetConnector);
                    current = target.Key.InsertItem(current, ctx);
                }
                return current;
            }

            // リングバッファの現在tick枠に今回の搬送数を入れ、カーソルを進める
            // Write this tick's relayed count into the ring slot and advance the cursor
            void RecordRate(int relayed)
            {
                _relayedPerTick[_windowCursor] = relayed;
                _windowCursor = (_windowCursor + 1) % HatchRateWindowTicks;
            }

            #endregion
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            return slot >= 0 && slot < _inTransit.Count ? _inTransit[slot] : ServerContext.ItemStackFactory.CreatEmpty();
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            while (_inTransit.Count <= slot) _inTransit.Add(ServerContext.ItemStackFactory.CreatEmpty());
            _inTransit[slot] = itemStack;
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _inTransit.Count;
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var serialized = _inTransit.Select(s => new ItemStackSaveJsonObject(s)).ToList();
            return JsonConvert.SerializeObject(serialized);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
```

> `ItemStackSaveJsonObject` / `ItemStackFactory.CreatEmpty()` / `BlockException.CheckDestroy` / `ConnectedInfo` / `BlockConnectorComponent<>` の実名・名前空間は `FuelGearGeneratorItemComponent.cs`・`VanillaBeltConveyorBlockInventoryInserter.cs`・`FluidPipeComponent.cs` を開いて一致させる。`GameUpdater.SecondsPerTick` は `FluidPipeComponent` で使用例あり（確認済み）。`CreatEmpty` はタイポ風だが実 API 名の可能性が高い—`ItemStackFactory` の実メソッド名を確認して合わせる。

- [ ] **Step 4: VanillaCleanRoomBoundaryTemplate を kind 別合成の完全版に更新**

ハッチのテストを緑にするにはテンプレートがハッチを合成する必要がある。ここで **全 kind 分岐を含む完全版**を一度に書く（パイプ/ドアの分岐も含めて作り、後続 Task 4/5 はこのテンプレを「存在する前提」で各コンポーネントの実装だけ進める）。

`Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs` をフェーズ1の版から次へ置き換える:

```csharp
using System.Collections.Generic;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    // 4種のクリーンルーム境界ブロック共通テンプレート。kind 別に密閉マーカー＋I/O挙動を合成
    // Shared template for the 4 boundary block types; composes the sealing marker + I/O behavior per kind
    public class VanillaCleanRoomBoundaryTemplate : IBlockTemplate
    {
        private readonly CleanRoomBoundaryKind _kind;

        public VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind kind)
        {
            _kind = kind;
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Build(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Build(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        // componentStates が null なら New、非nullなら Load。kind で合成内容を分岐する
        // null componentStates → New, non-null → Load; switch composition by kind
        private IBlock Build(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            // 全 kind 共通で密閉マーカーを付ける（flood-fill 検出が境界として見る）
            // Every kind carries the sealing marker (flood-fill detection sees it as a boundary)
            var components = new List<IBlockComponent>
            {
                new CleanRoomBoundaryComponent(_kind),
            };

            // kind 別に I/O 挙動コンポーネント（＋コネクタ）を合成する
            // Compose the I/O behavior component (+ connector) per kind
            switch (_kind)
            {
                case CleanRoomBoundaryKind.ItemHatch:
                    AddHatch();
                    break;
                case CleanRoomBoundaryKind.PipeConnector:
                    AddPipeConnector();
                    break;
                case CleanRoomBoundaryKind.Door:
                    components.Add(new CleanRoomDoorComponent());
                    break;
                case CleanRoomBoundaryKind.Wall:
                default:
                    break; // 壁はマーカーのみ
            }

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);

            #region Internal

            void AddHatch()
            {
                var param = (CleanRoomItemHatchBlockParam)blockMasterElement.BlockParam;
                var connector = new BlockConnectorComponent<IBlockInventory>(
                    param.InventoryConnectors.InputConnects,
                    param.InventoryConnectors.OutputConnects,
                    blockPositionInfo);
                var hatch = componentStates == null
                    ? new CleanRoomItemHatchComponent(blockInstanceId, connector)
                    : new CleanRoomItemHatchComponent(componentStates, blockInstanceId, connector);
                components.Add(connector);
                components.Add(hatch);
            }

            void AddPipeConnector()
            {
                var param = (CleanRoomPipeConnectorBlockParam)blockMasterElement.BlockParam;
                var connector = IFluidInventory.CreateFluidInventoryConnector(param.FluidInventoryConnectors, blockPositionInfo);
                const float capacity = 100f; // FluidPipe と同値
                var pipe = componentStates == null
                    ? new CleanRoomPipeConnectorComponent(capacity, connector)
                    : new CleanRoomPipeConnectorComponent(componentStates, capacity, connector);
                components.Add(connector);
                components.Add(pipe);
            }

            #endregion
        }
    }
}
```

> `CleanRoomPipeConnectorComponent` / `CleanRoomDoorComponent` は Task 4/5 で実装するが、本テンプレを先に完全形で書くため、それらの型は **Task 4/5 完了までコンパイルが通らない**。よってこの Step では `AddPipeConnector` と `Door` 分岐を**一時コメントアウト**してハッチだけ有効化してコンパイル→Task 3 のテストを緑にし、Task 4/5 で各分岐をアンコメントする運用でもよい。あるいは Task 4/5 のコンポーネントを先に空実装で置いてから本テンプレを入れる。**実装者はどちらか一方の順序を選ぶ**（推奨: ハッチ分岐のみ有効化で Task 3 を緑 → Task 4 でパイプ実装＋分岐アンコメント → Task 5 でドア実装＋分岐アンコメント）。
>
> `IBlockTemplate.New/Load` の正確なシグネチャ、`BlockSystem` のコンストラクタ引数順、`BlockMasterElement.BlockParam` のキャスト、`BlockConnectorComponent<IBlockInventory>` のコンストラクタ引数（`InputConnects`/`OutputConnects`）は `VanillaFluidBlockTemplate.cs`・`VanillaBeltConveyorComponent.cs`・`BlockConnectorComponent.cs`・フェーズ1の同テンプレで確認して一致させる。生成 param 型名 `CleanRoomItemHatchBlockParam.InventoryConnectors` は Task 1 の生成物。

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。型未検出なら Unity 再起動（新規 `.cs`＋新規コネクタ param 生成のため初回は再起動が要る）。

- [ ] **Step 6: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ItemHatch_RelaysItemInwardAndReportsThroughput"`
Expected: PASS。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomItemHatchComponent.cs moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs
git commit -m "feat(cleanroom): アイテムハッチの壁貫通中継とレート窓を実装"
```

---

## Task 4: パイプコネクタ — 壁貫通流体中継

`CleanRoomPipeConnectorComponent` は `IFluidInventory` として外パイプから `AddLiquid` を受け、内部 `FluidContainer` に溜め、`Update` で `BlockConnectorComponent<IFluidInventory>.ConnectedTargets`（室内流体ブロック）へ push する。`FluidPipeComponent` を簡略化した push 型。

> **設計判断（advisor 指摘の確定）:** codemap §5 のシグネチャは `IFluidInventory, IBlockSaveState` のみだが、moorestech の流体は **push モデル**（`FluidPipeComponent.Update` が隣接へ配る）。`AddLiquid` のみの受動コンポーネントだと溜まるだけで室内へ流れず中継テストが失敗する。よって **`IUpdatableBlockComponent` を足し、`FluidPipeComponent` 同様に Update で push** する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomPipeConnectorComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`

- [ ] **Step 1: 失敗テスト（流体が室内へ届く）を追加**

`CleanRoomIoTest` に追加:

```csharp
        // パイプコネクタが外パイプから受けた流体を室内パイプへ中継する
        // Pipe connector relays fluid received from the outer pipe to the inner pipe
        [Test]
        public void PipeConnector_RelaysFluidInward()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // コネクタを (0,0,0)、内側パイプを (1,0,0) に置く（outflow=+X が内側パイプに噛み合う）
            // Connector at (0,0,0), inner pipe at (1,0,0) (outflow=+X meets the inner pipe)
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomPipeConnector, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var connectorBlock);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipeId, new Vector3Int(1, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var innerPipe);

            Assert.True(connectorBlock.TryGetComponent<CleanRoomPipeConnectorComponent>(out var connector));

            // 外パイプ相当として、コネクタへ直接 AddLiquid する
            // Add fluid directly to the connector as a stand-in for the outer pipe
            var fluidId = ForUnitTestItemId.FluidId1;
            var stack = new Game.Fluid.FluidStack(50.0, fluidId);
            connector.AddLiquid(stack, Game.Fluid.FluidContainer.Empty);

            // 中継が進むまで tick を回す
            // Tick until the relay propagates
            GameUpdater.RunFrames(10);

            // 室内パイプに流体が届いている
            // Fluid has arrived in the inner pipe
            Assert.True(innerPipe.TryGetComponent<Game.Block.Blocks.Fluid.IFluidInventory>(out var innerInv));
            var innerAmount = innerInv.GetFluidInventory().Sum(f => f.Amount);
            Assert.Greater(innerAmount, 0.0, "Relayed fluid reaches the inner pipe");
        }
```

> `ForUnitTestModBlockId.FluidPipeId` / `ForUnitTestItemId.FluidId1` の実名は既存 fluid テスト（`Fluid*Test.cs`）に合わせる。`FluidStack` / `FluidContainer.Empty` は `Game.Fluid` 名前空間（確認済み）。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PipeConnector_RelaysFluidInward"`
Expected: FAIL（`CleanRoomPipeConnectorComponent` 未定義）。

- [ ] **Step 3: CleanRoomPipeConnectorComponent を実装**

`Game.Block/Blocks/CleanRoom/CleanRoomPipeConnectorComponent.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Blocks.Fluid;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // 壁貫通パイプコネクタ。外パイプから受けた流体を内部コンテナに溜め、毎tick室内へpushする
    // Wall-piercing pipe connector: buffers fluid from the outer pipe and pushes it inward each tick
    public class CleanRoomPipeConnectorComponent : IFluidInventory, IUpdatableBlockComponent, IBlockSaveState
    {
        public string SaveKey => SaveKeyStatic;
        public static string SaveKeyStatic { get; } = typeof(CleanRoomPipeConnectorComponent).FullName;

        private readonly FluidContainer _container;
        private readonly BlockConnectorComponent<IFluidInventory> _connector;

        public CleanRoomPipeConnectorComponent(float capacity, BlockConnectorComponent<IFluidInventory> connector)
        {
            _container = new FluidContainer(capacity);
            _connector = connector;
        }

        // セーブからの復元: 内部流体の ID/量を戻す（FluidPipeComponent と同方式）
        // Restore from save: fluid id/amount of the inner container (same as FluidPipeComponent)
        public CleanRoomPipeConnectorComponent(Dictionary<string, string> componentStates, float capacity, BlockConnectorComponent<IFluidInventory> connector)
            : this(capacity, connector)
        {
            if (!componentStates.TryGetValue(SaveKey, out var raw)) return;
            var json = JsonConvert.DeserializeObject<FluidPipeSaveJsonObject>(raw);
            if (json == null) return;
            _container.FluidId = json.FluidId;
            _container.Amount = json.Amount;
        }

        // 外パイプから受ける。ソース帰属は単純化し Empty で受ける
        // Accept from the outer pipe; simplify source attribution by accepting with Empty
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            BlockException.CheckDestroy(this);
            return _container.AddLiquid(fluidStack, FluidContainer.Empty);
        }

        public List<FluidStack> GetFluidInventory()
        {
            var list = new List<FluidStack>();
            if (_container.Amount > 0) list.Add(new FluidStack(_container.Amount, _container.FluidId));
            return list;
        }

        // 毎tick: 内部流体を接続先(室内)の IFluidInventory へ流量上限まで push する
        // Each tick: push the buffered fluid to connected (inner) IFluidInventory up to the flow cap
        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (_container.Amount <= 0) { _container.ClearPreviousSources(); return; }

            DistributeToTargets();
            _container.ClearPreviousSources();
            if (_container.Amount <= 0) _container.FluidId = FluidMaster.EmptyFluidId;

            #region Internal

            void DistributeToTargets()
            {
                var targets = _connector.ConnectedTargets;
                if (targets.Count == 0) return;

                foreach (var kvp in targets)
                {
                    if (_container.Amount <= 0) break;
                    var maxFlow = GetMaxFlowRate(kvp.Value);
                    if (maxFlow <= 0) continue;

                    var sendAmount = Math.Min(_container.Amount, maxFlow);
                    var stack = new FluidStack(sendAmount, _container.FluidId);
                    var remain = kvp.Key.AddLiquid(stack, _container);
                    var accepted = sendAmount - remain.Amount;
                    _container.Amount -= accepted;
                }
            }

            // 自他のFlowCapacityの最小×1tick秒。FluidPipeComponent と同流儀
            // min(self,target FlowCapacity) * seconds-per-tick; same as FluidPipeComponent
            double GetMaxFlowRate(ConnectedInfo info)
            {
                var selfOption = info.SelfConnector?.ConnectOption as FluidConnectOption;
                var targetOption = info.TargetConnector?.ConnectOption as FluidConnectOption;
                if (selfOption == null || targetOption == null) return 0;
                return Math.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * GameUpdater.SecondsPerTick;
            }

            #endregion
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var json = new FluidPipeSaveJsonObject
            {
                FluidIdValue = _container.FluidId.AsPrimitive(),
                Amount = (float)_container.Amount,
                Capacity = (float)_container.Capacity,
            };
            return JsonConvert.SerializeObject(json);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
```

> `FluidConnectOption.FlowCapacity` / `ConnectedInfo.SelfConnector.ConnectOption` / `FluidContainer.ClearPreviousSources()` / `FluidMaster.EmptyFluidId` / `FluidPipeSaveJsonObject` は本プランで読んだ `FluidPipeComponent.cs` / `FluidPipeSaveComponent.cs` と一致（確認済み）。`FluidPipeSaveJsonObject` を流用するか、独自JSONにするかは Task 7 のセーブテストで確定。

- [ ] **Step 4: テンプレートの PipeConnector 分岐を有効化**

`VanillaCleanRoomBoundaryTemplate`（Task 3 Step 4 で完全形を作成済み）の `AddPipeConnector` 分岐をアンコメントして有効化する。コードは Task 3 の `AddPipeConnector`（`IFluidInventory.CreateFluidInventoryConnector` ＋ `CleanRoomPipeConnectorComponent` ＋ `CleanRoomBoundaryComponent(PipeConnector)`、`capacity=100f`）をそのまま使う。本タスクで `CleanRoomPipeConnectorComponent` が実装されたので分岐がコンパイルできる。

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。型未検出なら Unity 再起動。

- [ ] **Step 6: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PipeConnector_RelaysFluidInward"`
Expected: PASS。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomPipeConnectorComponent.cs moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs
git commit -m "feat(cleanroom): パイプコネクタの壁貫通流体中継を実装"
```

---

## Task 5: ドア ＋ テンプレートへの kind 別合成（New/Load）

`CleanRoomDoorComponent` はプレイヤー通過を離散メソッド `NotifyPlayerPassage()` で受け、`burst_door = 15` を保留バーストに溜める。`ConsumePendingBurst()` で計算側が読み取りリセットする（consume-and-clear）。ドアはマーカーで密閉境界のまま（0.2）。あわせて `VanillaCleanRoomBoundaryTemplate` を 3 kind 分の合成（New/Load）で完全版にする。

> **プレイヤー通過の seam（正直な注記）:** サーバー側に「プレイヤーがドアブロックを跨いだ」イベントは**存在しない**。プレイヤー座標はクライアントが `SetPlayerCoordinateProtocol`（`va:playerCoordinate`）でストリームし `IEntitiesDatastore.SetPosition` に入るのみ（本プランで確認済み）。座標を監視してドア通過を検出する watcher の実装は**本プランのスコープ外**。よってドア通過は **サーバー側メソッド `NotifyPlayerPassage()` を統合 seam とし、将来の座標 watcher か client 検出がこれを呼ぶ**。テストは `NotifyPlayerPassage()` を**直接呼んで**バーストを検証する。seam を隠さず明示する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomDoorComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`

- [ ] **Step 1: 失敗テスト（通過でバースト、消費でリセット、密閉維持）**

`CleanRoomIoTest` に追加:

```csharp
        // ドアはプレイヤー通過でバーストを溜め、消費すると0に戻り、密閉境界のまま
        // The door accumulates a burst on passage, resets to 0 when consumed, and remains a sealing boundary
        [Test]
        public void Door_PlayerPassageRaisesBurstThenClears()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomDoor, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var doorBlock);

            // ドアは密閉境界マーカーを持つ（0.2 のドア整合）
            // The door carries the sealing-boundary marker (door reconciliation 0.2)
            Assert.True(doorBlock.TryGetComponent<ICleanRoomBoundaryComponent>(out var marker));
            Assert.AreEqual(CleanRoomBoundaryKind.Door, marker.BoundaryKind);

            Assert.True(doorBlock.TryGetComponent<CleanRoomDoorComponent>(out var door));

            // 2回通過 → バースト = 2 * burst_door(15) = 30
            // Two passages → burst = 2 * burst_door(15) = 30
            door.NotifyPlayerPassage();
            door.NotifyPlayerPassage();
            var consumed = door.ConsumePendingBurst();
            Assert.AreEqual(30.0, consumed, 1e-6);

            // 消費後は0
            // Zero after consume
            Assert.AreEqual(0.0, door.ConsumePendingBurst(), 1e-6);
        }
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Door_PlayerPassageRaisesBurstThenClears"`
Expected: FAIL（`CleanRoomDoorComponent` 未定義）。

- [ ] **Step 3: CleanRoomDoorComponent を実装**

`Game.Block/Blocks/CleanRoom/CleanRoomDoorComponent.cs`:

```csharp
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // クリーンルームのドア。プレイヤー通過バーストを溜め、計算側に読み取りリセットで渡す
    // Clean-room door: accumulates player-passage burst, handed off to the calculator via read-and-reset
    public class CleanRoomDoorComponent : IBlockComponent
    {
        // プレイヤー1通過あたりの瞬間加算量（balance §2: burst_door）
        // Per-passage instantaneous addition (balance §2: burst_door)
        public const double DoorPassageBurst = 15.0;

        private double _pendingBurst;

        // 統合seam: 将来の座標watcher/クライアント検出がプレイヤー通過時に呼ぶ
        // Integration seam: a future coordinate watcher / client detection calls this on player passage
        public void NotifyPlayerPassage()
        {
            _pendingBurst += DoorPassageBurst;
        }

        // 計算側が毎tick読み取り、保留バーストを0に戻す（consume-and-clear）
        // The calculator reads each tick and resets the pending burst to zero (consume-and-clear)
        public double ConsumePendingBurst()
        {
            var burst = _pendingBurst;
            _pendingBurst = 0;
            return burst;
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
```

> ドアの保留バーストは**保存しない**（揮発。次tickで計算側が消費するため）。`IBlockComponent` の必須メンバ（`IsDestroy`/`Destroy`）は既存単純コンポーネント（`CleanRoomBoundaryComponent`）に合わせる。
>
> **消費の一意性（重要）:** `ConsumePendingBurst()` は破壊的（読むと0にリセット）。正しさは「`CleanRoomPollutionCalculator.CalculateTotalPollution` が毎tick **ちょうど1回** だけこれを呼ぶ」前提に依存する（`CleanRoomPurityService.OnTick` が毎tick1回 `CalculateTotalPollution` を呼ぶ運用）。UI/デバッグ表示など**tick外の照会から `CalculateTotalPollution` を呼ぶとバーストを横取りして消えてしまう**。tick外で純度/汚染を覗く用途が出たら、peek（非破壊）と advance（消費）を分けること。本フェーズでは唯一の消費者を計算側1経路に限定する。

- [ ] **Step 4: テンプレートの Door 分岐を有効化**

`VanillaCleanRoomBoundaryTemplate`（Task 3 Step 4 で完全形を作成済み）の `case CleanRoomBoundaryKind.Door:` 分岐（`components.Add(new CleanRoomDoorComponent());`）をアンコメントして有効化する。`CleanRoomDoorComponent` が本タスクで実装されたのでコンパイルできる。これで Wall（マーカーのみ）／ItemHatch（Task 3）／PipeConnector（Task 4）／Door（本タスク）の全 kind が揃う。テンプレ本体のコードは Task 3 Step 4 の通り（再掲しない）。

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。型未検出なら Unity 再起動。

- [ ] **Step 6: テスト実行（ドア＋既出2件の回帰）**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(Door_PlayerPassageRaisesBurstThenClears|ItemHatch_RelaysItemInwardAndReportsThroughput|PipeConnector_RelaysFluidInward)"`
Expected: 全 PASS。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomDoorComponent.cs moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs
git commit -m "feat(cleanroom): ドアの通過バーストとテンプレのkind別合成を実装"
```

---

## Task 6: CleanRoomPollutionCalculator に A_hatch / A_door を配線

フェーズ3の `CleanRoomPollutionCalculator`（`CleanRoomPollutionInput` 実装）に、室内境界ブロックから `A_hatch`（ハッチレートの合計 × `k_hatch`）と `A_door`（ドアの保留バースト消費の合計）を取り込む。`A_hatch` はローリングレート、`A_door` は consume-and-clear で形が異なる。

> **形の違い（advisor 指摘の確定）:** `A_hatch = k_hatch · Σ hatch.RecentThroughputPerSecond`（窓で減衰するレート、毎tick評価しても搬送が続く限り正）。`A_door = Σ door.ConsumePendingBurst()`（バースト消費＝読んだら0、次tickは通過が無ければ0）。計算側は部屋の各境界セル→ブロック→`TryGetComponent<CleanRoomItemHatchComponent>` / `<CleanRoomDoorComponent>` で集計する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPollutionCalculator.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`

- [ ] **Step 1: 失敗テスト（搬送で A_total 上昇、通過バーストは1tickで消費）**

このテストはフェーズ1〜3の検出/計算 API（`CleanRoomDetectionSystem` / `CleanRoomPollutionCalculator` / `CleanRoom`）に依存する。`CleanRoomPollutionCalculator` の公開 API 名（例 `CalculateTotalPollution(CleanRoom room)` 等）は実ファイルを開いて一致させる。下は意図を示す骨子:

```csharp
        // 室内ハッチの搬送が続くと A_hatch 分だけ A_total が増える
        // While the in-room hatch keeps relaying, A_total rises by the A_hatch term
        [Test]
        public void Pollution_HatchThroughputRaisesAtotal()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var detection = serviceProvider.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();
            var calculator = serviceProvider.GetService<Game.CleanRoom.Purity.CleanRoomPollutionCalculator>();

            // 壁シェルの1面をハッチに置換した密閉部屋を作る（BuildSealedRoomWithHatch ヘルパ）
            // Build a sealed room with one wall face replaced by a hatch (BuildSealedRoomWithHatch helper)
            var hatch = BuildSealedRoomWithHatchAndInnerChest(world);
            GameUpdater.RunFrames(1);
            Assert.True(detection.TryGetRoomContainingBlock(hatch, out var room));

            // 搬送前の A_total
            // A_total before any relay
            var before = calculator.CalculateTotalPollution(room);

            // ハッチへアイテムを入れ、中継 tick を回す（窓に搬送が乗る）
            // Insert items into the hatch and tick so the relay lands in the window
            Assert.True(hatch.TryGetComponent<CleanRoomItemHatchComponent>(out var hatchComp));
            for (var i = 0; i < 10; i++)
            {
                hatchComp.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
                GameUpdater.RunFrames(1);
            }

            var after = calculator.CalculateTotalPollution(room);
            Assert.Greater(after, before, "A_hatch raises A_total while throughput is positive");
        }

        // ドア通過バーストは計上された次のtickには消費されて0へ戻る（恒常加算しない）
        // The door burst is consumed the next tick and returns to 0 (it does not add permanently)
        [Test]
        public void Pollution_DoorBurstIsConsumedOnce()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var detection = serviceProvider.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();
            var calculator = serviceProvider.GetService<Game.CleanRoom.Purity.CleanRoomPollutionCalculator>();

            var door = BuildSealedRoomWithDoor(world);
            GameUpdater.RunFrames(1);
            Assert.True(detection.TryGetRoomContainingBlock(door, out var room));
            Assert.True(door.TryGetComponent<CleanRoomDoorComponent>(out var doorComp));

            var baseline = calculator.CalculateTotalPollution(room);

            // 通過1回 → このtickの A_total は baseline + burst_door(15)
            // One passage → this tick's A_total is baseline + burst_door(15)
            doorComp.NotifyPlayerPassage();
            var withBurst = calculator.CalculateTotalPollution(room);
            Assert.AreEqual(baseline + 15.0, withBurst, 1e-3);

            // 次の評価では消費済みで baseline に戻る
            // Next evaluation is back to baseline (already consumed)
            var afterConsume = calculator.CalculateTotalPollution(room);
            Assert.AreEqual(baseline, afterConsume, 1e-3);
        }
```

> `BuildSealedRoomWithHatchAndInnerChest` / `BuildSealedRoomWithDoor` はテスト内ローカルヘルパ。フェーズ1の `BuildWallShell` を流用し1面を該当ブロックへ置換する。`CalculateTotalPollution` は **計算側 API の実名に合わせる**（フェーズ3で `CleanRoomPollutionInput` がどう A_total を返すか確認）。`A_door` は「計算が1回読むと消費」なので、テストは**計算を毎tick1回だけ呼ぶ**前提で書く（実運用は `CleanRoomPurityService.OnTick` が毎tick1回呼ぶ）。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Pollution_(HatchThroughputRaisesAtotal|DoorBurstIsConsumedOnce)"`
Expected: FAIL（A_hatch/A_door 未配線）。

- [ ] **Step 3: CleanRoomPollutionCalculator を拡張**

`CleanRoomPollutionCalculator.cs` の A_total 集計に、室内境界ブロックからの2項を足す。実装はフェーズ3の既存集計メソッド内に組み込む（疑似コード→実 API に合わせる）:

```csharp
        // バランス §2 の係数。A_hatch のレート係数とドアバースト
        // Balance §2 coefficients: hatch rate factor and door burst (door burst comes from the component)
        private const double KHatch = 0.30;

        // 部屋内の境界ブロックを走査し、A_hatch（レート）と A_door（バースト消費）を加える
        // Scan in-room boundary blocks and add A_hatch (rate) and A_door (burst consume)
        private double CalculateIoPollution(CleanRoom room)
        {
            var aHatch = 0.0;
            var aDoor = 0.0;

            // 部屋の境界セル上のブロックを取り、ハッチ/ドアコンポーネントを集計
            // For each boundary-cell block, sum hatch/door components
            foreach (var block in EnumerateBoundaryBlocks(room))
            {
                if (block.TryGetComponent<CleanRoomItemHatchComponent>(out var hatch))
                    aHatch += KHatch * hatch.RecentThroughputPerSecond;

                if (block.TryGetComponent<CleanRoomDoorComponent>(out var door))
                    aDoor += door.ConsumePendingBurst(); // consume-and-clear
            }

            return aHatch + aDoor;
        }
```

> `EnumerateBoundaryBlocks(room)` はフェーズ3で `a_connector·count` を数えるために既に境界ブロックを走査しているはず。**その既存走査に相乗りして**ハッチ/ドアを同時に集計するのが望ましい（二重走査・二重 consume を避ける）。`ConsumePendingBurst()` は1評価に1回だけ呼ぶこと（`CleanRoomPurityService.OnTick` が毎tick1回 `CalculateTotalPollution` を呼ぶ前提）。`Game.CleanRoom` asmdef が `Game.Block`（CleanRoom コンポーネント型）を参照できるか確認（フェーズ3で機械参照のため既に通っている見込み）。

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Pollution_(HatchThroughputRaisesAtotal|DoorBurstIsConsumedOnce)"`
Expected: 全 PASS。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/Purity/CleanRoomPollutionCalculator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs
git commit -m "feat(cleanroom): A_hatch/A_doorを汚染計算へ配線"
```

---

## Task 7: 永続化仕上げ — I/O state round-trip ＋ 純度セーブの非回帰

I/O 固有状態（ハッチ中継中アイテム・パイプ内流体）を `IBlockSaveState` で round-trip させ、グローバルセーブスキーマを触らずに完結することを固定する。あわせて I/O ブロックが部屋に在っても `CleanRoomPuritySaveData`（N＋Cells）が round-trip することを確認する。

> **確定事項（advisor 指摘）:**
> - **コネクタ re-link に IPostBlockLoad は不要。** `BlockConnectorComponent` はコンストラクタで `WorldBlockUpdateEvent` を購読し、既存隣接があればその場で接続する（本プランで確認済み。`FluidPipeComponent` が IPostBlockLoad 無しでロード後も繋がるのと同じ）。よって**コネクタ再リンク目的の IPostBlockLoad は追加しない**。本当に自力再構築できないクロスブロック参照が出た場合のみ追加する（本フェーズでは不要）。
> - **`CleanRoomPuritySaveData` は変更不要の見込み。** I/O state は各ブロックの `IBlockSaveState` に載るため、純度セーブは N＋Cells のまま。これを**テストで証明**し、テストが強制しない限り `CleanRoomPuritySaveData` は改変しない。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`
- （必要が判明した場合のみ）Modify: 各 I/O コンポーネントの `GetSaveState`/復元コンストラクタ

- [ ] **Step 1: 失敗テスト（ハッチ中継中アイテムの save/load round-trip）**

`CleanRoomIoTest` に追加。セーブ/ロードの実 API（`AssembleSaveJsonText` → JSON 文字列、`WorldLoaderFromJson` → 復元）は既存セーブテスト（`Tests/CombinedTest/.../SaveLoad*Test.cs` 等）の流儀に合わせる:

```csharp
        // ハッチが中継待ちアイテムを保持した状態でsave→loadし、アイテムが復元される
        // Save while the hatch holds in-transit items, then load and confirm they are restored
        [Test]
        public void ItemHatch_InTransitItemsSurviveSaveLoad()
        {
            // --- セーブ側ワールド ---
            var (packetA, providerA) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldA = ServerContext.WorldBlockDatastore;

            // 室内ターゲットを置かず、ハッチに中継待ちを溜めたまま保存する（中継が完了しないように）
            // Save with no inner target so items stay in-transit (relay cannot complete)
            worldA.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatch, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchA);
            Assert.True(hatchA.TryGetComponent<CleanRoomItemHatchComponent>(out var hatchCompA));
            hatchCompA.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3), InsertItemContext.Empty);
            GameUpdater.RunFrames(2); // ターゲット無し → バッファに残る

            var json = providerA.GetService<Game.SaveLoad.Json.AssembleSaveJsonText>().AssembleSaveJson();

            // --- ロード側ワールド ---
            var (packetB, providerB) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            providerB.GetService<Game.SaveLoad.Json.WorldLoaderFromJson>().Load(json);

            var worldB = ServerContext.WorldBlockDatastore;
            Assert.True(worldB.TryGetBlock(new Vector3Int(0, 0, 0), out IBlock hatchB));
            Assert.True(hatchB.TryGetComponent<CleanRoomItemHatchComponent>(out var hatchCompB));

            // 復元後、中継待ちの3個が残っている
            // After load, the 3 in-transit items are restored
            var restored = 0;
            for (var i = 0; i < hatchCompB.GetSlotSize(); i++) restored += hatchCompB.GetItem(i).Count;
            Assert.AreEqual(3, restored);
        }
```

> `AssembleSaveJsonText.AssembleSaveJson()` / `WorldLoaderFromJson.Load(string)` のシグネチャと取得方法（DI から取るか static か）は既存 SaveLoad テストに正確に合わせる。`ServerContext.WorldBlockDatastore` が DI コンテナごとに差し替わる点に注意（既存テストの流儀を踏襲）。パイプの流体 round-trip も同型で1テスト追加（内流体を溜めた状態で save/load し `GetFluidInventory().Sum(Amount)` を比較）。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ItemHatch_InTransitItemsSurviveSaveLoad"`
Expected: FAIL の可能性（テンプレ Load 経路が `componentStates` をハッチ復元コンストラクタへ渡していなければ落ちる）。落ちたら Task 5 Step 4 の `AddHatch` の Load 分岐（`componentStates` 非null時に復元コンストラクタを使う）を直す。

- [ ] **Step 3: 必要なら復元経路を修正**

`VanillaCleanRoomBoundaryTemplate.Build` の Load 経路で `componentStates` が各 I/O 復元コンストラクタへ渡っていることを確認・修正（Task 5 の実装で既に分岐済みなら不要）。`SaveKey` の一意性（`typeof(...).FullName`）でグローバルセーブ JSON に衝突なく載ることを確認。

- [ ] **Step 4: 純度セーブの非回帰テストを追加**

I/O ブロックを含む部屋で `CleanRoomPuritySaveData`（N＋Cells）が round-trip することを確認するテストを追加。フェーズ2の純度セーブ API（codemap §1.3: `CleanRoomSaveLoadService.GetSaveData()/Restore()`、§2: `CleanRoomPurityService.TryGetState(room, out state)` ＋ `state.ImpurityCount`、balance §6 のセーブ項目）を使う。`CleanRoomPuritySaveData` を**改変せずに**緑になることをアサートし、結論をコメントで明示する。

```csharp
        // I/Oブロックを含む部屋でも純度セーブ(N+Cells)はそのまま round-trip する（CleanRoomPuritySaveData 改変不要）
        // Purity save (N+Cells) round-trips even with I/O blocks present (no CleanRoomPuritySaveData change needed)
        [Test]
        public void PuritySave_RoundTripsWithIoBlocksPresent()
        {
            // --- セーブ側: 壁シェルの1面をハッチに置換した密閉部屋を作り、N をシードする ---
            // Save side: build a sealed room with one wall face replaced by a hatch, seed N
            var (_, providerA) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldA = ServerContext.WorldBlockDatastore;
            var detectionA = providerA.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();
            var purityA = providerA.GetService<Game.CleanRoom.Purity.CleanRoomPurityService>();

            var hatchA = BuildSealedRoomWithHatchAndInnerChest(worldA);
            GameUpdater.RunFrames(1);
            Assert.True(detectionA.TryGetRoomContainingBlock(hatchA, out var roomA));

            // 純度状態に既知の N をシードする（フェーズ2の AddImpurity 経路）
            // Seed a known N into the purity state (phase-2 AddImpurity path)
            Assert.True(purityA.TryGetState(roomA, out var stateA));
            stateA.AddImpurity(123.0);
            var expectedN = stateA.ImpurityCount;

            var json = providerA.GetService<Game.SaveLoad.Json.AssembleSaveJsonText>().AssembleSaveJson();

            // --- ロード側: ブロック→検出→純度復元の順で N が戻る ---
            // Load side: blocks → detection → purity restore, N is recovered
            var (_, providerB) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            providerB.GetService<Game.SaveLoad.Json.WorldLoaderFromJson>().Load(json);
            var worldB = ServerContext.WorldBlockDatastore;
            var detectionB = providerB.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();
            var purityB = providerB.GetService<Game.CleanRoom.Purity.CleanRoomPurityService>();

            GameUpdater.RunFrames(1);
            Assert.True(worldB.TryGetBlock(hatchA.BlockPositionInfo.OriginalPos, out IBlock hatchB));
            Assert.True(detectionB.TryGetRoomContainingBlock(hatchB, out var roomB));
            Assert.True(purityB.TryGetState(roomB, out var stateB));

            // I/O面が在っても純度セーブは N を素通しで復元する（スキーマ改変不要）
            // Purity save restores N intact even with the I/O face present (no schema change)
            Assert.AreEqual(expectedN, stateB.ImpurityCount, 1e-6);
        }
```

> `CleanRoomPurityService.TryGetState` / `CleanRoomPurityState.AddImpurity`/`ImpurityCount` / `BlockPositionInfo.OriginalPos` / 純度復元が `WorldLoaderFromJson.Load` 内の「ブロック後」フックで走ること（codemap §1.3）は、フェーズ2成果物の実シグネチャに合わせて確認・調整する。フェーズ2が手元で確定したら名前差分だけ直す（構成・アサートの骨子はこのまま）。**結論: テストが緑なら `CleanRoomPuritySaveData` は N＋Cells のまま変更不要。**

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(ItemHatch_InTransitItemsSurviveSaveLoad|PipeConnector_.*SaveLoad|PuritySave_RoundTripsWithIoBlocksPresent)"`
Expected: 全 PASS。

- [ ] **Step 6: フェーズ5全テスト＋既存回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: 全 PASS。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(GearBeltConveyor|Fluid|SaveLoad)Test"`
Expected: 従来どおり PASS（I/O 追加・セーブ流用が既存を壊していない）。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/ moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs
git commit -m "test(cleanroom): I/O state round-tripと純度セーブ非回帰を固定しフェーズ5完了"
```

---

## フェーズ5 完了の定義（Definition of Done）

- ハッチ/パイプ境界に I/O コネクタ param がスキーマ・テスト mod まで通り、設置できる。
- `CleanRoomItemHatchComponent` が外→内へアイテムを中継し、`RecentThroughputPerSecond`（窓 = 20tick = 1秒）を公開する。中継待ちアイテムが save/load で復元される。
- `CleanRoomPipeConnectorComponent` が外→内へ流体を中継する（push 型 Update）。内部流体が save/load で復元される。
- `CleanRoomDoorComponent` が `NotifyPlayerPassage()` で `burst_door=15` を溜め、`ConsumePendingBurst()` で消費リセットする。ドアは密閉境界マーカーを保持（フェーズ1整合）。
- `VanillaCleanRoomBoundaryTemplate` が kind 別に密閉マーカー＋I/O挙動を New/Load で合成する。Wall はマーカーのみ。
- `CleanRoomPollutionCalculator` が `A_hatch = k_hatch·Σ throughput` と `A_door = Σ consume burst` を A_total に取り込む。搬送で A_total が上がり、ドアバーストは1tickで消費される。
- `CleanRoomPuritySaveData`（N＋Cells）は I/O ブロック在室でも改変なしで round-trip する。コネクタ再リンクは `BlockConnectorComponent` の自動接続に任せ、IPostBlockLoad は不要。
- 既存テスト（belt/fluid/saveload）が非回帰。

## フェーズ5で意図的にスコープ外とした事項

- **プレイヤー座標→ドア通過の自動検出 watcher**: 本プランは seam（`NotifyPlayerPassage()`）まで。座標ストリーム（`SetPlayerCoordinateProtocol`/`IEntitiesDatastore`）を監視してドアセル跨ぎを検出する実装は別タスク。
- **ハッチのスループット上限**: 「低スループット＝集約の根拠」（設計書 §7）の上限値は未設定（無制限受理）。バランス調整で上限を入れる場合は別途。
- 本番 mod（moorestech_master）の blocks.json への I/O ブロック配線・モデル/画像アセット。
- 高度なソース帰属（パイプの per-source バケット。本コネクタは Empty 帰属で簡略化）。

---

## Self-Review

**codemap §5 の網羅:**
- `CleanRoomItemHatchComponent`（`IBlockInventory, IUpdatableBlockComponent, IBlockSaveState` ＋ `RecentThroughputPerSecond`）: Task 3 ✓
- `CleanRoomPipeConnectorComponent`（`IFluidInventory, IBlockSaveState`、`CreateFluidInventoryConnector`）: Task 4 ✓（`IUpdatableBlockComponent` を push 用に追加した理由を明記 ✓）
- `CleanRoomDoorComponent`（通過→A_door バースト、flood-fill 上は密閉境界）: Task 5 ✓
- `VanillaCleanRoomBoundaryTemplate` への合成（New/Load、kind switch）: Task 5 ✓
- `CleanRoomPollutionCalculator` 拡張（A_hatch レート＋A_door バースト）: Task 6 ✓
- I/O state の `IBlockSaveState` round-trip ＋ `CleanRoomPuritySaveData` 非回帰: Task 7 ✓
- スキーマ（hatch inventory / pipe fluid コネクタ param 追加・regen・テスト mod）: Task 1/2 ✓

**design §5/§7/§9 の網羅:**
- §5 汚染源: `A_hatch` はレート換算（1個ごと加算でない）✓、`A_door` は通過バースト ✓、`a_connector` は接続点数（占有でなく境界として寄与、0.3 で明記）✓。
- §7 I/O 役割分担: ハッチ=アイテム低スループット＋汚染源、パイプ=流体、ドア=人で汚染大 → 3コンポーネントで実装 ✓。
- §9 アイテム外部取り出しで物が変化しない: I/O は **アイテム本体を一切改変せず中継するのみ**（`InsertItem`/`AddLiquid` をそのまま転送、グレード等の書き換えなし）。Task 3/4 の実装は item/fluid を素通しするので §9 を満たす。Task 7 のセーブテストも個数・量を保存するだけ。**非回帰メモ:** ハッチ/パイプは純度をアイテムに焼き込まない（純度は製造の瞬間に部屋属性として参照されるのみ。フェーズ4の責務）。

**プレースホルダ走査:**
- コード本体に未確定値なし。`k_hatch=0.30`/`burst_door=15`/`a_connector=0.50`/窓=20tick/容量100f は具体値。
- 残る `<...>` 風の穴は無い。Task 2 の JSON は「既存書式に合わせる」指示＋構造例（実フィールド名は実 mod 確認）で、これは GUID 等の捏造を避ける安全網であって本文の数値は具体。
- Task 7 Step 4 の純度セーブ round-trip テストは `Assert.Pass` を排し、フェーズ2の文書化済み API（`CleanRoomPurityService.TryGetState` / `CleanRoomPurityState.AddImpurity`/`ImpurityCount` / `CleanRoomSaveLoadService` の「ブロック後復元」）に対する**具体コード**で書いた。フェーズ2成果物が未マージのため名前差分の調整余地は残すが、構成・アサートは捏造でなく文書化済み契約に接地。

**契約名の型整合:** `CleanRoomItemHatchComponent` / `CleanRoomPipeConnectorComponent` / `CleanRoomDoorComponent` / `CleanRoomPollutionCalculator` / `CleanRoomPuritySaveData` / `VanillaCleanRoomBoundaryTemplate` / `CleanRoomBoundaryComponent` / `CleanRoomBoundaryKind`（`Wall`/`Door`/`ItemHatch`/`PipeConnector`）を verbatim 使用。重複・別名なし ✓。

**ドア密閉 vs フェーズ1 の整合（明示確認）:** 0.2/0.4 で確定。ドアは `CleanRoomBoundaryComponent(Door)` マーカーを持ち、flood-fill 検出では**閉じた壁と同一の密閉境界**（空気を漏らさない）。プレイヤー通過は `NotifyPlayerPassage()` の**離散イベント**で `A_door` バーストを足すだけで、密閉性・V には影響しない。Task 5 のテスト `Door_PlayerPassageRaisesBurstThenClears` が「ドアが `ICleanRoomBoundaryComponent` を保持」を直接アサートし、フェーズ1の「ドアが部屋を密閉する」と矛盾しないことを固定する ✓。

**占有 vs V（0.3）:** I/O 境界ブロックは boundary であり interior V に算入しない。`a_connector·count` で寄与。V 定義は本プランで不変 ✓。

**push モデルの整合:** パイプコネクタに `IUpdatableBlockComponent` を追加（codemap シグネチャを拡張）した根拠を Task 4 冒頭に明記。受動 `AddLiquid` のみだと室内へ流れず中継テストが空振りするため、`FluidPipeComponent` の push 型に揃えた ✓。
