# パイプ・タンク・液体PFのブロックUI Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** パイプ・タンク（blockType `FluidPipe`）と液体プラットフォーム（`TrainFluidPlatform`）のブロックUIに、中の液体のアイコン・名前・量/容量を出す。

**Architecture:** 表示は既存WebUIのBlockDetail（`dto.FluidSlots` → `FluidSlotRow`）をそのまま使う。液体PFはサーバー側でポンプと同じ `FluidMachineInventoryStateDetail` を状態として出し、クライアントの既存の液体スロット詰め経路に乗せる。パイプ・タンクは既存の `FluidPipeStateDetail` をクライアントで `dto.FluidSlots` へ詰め、マスタの `openable` を true にしてクリックで開けるようにする。

**Tech Stack:** Unity C#（server/client）、MessagePack、UniRx、React + Mantine（moorestech_web/webui）、マスタJSON（../moorestech_master）

## Requirements

1. 鉄のパイプ・鋼鉄のパイプ・液体タンク（blockType `FluidPipe`）をクリックするとブロックUIが開く。受け入れ: `moorestechAlphaMod_8/master/blocks.json` の3ブロックが `"openable": true`。
2. パイプ・タンクのUIに、中の液体のアイコン・名前・量/容量が1スロットで出る。空なら空スロット。受け入れ: `BlockDetailDtoBuilder.Apply` が `FluidPipeStateDetail` を持つブロックで `dto.FluidSlots` に1要素（FluidId/Amount/Capacity/FluidGuid）を詰める。WebUIでは `fluid-pipe-fluid-slots` の行に描かれ、アイテムグリッドと進捗表示は出ない。
3. 液体PFのUIに、PF自身が溜めている液体のアイコン・名前・量/容量が1スロットで出る（既存の積む/降ろす切り替えと容量表示は残す）。受け入れ: `TrainPlatformFluidContainerComponent.GetBlockStateDetails()` が `FluidMachineInventoryStateDetail`（入力0本・出力1本）を返し、クライアントの既存経路で `dto.FluidSlots` に1要素入る。
4. 液体PFの中身が変わったら開いているUIが更新される。受け入れ: 注入・隣接への押し出し・貨車との受け渡しのたびに `OnChangeBlockState` が発火する（量が変わらなかったときは発火しない）。
5. やらないこと: 液体を捨てる等の操作、ホバー表示、停車中の貨車の液体表示、ボイドパイプ、UI Toolkitでの実装、流量・方向・停止理由の表示。

## Global Constraints

- 1ファイル200行未満。`TrainPlatformFluidContainerComponent.cs` は現在269行なので、Task 1で分割して200行未満にする。
- コメントは日本語1行＋英語1行のセット。`#region Internal` はメソッド内ローカル関数だけ。
- `Func<>`・partial・try-catch・デフォルト引数は使わない。イベントはUniRx。
- `.meta` は手で作らない（Unityが生成したものはコミットしてよい）。
- 別リポジトリ `moorestech_master` の変更はpushしてPRを作り、本repoの `.moorestech-external-revisions.json` の `moorestech_master` ピンをそのPRのpush済みコミットへ向ける。
- C#を変えたら `uloop compile --project-path ./moorestech_client` を通す。
- 作業場所: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/fluid-block-ui`（ブランチ `feat/fluid-block-ui`）。最初に `pwd` で確認する。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 5配置とも前例（ポンプの状態発行・既存FluidSlots経路・SectionStackView設定表・液体PFのopenable）に一致、新しい経路なし、既存操作の死活に影響なし
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

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
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/TrainFluidPlatformStateDetailTest.cs`

**Interfaces:**
- Produces: `TrainPlatformFluidContainerComponent : IUpdatableBlockComponent, IFluidInventory, IBlockSaveState, IBlockStateObservable`
  - `IObservable<Unit> OnChangeBlockState`
  - `BlockStateDetail[] GetBlockStateDetails()` → キー `FluidMachineInventoryStateDetail.BlockStateDetailKey`、`InputTanks` 空、`OutputTanks` 1要素
- Produces: `public static class TrainPlatformFluidTransfer`
  - `public static double Transfer(FluidContainer from, FluidContainer to)`（移した量を返す）
  - `public static double PushToAdjacent(FluidContainer container, BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>> connector)`（押し出した合計量を返す）
  - `public static bool HasIncompatibleFluids(FluidContainer platformFluid, FluidContainer trainFluid)`

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

（`BlockStateDetail` のプロパティ名が `Key`/`Value` でなければ実ファイル `Game.Block.Interface/State/BlockStateDetail.cs` に合わせる。ヘルパーの `#region Helper` はクラス直下だが名前が Internal ではないので規約上可）

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

- [ ] **Step 4: 行数を確かめる**

Run: `wc -l moorestech_server/Assets/Scripts/Game.Block/Blocks/TrainRail/ContainerComponents/TrainPlatformFluidContainerComponent.cs`
Expected: 200未満。超えたら `IsTargetContainer` を `TrainPlatformFluidTransfer` へ移す（引数に `TrainCar dockedCar` と `FluidTrainCarContainer platformContainer` を取り、`out FluidTrainCarContainer` を返す形）。

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
- Modify: `moorestech_web/webui/src/features/blockInventory/views/SectionStackView.test.tsx`
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

`SectionStackView.test.tsx` に、既存テストの書き方（モックデータの作り方・render方法）に合わせて1ケース足す: `blockType: "FluidPipe"`、`fluidSlots` 1要素、`itemSlots` 空のとき、`fluid-pipe-fluid-slots` が描かれ、`generic-block-grid` が無いこと。

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
Run: `cd moorestech_web/webui && npx vitest run src/features/blockInventory/views/SectionStackView.test.tsx`
Expected: ErrorCount 0 / 全PASS

- [ ] **Step 6: 実画面で確かめる**

uloop で PlayMode を起動し、`uloop execute-dynamic-code` でプレイヤーの近くに液体タンクを置いて水を注入（サーバーの `FluidPipeComponent.AddLiquid`）し、タンクをクリックしてUIを開いた状態を `uloop screenshot` で撮る。同様に液体PFへ `AddLiquid` してUIを開き撮る。確認点: 液体アイコン・名前・量/容量が出ている／パイプのUIにアイテムグリッドが出ていない／液体PFの積む・降ろす切り替えが残っている。スクショのパスを報告に書く。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs moorestech_client/Assets/Scripts/Client.Tests/WebUi/BlockDetail/FluidBlockDetailDtoBuilderTest.cs* moorestech_web/webui/src/features/blockInventory/views/SectionStackView.tsx moorestech_web/webui/src/features/blockInventory/views/SectionStackView.test.tsx .moorestech-external-revisions.json
git commit -m "パイプ・タンク・液体PFのブロックUIに液体を表示する"
```

### Task 3: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

### Task 4: PR作成

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。PR本文に moorestech_master 側PRのURLを書く。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える。

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0083-fluid-pipe-tank-platform-block-ui.md`、`.decisions/2026-10-18-パイプタンク液体PFのUIはWebUIに液体名と量容量の表示だけ足す.md`
- 液体PFの状態は新しいStateDetail型を作らず、ポンプと同じ `FluidMachineInventoryStateDetail`（出力タンク1本）で出す。クライアントの既存の液体スロット詰め経路がそのまま使えるため。出所: agent前提（`PumpFluidOutputComponent.cs:133-141` の前例）
- 液体PFの変化通知は量が変わった操作の直後に出す（`Update()` で毎tick値を比べない）。出所: agent前提（AGENTS.md「状態変化の検知は購読で」、ポンプ `:94` の前例）
- 液体PFの容量表示はコンテナがあればコンテナ自身の容量を出す。貨車から受け取ったコンテナは貨車側の容量を持つため。出所: agent前提（`UnloadFluidFromTrain` の `Container = trainContainer`）
- `TrainPlatformFluidContainerComponent.cs`（269行）は200行規約のため、受け渡し計算を `TrainPlatformFluidTransfer` へ切り出す。出所: agent前提（AGENTS.md 200行規約）
- unityプレイ録画テストは入れない。表示は既存の液体スロット部品と既存のUIを開く経路（液体PFで実績あり）に乗るだけで、新しい入力・遷移を作らないため。代わりに Task 2 Step 6 で PlayMode の実画面スクショを撮る。出所: agent前提
