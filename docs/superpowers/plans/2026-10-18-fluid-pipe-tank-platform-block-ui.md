# パイプ・タンク・液体PFのブロックUI Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** パイプ・タンク（blockType `FluidPipe`）と液体プラットフォーム（`TrainFluidPlatform`）のブロックUI、および液体貨車をFで開く貨車インベントリ画面に、中の液体のアイコン・名前・量/容量を出す。

**Architecture:** 表示は既存WebUIのBlockDetail（`dto.FluidSlots` → `FluidSlotRow`）をそのまま使う。液体PFはサーバー側でポンプと同じ `FluidMachineInventoryStateDetail` を状態として出し、クライアントの既存の液体スロット詰め経路に乗せる。パイプ・タンクは既存の `FluidPipeStateDetail` をクライアントで `dto.FluidSlots` へ詰め、マスタの `openable` を true にしてクリックで開けるようにする。

**Tech Stack:** Unity C#（server/client）、MessagePack、UniRx、React + Mantine（moorestech_web/webui）、マスタJSON（../moorestech_master）

## Requirements

1. 鉄のパイプ・鋼鉄のパイプ・液体タンク（blockType `FluidPipe`）をクリックするとブロックUIが開く。受け入れ: `moorestechAlphaMod_8/master/blocks.json` の3ブロックが `"openable": true`。
2. パイプ・タンクのUIに、中の液体のアイコン・名前・量/容量が1スロットで出る。空なら空スロット。受け入れ: `BlockDetailDtoBuilder.Apply` が `FluidPipeStateDetail` を持つブロックで `dto.FluidSlots` に1要素（FluidId/Amount/Capacity/FluidGuid）を詰める。WebUIでは `fluid-pipe-fluid-slots` の行に描かれ、アイテムグリッドと進捗表示は出ない。
3. 液体PFのUIに、PF自身が溜めている液体のアイコン・名前・量/容量が1スロットで出る（既存の積む/降ろす切り替えは残す。既存の「容量: N」の文字表示はスロットの量/容量と二重になり、貨車由来のコンテナでは値も食い違うので消し、DTOの `FluidCapacity` も撤去する）。受け入れ: `TrainPlatformFluidContainerComponent.GetBlockStateDetails()` が `FluidMachineInventoryStateDetail`（入力0本・出力1本）を返し、クライアントの既存経路で `dto.FluidSlots` に1要素入る。
4. 液体PFの中身が変わったら開いているUIが更新される。受け入れ: 注入・隣接への押し出し・貨車との受け渡しのたびに `OnChangeBlockState` が発火する（量が変わらなかったときは発火しない）。
5. 液体貨車をFで開いたとき、貨車インベントリ画面に貨車の液体のアイコン・名前・量/容量が出る。開いたまま量が変わったら更新される。受け入れ: 液体貨車への在庫要求が Success と液体1本を返す／貨車の液体が変わる・容器が付け外しされると加入者へ Refresh イベントが届き、クライアントが取り直す／WebUI の `train-inventory-fluids` に描かれる。
6. やらないこと: 液体を捨てる等の操作、ホバー表示、液体PFの画面に停車中の貨車の液体を並べること、ボイドパイプ、UI Toolkitでの実装、流量・方向・停止理由の表示。

## Global Constraints

- 1ファイル200行未満。`TrainPlatformFluidContainerComponent.cs` は現在269行なので、Task 1で分割して200行未満にする。
- コメントは日本語1行＋英語1行のセット。`#region Internal` はメソッド内ローカル関数だけ。
- `Func<>`・partial・try-catch・デフォルト引数は使わない。イベントはUniRx。
- `.meta` は手で作らない（Unityが生成したものはコミットしてよい）。
- 別リポジトリ `moorestech_master` の変更はpushしてPRを作り、本repoの `.moorestech-external-revisions.json` の `moorestech_master` ピンをそのPRのpush済みコミットへ向ける。
- C#を変えたら `uloop compile --project-path ./moorestech_client` を通す。
- 作業場所: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/fluid-block-ui`（ブランチ `feat/fluid-block-ui`）。最初に `pwd` で確認する。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 5配置とも前例（ポンプの状態発行・既存FluidSlots経路・SectionStackView設定表・液体PFのopenable）に一致、新しい経路なし、既存操作の死活に影響なし。Task 3（後から追加）は本体が再照合: 統合在庫イベントと在庫要求応答への相乗りで、3点セット（イベント・初期データ・購読）の前例どおり。違反0
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱1・第3バケツ1 / 弱=空戻し判定と変化通知の繰り返し（このままでよい、拒否権つき前提）、第3バケツ=隣接押し出しループがポンプ・機械と三重。Task 3 追加前の plan に対する実施

---

## 配置と前例

| # | 項目 | 配置先 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | 液体PFの状態発行（`IBlockStateObservable` 実装） | `Game.Block` / `TrainPlatformFluidContainerComponent` | UniRx `Subject<Unit>`＋`FluidMachineInventoryStateDetail` | `Game.Block/Blocks/Pump/PumpFluidOutputComponent.cs:94,133-141`（変化したときだけOnNext、出力タンク1本） |
| 2 | 液体の受け渡し計算の切り出し | `Game.Block` / 新設 `TrainPlatformFluidTransfer`（static） | 純粋な計算 | 同ファイルの既存 private static `TransferFluid` |
| 3 | パイプ状態→`dto.FluidSlots` | `Client.WebUiHost` / `BlockDetailDtoBuilder` | 既存 `AppendFluidSlots` | 同ファイル `:78-85`（FluidMachineInventory分岐） |
| 4 | パイプのビュー設定 | webui `SectionStackView.tsx` の `configByBlockType` | 静的設定 | 同表の `ElectricPump` 行 |
| 5 | openable | `moorestech_master` blocks.json | マスタ値 | 液体PF（`openable: true`、インベントリ無しでも開ける） |

データフロー: （液体の増減）→ `TrainPlatformFluidContainerComponent`（書き手）→ `OnChangeBlockState` → `BlockSystem` が状態をまとめて配信 → クライアント `BlockGameObject` の状態 → `BlockDetailDtoBuilder`（読み手）→ WebUI。新しい経路は足さない。

---

### Task 1: 液体PFが自分の液体を状態として出す

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/ContainerComponents/TrainPlatformFluidTransfer.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/ContainerComponents/TrainPlatformFluidContainerComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/ContainerComponents/TrainPlatformFluidContainerSaveJsonObject.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainFluidPlatformStateDetailTest.cs`（前例 `TrainStationDockingFluidTransferTest.cs` と同じ直下に置く）

**Interfaces:**
- Produces: `TrainPlatformFluidContainerComponent : IUpdatableBlockComponent, IFluidInventory, IBlockSaveState, IBlockStateObservable`
  - `IObservable<Unit> OnChangeBlockState`
  - `BlockStateDetail[] GetBlockStateDetails()` → キー `FluidMachineInventoryStateDetail.BlockStateDetailKey`、`InputTanks` 空、`OutputTanks` 1要素
- Produces: `public static class TrainPlatformFluidTransfer`
  - `public static double Transfer(FluidContainer from, FluidContainer to)`（移した量を返す）
  - `public static double PushToAdjacent(FluidContainer container, BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>> connector)`（押し出した合計量を返す）
  - `public static bool HasIncompatibleFluids(FluidContainer platformFluid, FluidContainer trainFluid)`
  - `public static bool IsTargetContainer(TrainCar dockedCar, FluidTrainCarContainer platformContainer, out FluidTrainCarContainer fluidContainer)`（既存 private `IsTargetContainer` の移設。`platformContainer` は null 可）

- [ ] **Step 1: テストを書く**

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.TrainRail.ContainerComponents;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Fluid;
using MessagePack;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainFluidPlatformStateDetailTest
    {
        private static readonly Guid WaterGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // 空のPFは空の出力タンクを1本出す
        // An empty platform publishes one empty output tank
        [Test]
        public void EmptyPlatformPublishesOneEmptyOutputTank()
        {
            var component = PlacePlatform();
            var detail = ReadDetail(component);

            Assert.AreEqual(0, detail.InputTanks.Count);
            Assert.AreEqual(1, detail.OutputTanks.Count);
            Assert.AreEqual(FluidMaster.EmptyFluidId.AsPrimitive(), detail.OutputTanks[0].FluidId);
            Assert.AreEqual(0, detail.OutputTanks[0].Amount, 0.001);
            Assert.Greater(detail.OutputTanks[0].MaxCapacity, 0);
        }

        // 注入すると量が状態に載り、変化通知が1回出る
        // Injecting fluid shows up in the state and fires one change notification
        [Test]
        public void AddLiquidPublishesAmountAndNotifies()
        {
            var component = PlacePlatform();
            var notifyCount = 0;
            component.OnChangeBlockState.Subscribe(_ => notifyCount++);

            var waterId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            component.AddLiquid(new FluidStack(300.0, waterId), default);

            var tank = ReadDetail(component).OutputTanks[0];
            Assert.AreEqual(waterId.AsPrimitive(), tank.FluidId);
            Assert.AreEqual(300.0, tank.Amount, 0.001);
            Assert.AreEqual(1, notifyCount);
        }

        // 満杯で何も入らなかったときは通知しない
        // No notification when nothing is accepted because the tank is full
        [Test]
        public void RejectedAddLiquidDoesNotNotify()
        {
            var component = PlacePlatform();
            var waterId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var capacity = ReadDetail(component).OutputTanks[0].MaxCapacity;
            component.AddLiquid(new FluidStack(capacity, waterId), default);

            var notifyCount = 0;
            component.OnChangeBlockState.Subscribe(_ => notifyCount++);
            component.AddLiquid(new FluidStack(10.0, waterId), default);

            Assert.AreEqual(0, notifyCount);
        }

        #region Helper

        private static TrainPlatformFluidContainerComponent PlacePlatform()
        {
            var env = TrainTestHelper.CreateEnvironment();
            var (platformBlock, _) = TrainTestHelper.PlaceBlockWithRailComponents(env, ForUnitTestModBlockId.TestTrainFluidPlatform, Vector3Int.zero, BlockDirection.North);
            return platformBlock.GetComponent<TrainPlatformFluidContainerComponent>();
        }

        private static FluidMachineInventoryStateDetail ReadDetail(TrainPlatformFluidContainerComponent component)
        {
            var details = component.GetBlockStateDetails();
            Assert.AreEqual(1, details.Length);
            Assert.AreEqual(FluidMachineInventoryStateDetail.BlockStateDetailKey, details[0].Key);
            return MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(details[0].Value);
        }

        #endregion
    }
}
```

（`BlockStateDetail` は `Game.Block.Interface/Component/IBlockStateDetail.cs` の struct で `Key`/`Value` を持つ。ヘルパーの `#region Helper` はクラス直下だが名前が Internal ではないので規約上可）

- [ ] **Step 2: 受け渡し計算を `TrainPlatformFluidTransfer.cs` へ切り出す**

既存の `TransferFluid`・`PushFluidToAdjacentBlocks`・`HasIncompatibleFluids` の本体をそのまま移し、移した量を返すようにする。

```csharp
using System;
using Core.Master;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Fluid;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    /// <summary>
    /// 液体PFの液体受け渡し計算（PF⇔貨車・PF→隣接ブロック）
    /// Fluid transfer math for the fluid platform (platform⇔train car, platform→adjacent blocks)
    /// </summary>
    public static class TrainPlatformFluidTransfer
    {
        public static double Transfer(FluidContainer from, FluidContainer to)
        {
            if (from.Amount < double.Epsilon) return 0;

            var fluidStack = new FluidStack(from.Amount, from.FluidId);
            var transferred = to.AddLiquid(fluidStack).AcceptedAmount;
            from.Amount -= transferred;

            if (from.Amount < double.Epsilon)
            {
                from.FluidId = FluidMaster.EmptyFluidId;
            }
            return transferred;
        }

        public static double PushToAdjacent(FluidContainer fluidContainer, BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>> connector)
        {
            if (fluidContainer.Amount < double.Epsilon) return 0;

            // 接続先ごとに流量上限まで押し出す
            // Push up to each connector's flow capacity
            var totalTransferred = 0.0;
            foreach (var (inventory, info) in connector.ConnectedTargets)
            {
                if (fluidContainer.Amount < double.Epsilon) break;

                var transferAmount = Math.Min(fluidContainer.Amount, GetFlowRate(info) * GameUpdater.SecondsPerTick);
                if (transferAmount < double.Epsilon) continue;

                var remain = inventory.AddLiquid(new FluidStack(transferAmount, fluidContainer.FluidId), info);
                var transferred = transferAmount - remain.Amount;
                if (transferred > 0)
                {
                    fluidContainer.Amount -= transferred;
                    totalTransferred += transferred;
                }
            }

            if (fluidContainer.Amount < double.Epsilon)
            {
                fluidContainer.FluidId = FluidMaster.EmptyFluidId;
            }
            return totalTransferred;

            #region Internal

            double GetFlowRate(ConnectedInfo info)
            {
                if (info.SelfConnector is IFluidConnector fluidConnector)
                {
                    return fluidConnector.Option.FlowCapacity;
                }
                throw new ArgumentException("FluidConnectOption is not set on connector");
            }

            #endregion
        }

        public static bool HasIncompatibleFluids(FluidContainer platformFluid, FluidContainer trainFluid)
        {
            if (platformFluid.Amount < double.Epsilon || trainFluid.Amount < double.Epsilon) return false;
            return platformFluid.FluidId != trainFluid.FluidId;
        }
    }
}
```

（`using` は元ファイルが使っていたものに合わせる。`ConnectedInfo`・`IFluidConnector` の名前空間は元ファイルの `using` を写す）

- [ ] **Step 3: `TrainPlatformFluidContainerComponent` を状態発行に対応させる**

1. クラス宣言に `IBlockStateObservable` を足し、フィールドとプロパティを足す:
```csharp
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;
        private readonly Subject<Unit> _onChangeBlockState = new();
```
2. `Update()` の `PushFluidToAdjacentBlocks();` を次へ置き換え、旧 `PushFluidToAdjacentBlocks` メソッドは削除する:
```csharp
            // 隣接ブロックへ押し出し、量が変わったら通知する
            // Push to adjacent blocks and notify when the amount changed
            if (Container != null && 0 < TrainPlatformFluidTransfer.PushToAdjacent(Container.Container, _fluidConnector)) NotifyFluidChanged();
```
3. `AddLiquid` を次にする:
```csharp
        public FluidStack AddLiquid(FluidStack fluidStack, ConnectedInfo connectedInfo)
        {
            if (Container == null) Container = new FluidTrainCarContainer(new FluidContainer(_capacity));

            var result = Container.Container.AddLiquid(fluidStack);
            if (0 < result.AcceptedAmount) NotifyFluidChanged();
            return result.Remainder;
        }
```
4. `LoadFluidToTrain` / `UnloadFluidFromTrain`: 貨車とコンテナを入れ替える分岐（`Container = null;` / `Container = trainContainer;` の直後）で `NotifyFluidChanged();` を呼ぶ。`TransferFluid(a, b);` は `if (0 < TrainPlatformFluidTransfer.Transfer(a, b)) NotifyFluidChanged();` に置き換え、旧 `TransferFluid` は削除する。
5. `HasIncompatibleFluids(FluidTrainCarContainer)` の本体を次にする:
```csharp
            if (Container == null) return false;
            return TrainPlatformFluidTransfer.HasIncompatibleFluids(Container.Container, trainFluidContainer.Container);
```
6. 状態の出力と通知を足す:
```csharp
        public BlockStateDetail[] GetBlockStateDetails()
        {
            // PF自身の液体を出力タンク1本として配信する（ポンプと同じ形）
            // Publish the platform's own fluid as a single output tank (same shape as the pump)
            var tank = Container == null
                ? new FluidMessagePack(FluidMaster.EmptyFluidId, 0, _capacity)
                : new FluidMessagePack(Container.Container.FluidId, Container.Container.Amount, Container.Container.Capacity);
            var detail = new FluidMachineInventoryStateDetail(new List<FluidMessagePack>(), new List<FluidMessagePack> { tank });
            return new[] { new BlockStateDetail(FluidMachineInventoryStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail)) };
        }

        private void NotifyFluidChanged()
        {
            _onChangeBlockState.OnNext(Unit.Default);
        }
```
容量はコンテナがあればそのコンテナの容量を出す（貨車から受け取ったコンテナは貨車側の容量を持つため、PFのマスタ容量で割ると量が容量を超えて見える）。`using` に `Game.Block.Interface.State`・`MessagePack`・`UniRx` を足す。

- [ ] **Step 4: さらに分割して200行未満にする（必ず行う）**

Step 3 までだと約233行になる（予測レビューで実測）。次の2つを両方行う。
1. `IsTargetContainer` を `TrainPlatformFluidTransfer.IsTargetContainer(TrainCar dockedCar, FluidTrainCarContainer platformContainer, out FluidTrainCarContainer fluidContainer)` へ移す。本体は既存のまま、`_dockingComponent.DockedTrainCar` を `dockedCar`、`HasIncompatibleFluids(f)` を `platformContainer != null && HasIncompatibleFluids(platformContainer.Container, f.Container)` に置き換える。呼び出し側は `TrainPlatformFluidTransfer.IsTargetContainer(dockedCar, Container, out var targetContainer)`。コンポーネント側の `HasIncompatibleFluids(FluidTrainCarContainer)` ラッパーは削除する（Step 3 の 5 は不要になる）。
2. ネストクラス `TrainPlatformFluidContainerSaveJsonObject` を同名のトップレベルクラスとして `TrainPlatformFluidContainerSaveJsonObject.cs` へ移す（外部からの参照は無い。`[JsonProperty("fluid")]` はそのまま）。

Run: `wc -l moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/ContainerComponents/*.cs`
Expected: すべて200未満。

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainFluidPlatformStateDetailTest|TrainStationDockingFluidTransferTest|TrainFluidPlatformSaveLoadTest|TrainBidirectionalFluidLoopSaveDataTest"`
Expected: ErrorCount 0 / 全PASS（既存3クラスは挙動不変の確認）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/ContainerComponents moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainFluidPlatformStateDetailTest.cs*
git commit -m "液体PFが自分の液体を状態として配信する"
```

### Task 2: パイプ・タンクの液体をブロックUIへ詰めて開けるようにする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs:78-85`
- Modify: `moorestech_web/webui/src/features/blockInventory/views/SectionStackView.tsx:21-33`
- Modify: `moorestech_web/webui/src/features/blockInventory/views/__tests__/SectionStackView.test.tsx`
- Modify（容量文字表示の撤去）: `moorestech_web/webui/src/features/blockInventory/details/TrainPlatformSection.tsx`, `moorestech_web/webui/src/bridge/contract/schemas/inventory.ts`, `moorestech_web/webui/e2e/mock-host/fixtures/blocks/logisticsFixtures.ts`, `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/TrainPlatformDetailDtoBuilder.cs`, `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtos.cs`, `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractPhaseB2Test.cs`, `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/block_inventory_train_fluid_platform.json`, `Localization/localization.csv`（`ui.blockInventory.trainPlatformCapacity` 行）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BlockDetail/FluidBlockDetailDtoBuilderTest.cs`
- Modify（別repo）: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（鉄のパイプ・鋼鉄のパイプ・液体タンクの `openable`）
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: Task 1 の `FluidMachineInventoryStateDetail`（液体PF）。`Game.Fluid.FluidPipeStateDetail(FluidId fluidId, float amount, float capacity)` と `BlockStateDetailKey = "FluidPipeStateData"`（既存）
- Produces: なし

- [ ] **Step 1: テストを書く**

`GearToElectricDetailDtoBuilderTest.ParentApplyIncludesGenerationStateSupplyAndMaximum` と同じ注入方法で、親の `BlockDetailDtoBuilder.Apply` を通す。

```csharp
using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Fluid;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.WebUi.BlockDetail
{
    public class FluidBlockDetailDtoBuilderTest
    {
        private static readonly System.Guid WaterGuid = System.Guid.Parse("00000000-0000-0000-1234-000000000001");

        // パイプの状態が液体スロット1本になる
        // A pipe state becomes a single fluid slot
        [Test]
        public void PipeStateFillsOneFluidSlot()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var waterId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var detail = new FluidPipeStateDetail(waterId, 40f, 100f);
            var dto = ApplyWithState(ForUnitTestModBlockId.FluidPipe, FluidPipeStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail));

            Assert.AreEqual(1, dto.FluidSlots.Count);
            Assert.AreEqual(waterId.AsPrimitive(), dto.FluidSlots[0].FluidId);
            Assert.AreEqual(40.0, dto.FluidSlots[0].Amount, 0.001);
            Assert.AreEqual(100.0, dto.FluidSlots[0].Capacity, 0.001);
            Assert.AreEqual(WaterGuid.ToString("D"), dto.FluidSlots[0].FluidGuid);
        }

        // 空のパイプは空Guidのスロットになる
        // An empty pipe becomes a slot with an empty GUID
        [Test]
        public void EmptyPipeFillsEmptyFluidSlot()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var detail = new FluidPipeStateDetail(FluidMaster.EmptyFluidId, 0f, 100f);
            var dto = ApplyWithState(ForUnitTestModBlockId.FluidPipe, FluidPipeStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail));

            Assert.AreEqual(1, dto.FluidSlots.Count);
            Assert.AreEqual("", dto.FluidSlots[0].FluidGuid);
        }

        // 液体PFの出力タンクが液体スロット1本になる
        // The fluid platform's output tank becomes a single fluid slot
        [Test]
        public void FluidPlatformStateFillsOneFluidSlot()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var waterId = MasterHolder.FluidMaster.GetFluidId(WaterGuid);
            var detail = new FluidMachineInventoryStateDetail(new List<FluidMessagePack>(), new List<FluidMessagePack> { new(waterId, 250.0, 1000.0) });
            var dto = ApplyWithState(ForUnitTestModBlockId.TestTrainFluidPlatform, FluidMachineInventoryStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(detail));

            Assert.AreEqual(1, dto.FluidSlots.Count);
            Assert.AreEqual(250.0, dto.FluidSlots[0].Amount, 0.001);
            Assert.AreEqual(1000.0, dto.FluidSlots[0].Capacity, 0.001);
        }

        private static BlockInventoryDto ApplyWithState(BlockId blockId, string key, byte[] value)
        {
            // 受信状態を実クライアントへ注入して親の合成入口を通す
            // Inject a received state into a real client object and run the parent composition entry
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var state = new BlockState(new Dictionary<string, byte[]> { { key, value } });
            var gameObject = new GameObject("FluidBlockDetailDtoBuilderTest");
            var block = gameObject.AddComponent<BlockGameObject>();
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.BlockMasterElement)).SetValue(block, master);
            typeof(BlockGameObject).GetField("_blockStateMessagePack", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(block, new BlockStateMessagePack(state, Vector3Int.zero));
            var dto = new BlockInventoryDto();
            BlockDetailDtoBuilder.Apply(dto, block, new BlockNetworkInfoCache());
            Object.DestroyImmediate(gameObject);
            return dto;
        }
    }
}
```

（`FluidSlots` 要素の型名・プロパティ名は `BlockInventoryDtos.cs` の `BlockFluidSlotDto` に合わせる。`BlockId` の名前空間が違えば実ファイルに合わせる。TestTrainFluidPlatform の DtoBuilder が他の詳細を詰める際に必要な状態が無くて例外になる場合は、そのビルダーの前例テスト `TrainStationDetailDtoBuilderTest` の注入方法に合わせて必要な状態を足す）

- [ ] **Step 2: `BlockDetailDtoBuilder.Apply` にパイプの分岐を足す**

FluidMachineInventory 分岐（`:78-85`）の直後に足す。`using Game.Fluid;` を追加する。

```csharp
            // パイプ・タンク: 単一流体の FluidPipe StateDetail を液体スロット1本にする
            // Pipes and tanks: turn the single-fluid FluidPipe state detail into one fluid slot
            var pipe = block.GetStateDetail<FluidPipeStateDetail>(FluidPipeStateDetail.BlockStateDetailKey);
            if (pipe != null)
            {
                AppendFluidSlots(dto.FluidSlots, new List<FluidMessagePack> { new(pipe.FluidId, pipe.Amount, pipe.Capacity) });
            }
```

- [ ] **Step 3: WebUIのビュー設定を足す**

`SectionStackView.tsx` の `configByBlockType` に1行足す（アイテムグリッド無し・進捗無し）:

```ts
  FluidPipe: { itemGridTestId: null, fluidRowTestId: "fluid-pipe-fluid-slots", renderEmptyGrid: false, showFluidProgress: false },
```

`views/__tests__/SectionStackView.test.tsx` に、既存テストの書き方（モックデータの作り方・render方法）に合わせて1ケース足す: `blockType: "FluidPipe"`、`fluidSlots` 1要素、`itemSlots` 空のとき、`fluid-pipe-fluid-slots` が描かれ、`generic-block-grid` が無いこと。

- [ ] **Step 3.5: 液体PFの容量文字表示を撤去する**

`TrainPlatformSection.tsx` から `capacityText`/`capacityElement`/`TRAIN_PLATFORM_CAPACITY_TEST_ID` を消す。続いて `TrainPlatformDetailDto.FluidCapacity`（`BlockDetailDtos.cs`）と、それを詰める `TrainPlatformDetailDtoBuilder.CreateFluidDetail` の引数・代入、webui スキーマ `inventory.ts` の `fluidCapacity`、fixture 2本（`logisticsFixtures.ts`・`block_inventory_train_fluid_platform.json`）の `fluidCapacity`、`WireContractPhaseB2Test.cs` の該当アサート、`Localization/localization.csv` の `ui.blockInventory.trainPlatformCapacity` 行を消す。`CreateFluidDetail` が他の値を詰めなくなったら貨物PFと同じ生成へ畳む。
Run: `grep -rn "fluidCapacity\|FluidCapacity\|trainPlatformCapacity" moorestech_client/Assets/Scripts moorestech_web/webui/src moorestech_web/webui/e2e Localization`
Expected: 0件（i18n の生成キー `L.ui...` が自動生成なら生成手順を `moorestech_web/webui` の README に従って回す）

- [ ] **Step 4: マスタの openable を true にする（別repo）**

```bash
cd /Users/sakastudio/hermes-agent/data/repos/moorestech_master
git fetch origin
git worktree add -b feat/fluid-block-ui-openable /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/fluid-block-ui origin/master
```
`server_v8/mods/moorestechAlphaMod_8/master/blocks.json` で `name` が「鉄のパイプ」「鋼鉄のパイプ」「液体タンク」の3要素の `"openable": false` を `true` にする（JSONの整形はファイル既存の形を保つ。python等で全体を書き戻さず、該当3行だけ書き換える）。

```bash
cd /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/fluid-block-ui
git commit -am "パイプとタンクのブロックUIを開けるようにする"
git push -u origin feat/fluid-block-ui-openable
gh pr create --base master --title "パイプとタンクのブロックUIを開けるようにする" --body "moorestech 本体の feat/fluid-block-ui と対。鉄のパイプ・鋼鉄のパイプ・液体タンクの openable を true にする。"
git rev-parse HEAD
```
本repoの `.moorestech-external-revisions.json` の `moorestech_master` の `commitHash` を上の `git rev-parse HEAD` の値にする。ピンの反映手順（pinワークツリーの作り直し等）は `docs/development/worktree-and-review-pitfalls.md` に従う。

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.WebUi.BlockDetail.FluidBlockDetailDtoBuilderTest"`
Run: `cd moorestech_web/webui && npx vitest run src/features/blockInventory src/bridge`
Expected: ErrorCount 0 / 全PASS

- [ ] **Step 6: 実画面で確かめる**

Step 4 のピン反映（masterデータの読み込み先が新しいコミットになっていること）が済んでから行う。uloop で PlayMode を起動し、`uloop execute-dynamic-code` でプレイヤーの近くに液体タンクを置いて水を注入（サーバーの `FluidPipeComponent.AddLiquid`）し、タンクをクリックしてUIを開いた状態を `uloop screenshot` で撮る。同様に液体PFへ `AddLiquid` してUIを開き撮る。確認点: 液体アイコン・名前・量/容量が出ている／パイプのUIにアイテムグリッドが出ていない／液体PFの積む・降ろす切り替えが残っている。スクショのパスを報告に書く。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts moorestech_web/webui/src moorestech_web/webui/e2e Localization/localization.csv .moorestech-external-revisions.json
git commit -m "パイプ・タンク・液体PFのブロックUIに液体を表示する"
```

### Task 3: 液体貨車をFで開いたら液体を出す

`creating-server-protocol` スキルを先に読む（既存プロトコルの拡張とイベント種別の追加に当たる）。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/InventoryRequestProtocol.cs`（応答に `Fluids`、液体貨車は Success）
- Modify: `moorestech_server/Assets/Scripts/Server.Util/MessagePack/InventoryEventType.cs`（`Refresh = 2`）
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/UnifiedInventoryEvent/UnifiedInventoryEventPacket.cs`（`CreateRefresh`）
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/UnifiedInventoryEvent/TrainInventoryUpdateService.cs`（取り直し通知の配信）
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Event/ITrainUpdateEvent.cs`, `moorestech_server/Assets/Scripts/Game.Train/Event/TrainUpdateEvent.cs`（`OnContentRefreshRequested` / `InvokeContentRefresh`）
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/Containers/FluidTrainCarContainer.cs`（載っている貨車を覚え、変化を通知）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/ContainerComponents/TrainPlatformFluidTransfer.cs`, `TrainPlatformFluidContainerComponent.cs`（貨車側の液体が変わったら通知）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/Responses.cs`, `moorestech_client/Assets/Scripts/Client.Network/API/Requests/InventoryQueryApi.cs`（`InventoryResponse.Fluids`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/SubInventoryModel.cs`（`Fluids` / `SetFluids`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/TrainSubInventorySource.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventoryState.cs`（Refresh で取り直し。194行のため分割する）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/SubInventoryLoader.cs`（取得→モデル生成→登録→通知の1本）
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/FluidSlotDtoBuilder.cs`（`AppendFluidSlots` を移す）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs`, `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/TrainInventoryDtoFactory.cs`
- Modify: `moorestech_web/webui/src/features/blockInventory/train/TrainInventoryBody.tsx`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RequestBlockInventoryTest/RequestTrainInventoryTest.cs`（ケース追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RequestBlockInventoryTest/TrainFluidRefreshEventTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/TrainInventoryFluidDtoTest.cs`

**Interfaces:**
- Consumes: Task 1 の `TrainPlatformFluidTransfer.Transfer` / `TrainPlatformFluidContainerComponent` の `LoadFluidToTrain`・`UnloadFluidFromTrain`
- Produces（server）:
  - `ResponseInventoryRequestProtocolMessagePack.Fluids : FluidMessagePack[]`（`[Key(6)]`。アイテム貨車・失敗応答は空配列）
  - `ResponseInventoryRequestProtocolMessagePack.CreateSuccess(InventoryType inventoryType, InventoryIdentifierMessagePack identifier, IReadOnlyList<IItemStack> items, IReadOnlyList<FluidMessagePack> fluids)`
  - `InventoryEventType.Refresh = 2`、`UnifiedInventoryEventMessagePack.CreateRefresh(ISubInventoryIdentifier identifier)`（Slot -1, Item null）
  - `ITrainUpdateEvent.OnContentRefreshRequested : IObservable<TrainCarInstanceId>`、`TrainUpdateEvent.InvokeContentRefresh(TrainCarInstanceId)`
  - `FluidTrainCarContainer.NotifyFluidChanged()`（載っている貨車があれば取り直し通知を出す）
- Produces（client）:
  - `InventoryResponse.Fluids : List<FluidMessagePack>`
  - `SubInventoryModel.Fluids : List<FluidMessagePack> { get; private set; }`、`SubInventoryModel.SetFluids(IReadOnlyList<FluidMessagePack>)`
  - `FluidSlotDtoBuilder.Append(List<BlockFluidSlotDto> slots, IEnumerable<FluidMessagePack> tanks)`（`BlockDetailDtoBuilder.AppendFluidSlots` の移設。中身は同じ）

- [ ] **Step 1: サーバーのテストを書く**

`RequestTrainInventoryTest` に `TrainInventoryRequestWithFluidContainer` を足す。既存 `TrainInventoryRequest` と同じ組み立てで、車両だけ `TrainTestCarFactory.CreateTrainCarWithFluidContainer(0, 400000, 1000, distance, true)` にし、返る `fluidContainer` に水 300 を `AddLiquid` してから要求する。期待: `Result == Success`、`Items.Length == 0`、`Fluids.Length == 1`、`Fluids[0].FluidId == 水のFluidId`、`Fluids[0].Amount == 300`、`Fluids[0].MaxCapacity == 1000`。既存 `TrainInventoryRequest` に `Assert.AreEqual(0, data.Fluids.Length)` を足す。

`TrainFluidRefreshEventTest`: 液体PF（`ForUnitTestModBlockId.TestTrainFluidPlatform`）に水を入れ、液体貨車をドッキングさせ（`TrainStationDockingFluidTransferTest.FluidPlatformTransfersFluidToDockedTrainCar` と同じ組み立て）、プレイヤーがその貨車の在庫を購読した状態（`SubscribeInventoryProtocol` を `RequestTrainInventoryTest` の購読前例どおりに送る）で、転送が終わるまで Update を回す。期待: そのプレイヤー宛てのイベントに `UnifiedInventoryEventPacket.EventTag` の `EventType == InventoryEventType.Refresh` が1件以上あり、`Identifier` がその貨車。購読していない別プレイヤーには来ない。イベントの取り出しは既存の統合在庫イベントのテスト（`grep -rln "UnifiedInventoryEventMessagePack" moorestech_server/Assets/Scripts/Tests` で探す）の書き方に合わせる。

- [ ] **Step 2: サーバーを実装する**

1. `InventoryEventType` に `Refresh = 2` を足す（`/// 開いている在庫を取り直させるイベント` の日英 summary を既存に倣って付ける）。
2. `UnifiedInventoryEventMessagePack.CreateRefresh(identifier)` を `CreateRemove` と同じ形で足す（EventType だけ Refresh）。
3. `ITrainUpdateEvent` に `IObservable<TrainCarInstanceId> OnContentRefreshRequested { get; }`、`TrainUpdateEvent` に `Subject` と `public void InvokeContentRefresh(TrainCarInstanceId id)` を `OnTrainCarRemoved` と同じ形で足す。
4. `TrainInventoryUpdateService` で `trainUpdateEvent.OnContentRefreshRequested.Subscribe(OnTrainContentRefresh)` し、`OnTrainCarRemoved` と同じ形で `CreateRefresh(identifier)` を加入者へ送る。
5. `FluidTrainCarContainer`:
```csharp
        private TrainCar _attachedCar;

        public void OnAttachedToCar(TrainCar trainCar)
        {
            _attachedCar = trainCar;
            NotifyFluidChanged();
        }

        public void OnDetachedFromCar()
        {
            // 外れる前の貨車へ取り直しを送ってから手放す
            // Ask the car being left to refresh before letting go of it
            NotifyFluidChanged();
            _attachedCar = null;
        }

        public void NotifyFluidChanged()
        {
            if (_attachedCar == null) return;
            ((TrainUpdateEvent)ServerContext.GetService<ITrainUpdateEvent>()).InvokeContentRefresh(_attachedCar.TrainCarInstanceId);
        }
```
`_attachedCar == null` の早期 return は「PFに置かれている容器は貨車の画面を持たない」ためで、通知先が無いだけなので黙ってよい。行に `// silent-ok: 貨車に載っていない容器には開いている貨車画面が無い / 液体PF側は自分の OnChangeBlockState で通知済み` を付ける。キャスト取得は `TrainCar.cs:73` の前例どおり。
6. 液体PFの受け渡しで貨車側の量が変わったら通知する: `LoadFluidToTrain`・`UnloadFluidFromTrain` の `if (0 < TrainPlatformFluidTransfer.Transfer(a, b)) NotifyFluidChanged();` を `{ NotifyFluidChanged(); trainContainer.NotifyFluidChanged(); }` にする（容器の付け外しは `SetContainer` 経由で 5 が通知する）。
7. `InventoryRequestProtocol`:
   - `ResponseInventoryRequestProtocolMessagePack` に `[Key(6)] public FluidMessagePack[] Fluids { get; set; }` を足し、コンストラクタに `IReadOnlyList<FluidMessagePack> fluids` 引数を足して `Fluids = fluids.ToArray()`。既存のデフォルト引数 `result = InventoryRequestResult.Success` は外し、全呼び出しで明示する。`CreateSuccess` に `fluids` 引数を足す。失敗系の Create は `Array.Empty<FluidMessagePack>()` を渡す。
   - `CreateTrainResponse` の分岐を次にする:
```csharp
                // アイテム貨車はスロット、液体貨車は液体1本を返す
                // Item cars return slots; fluid cars return their single fluid
                if (trainCar.Container is ItemTrainCarContainer)
                    return ResponseInventoryRequestProtocolMessagePack.CreateSuccess(InventoryType.Train, identifier, _inventoryResolver.Resolve(identifier, requesterPlayerId).InventoryItems.ToArray(), Array.Empty<FluidMessagePack>());
                if (trainCar.Container is FluidTrainCarContainer fluidCar)
                {
                    var fluid = new FluidMessagePack(fluidCar.Container.FluidId, fluidCar.Container.Amount, fluidCar.Container.Capacity);
                    return ResponseInventoryRequestProtocolMessagePack.CreateSuccess(InventoryType.Train, identifier, Array.Empty<IItemStack>(), new[] { fluid });
                }
                return ResponseInventoryRequestProtocolMessagePack.CreateContainerNotFound(InventoryType.Train, identifier);
```
   - ブロック側の `CreateSuccess` 呼び出しは `Array.Empty<FluidMessagePack>()` を渡す。

- [ ] **Step 3: サーバーをコンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RequestTrainInventoryTest|TrainFluidRefreshEventTest|RequestBlockInventory|UnifiedInventory|TrainStationDockingFluidTransferTest"`
Expected: ErrorCount 0 / 全PASS

- [ ] **Step 4: クライアントのテストを書く**

`TrainInventoryFluidDtoTest`: `SubInventoryModel` に `SetFluids` で水 300/1000 を1本入れ、`TrainSubInventorySource`（`new TrainSubInventorySource(new TrainCarInstanceId(1), <ITrainCarViewRegistry の既存テスト用実装または null>)`）と合わせて `TrainInventoryDtoFactory.Create` を呼び、`dto.FluidSlots.Count == 1`・`Amount == 300`・`Capacity == 1000`・`FluidGuid == 水のGuid` を確かめる。`Create` が要求する `TrainUnitClientCache` などの組み立ては `grep -rln "TrainInventoryDtoFactory.Create" moorestech_client/Assets/Scripts/Client.Tests` の既存テストに合わせる。既存テストが無ければ、`TrainTimetableDtoBuilder.Build` が null 許容かを読んで最小の実体を渡す。もう1ケース: `TrainSubInventorySource.CreateModel` に `Fluids` 1本・`Result = Success` の `InventoryResponse` を渡すと `model.Fluids.Count == 1`。

- [ ] **Step 5: クライアントを実装する**

1. `InventoryResponse` に `public List<FluidMessagePack> Fluids { get; }` とコンストラクタ引数を足し、`InventoryQueryApi.cs:35` で `response.Fluids.ToList()` を渡す。
2. `SubInventoryModel` に `public List<FluidMessagePack> Fluids { get; private set; } = new();` と `public void SetFluids(IReadOnlyList<FluidMessagePack> fluids) { Fluids = new List<FluidMessagePack>(fluids); }`。
3. `TrainSubInventorySource.CreateModel` の Success 分岐で `model.SetFluids(inventoryResponse.Fluids);`。
4. `BlockDetailDtoBuilder.AppendFluidSlots` を `FluidSlotDtoBuilder.Append` へ移し（中身はそのまま、`public static`）、`BlockDetailDtoBuilder` の呼び出しを置き換える。`TrainInventoryDtoFactory.Create` でアイテムの後に `FluidSlotDtoBuilder.Append(dto.FluidSlots, inventory.Fluids);`。
5. `SubInventoryState`: `OnEnter` 内のローカル関数 `LoadInventory` の本体（取得→`CreateModel`→`SetSubInventory`→`_onSubInventoryUpdated.OnNext`）を `SubInventoryLoader` へ移し、OnEnter と Refresh の両方から呼ぶ。購読（`SubscribeInventory(..., true)`）は OnEnter 側だけに残す。`OnUnifiedInventoryEvent` に分岐を足す:
```csharp
            else if (packet.EventType == InventoryEventType.Refresh)
            {
                // 中身の形が変わったので開いたときと同じ要求で取り直す
                // The contents changed shape, so refetch with the same request used on open
                _loader.Load(CurrentSubInventorySource, _loadInventoryCts.Token).Forget();
            }
```
`SubInventoryLoader` の形は `public async UniTask Load(ISubInventorySource source, CancellationToken ct)` とし、結果のモデルは `SubInventoryState` が受け取って `CurrentSubInventory` に入れる（戻り値 `UniTask<SubInventoryModel>` にして呼び出し側で代入・登録・通知する形でもよい。どちらでも `SubInventoryState.cs` が200行未満になること）。取り直し中に画面を閉じたら `_loadInventoryCts` のキャンセルで止まる（OnExit の既存処理）。
6. `TrainInventoryBody.tsx` のインベントリタブで、`data.fluidSlots.length > 0` のときアイテムグリッドの代わりに `<FluidSlotRow fluids={data.fluidSlots.map((fluid) => ({ fluid }))} progress={null} testId="train-inventory-fluids" />` を出す（`TrainPlatformInventory.tsx:21-23` と同じ書き方）。

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainInventoryFluidDtoTest|FluidBlockDetailDtoBuilderTest|SubInventory|TrainInventory"`
Run: `cd moorestech_web/webui && npx vitest run src/features/blockInventory src/bridge`
Run: `wc -l moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventoryState.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/InventoryRequestProtocol.cs`
Expected: ErrorCount 0 / 全PASS / 両ファイル200行未満

- [ ] **Step 7: 実画面で確かめる**

Task 2 Step 6 と同じPlayModeで、液体貨車を置いて液体PFから水を積ませ、貨車をFで開いて `uloop screenshot` で撮る。確認点: 液体のアイコン・名前・量/容量が出ている／「この列車にはアイテムコンテナがありません」が出ていない／開いたまま積み込みが進むと量が変わる（2枚撮る）。

- [ ] **Step 8: コミットする**

```bash
git add moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts moorestech_web/webui/src
git commit -m "液体貨車の在庫画面に液体を表示する"
```

### Task 4: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

### Task 5: PR作成

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。PR本文に moorestech_master 側PRのURLを書く。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0083-fluid-pipe-tank-platform-block-ui.md`（決定6・7で液体貨車の画面を追加）、`.decisions/2026-10-18-パイプタンク液体PFのUIはWebUIに液体名と量容量の表示だけ足す.md`
- 液体PFの状態は新しいStateDetail型を作らず、ポンプと同じ `FluidMachineInventoryStateDetail`（出力タンク1本）で出す。クライアントの既存の液体スロット詰め経路がそのまま使えるため。出所: agent前提（`PumpFluidOutputComponent.cs:133-141` の前例）
- 液体PFの変化通知は量が変わった操作の直後に出す（`Update()` で毎tick値を比べない）。出所: agent前提（AGENTS.md「状態変化の検知は購読で」、ポンプ `:94` の前例）
- 液体PFの容量表示はコンテナがあればコンテナ自身の容量を出す。貨車から受け取ったコンテナは貨車側の容量を持つため。出所: agent前提（`UnloadFluidFromTrain` の `Container = trainContainer`）
- `TrainPlatformFluidContainerComponent.cs`（269行）は200行規約のため、受け渡し計算を `TrainPlatformFluidTransfer` へ切り出す。出所: agent前提（AGENTS.md 200行規約）
- 液体PFの「容量: N」文字表示とDTOの `FluidCapacity` は撤去し、スロットの量/容量へ一本化する。二重表示になり、貨車由来コンテナでは値が食い違うため。出所: agent前提（user-simulator予測レビューのWarningを推奨既定で採用。ユーザー委任「細部は推奨既定で」）
- 新設ヘルパ内の空戻し判定（`Amount < double.Epsilon` → `EmptyFluidId`）と「量が変わったら通知」の繰り返しはまとめない。ポンプ・機械と同じ書き方を保つため。出所: シミュレーター予測（確信中）→拒否権つき前提としてユーザーへ提示 2026-10-18
- 本PR外のリファクタ提案（PR本文の「本PR外のリファクタ提案」節に書く。タスクにはしない）: 隣接ブロックへの押し出しループ（`GetFlowRate` 込み）が `PumpFluidOutputComponent.Update()`・`VanillaMachineFluidInventoryComponent.TransferFromMachineToPipes()`・新設 `TrainPlatformFluidTransfer.PushToAdjacent` の3か所で同形。
- 貨車の液体の同期は、初期データ＝在庫要求の応答に `Fluids`、変化＝統合在庫イベントの `Refresh`（取り直し）にする。差分イベントにしないのは、貨車の液体が液体PFの受け渡しでしか変わらず頻度が低く、容器の付け外し（アイテム⇔液体⇔無し）も同じ取り直しで閉じられるため。出所: agent前提（ADR 0083 決定7）
- `FluidTrainCarContainer` からの通知は `TrainCar` を経由せず、`TrainCar.cs:73` と同じキャストで `TrainUpdateEvent` を直接呼ぶ。`TrainCar.cs` は251行で、触ると分割が要るため。出所: agent前提
- `SubInventoryState.cs`（194行）は取り直しの分岐で200行を超えるので、読み込み処理を `SubInventoryLoader` へ出す。出所: agent前提（200行規約）
- unityプレイ録画テストは入れない。表示は既存の液体スロット部品と既存のUIを開く経路（液体PFで実績あり）に乗るだけで、新しい入力・遷移を作らないため。代わりに Task 2 Step 6 で PlayMode の実画面スクショを撮る。出所: agent前提
