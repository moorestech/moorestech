using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Mooresmaster.Model.FluidInventoryConnectsModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using Game.Block.Interface.Component.ConnectJudge;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    ///     速度モデル（リープフロッグ）に基づく流体パイプのテスト。
    ///     旧pushモデルと異なり、パイプ間は「水位（充填率）が釣り合う」まで流れ、一方向パイプは許可方向にのみ流れる。
    ///
    ///     Fluid pipe tests based on the velocity (leapfrog) model.
    ///     Unlike the old push model, pipes flow until fill rates (water levels) balance, and one-way pipes flow only in the allowed direction.
    /// </summary>
    public class FluidTest
    {
        public static readonly Guid FluidGuid = new("00000000-0000-0000-1234-000000000001");
        public static FluidId FluidId => MasterHolder.FluidMaster.GetFluidId(FluidGuid);

        /// <summary>
        ///     2本のパイプで水位が均等化されることのテスト
        ///     Two pipes equalize their water levels
        /// </summary>
        [Test]
        public void FluidEqualizationTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);

            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();

            const double addingAmount = 30;
            var addingStack = new FluidStack(addingAmount, FluidId);
            var remainAmount = fluidPipe0.AddLiquid(addingStack, default);

            // fluidPipeのcapacityは100だから溢れない
            // Pipe capacity is 100, so nothing overflows
            if (remainAmount.Amount > 0) Assert.Fail();

            // 十分なtick数で減衰振動が静定する（3秒 = 60 tick）
            // Damped oscillation settles within enough ticks (3 seconds = 60 ticks)
            const int ticks = 60;
            for (var i = 0; i < ticks; i++)
            {
                GameUpdater.RunFrames(1);
            }

            // 水位が釣り合い、両パイプが15ずつ持つ
            // Levels balance out at 15 in each pipe
            Assert.AreEqual(15d, fluidPipe0.GetAmount(), 1.5d);
            Assert.AreEqual(15d, fluidPipe1.GetAmount(), 1.5d);

            // 総量は保存される
            // Total amount is conserved
            Assert.AreEqual(addingAmount, fluidPipe0.GetAmount() + fluidPipe1.GetAmount(), 0.0001d);
        }

        /// <summary>
        ///     水位差が大きい間は流量が面上限（flowCapacity×tick秒）で飽和することのテスト
        ///     While the level difference is large, flux saturates at the face cap (flowCapacity times seconds per tick)
        /// </summary>
        [Test]
        public void RealtimeFluidTransportTest()
        {
            const double amount = 30d;
            const double pipeFlowCapacity = 10d;

            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);

            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();

            var addingStack = new FluidStack(amount, FluidId);
            var remainAmount = fluidPipe0.AddLiquid(addingStack, default);
            if (remainAmount.Amount > 0) Assert.Fail();

            // 水位差が十分大きい20tickの間は、毎tickちょうど面上限だけ流れる
            // For the first 20 ticks the level difference stays large, so exactly the face cap flows each tick
            const int saturatedTicks = 20;
            var flowPerTick = pipeFlowCapacity * GameUpdater.SecondsPerTick;
            for (var tick = 0; tick < saturatedTicks; tick++)
            {
                GameUpdater.RunFrames(1);

                var transported = flowPerTick * (tick + 1);
                Assert.AreEqual(amount - transported, fluidPipe0.GetAmount(), 0.01d);
                Assert.AreEqual(transported, fluidPipe1.GetAmount(), 0.01d);
            }
        }

        /// <summary>
        ///     FluidPipeを設置できるかテスト
        ///     Test to set FluidPipe
        /// </summary>
        [Test]
        public void SetFluidPipeTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var blockFactory = ServerContext.BlockFactory;

            var fluidPipeBlock = blockFactory.Create(
                ForUnitTestModBlockId.FluidPipe,
                new BlockInstanceId(int.MaxValue),
                new BlockPositionInfo(
                    Vector3Int.zero,
                    BlockDirection.North,
                    Vector3Int.one
                )
            );

            // FluidPipeComponentの存在を確認
            // Check the existence of FluidPipeComponent
            Assert.True(fluidPipeBlock.ExistsComponent<FluidPipeComponent>());
        }

        [Test]
        public void FluidPipeConnectTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);

            BlockConnectorComponent<IFluidInventory, DefaultConnectJudge> fluidPipeConnector0 = fluidPipeBlock0.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultConnectJudge>>();
            BlockConnectorComponent<IFluidInventory, DefaultConnectJudge> fluidPipeConnector1 = fluidPipeBlock1.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultConnectJudge>>();

            // パイプ同士が接続されているかのテスト
            // Test if the pipes are connected
            KeyValuePair<IFluidInventory, ConnectedInfo> connect0 = fluidPipeConnector0.ConnectedTargets.First();
            Assert.AreEqual(fluidPipeBlock1, connect0.Value.TargetBlock);

            KeyValuePair<IFluidInventory, ConnectedInfo> connect1 = fluidPipeConnector1.ConnectedTargets.First();
            Assert.AreEqual(fluidPipeBlock0, connect1.Value.TargetBlock);

            // 正しくオプションが読み込まれているかのテスト
            // Test if the options are read correctly
            var option0 = (connect0.Value.SelfConnector as IFluidConnector)?.Option;
            Assert.IsNotNull(option0);
            Assert.AreEqual(10, option0.FlowCapacity);

            var option1 = (connect0.Value.SelfConnector as IFluidConnector)?.Option;
            Assert.IsNotNull(option1);
            Assert.AreEqual(10, option1.FlowCapacity);
        }

        /// <summary>
        ///     一方向パイプが逆方向へ流さないことのテスト
        ///     A one-way pipe never lets fluid flow backwards
        /// </summary>
        [Test]
        public void FlowBlockTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.OneWayFluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oneWayFluidPipeBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);

            BlockConnectorComponent<IFluidInventory, DefaultConnectJudge> oneWayFluidPipeConnector = oneWayFluidPipeBlock.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultConnectJudge>>();

            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var oneWayFluidPipe = oneWayFluidPipeBlock.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();

            // oneWayFluidPipeの東（x+）方向にしか流れない設定になっていることを確認
            // Confirm the one-way pipe only connects toward east (x+)
            {
                // 出力
                var connect = oneWayFluidPipeConnector.ConnectedTargets[fluidPipe1];
                var selfOption = (connect.SelfConnector as IFluidConnector)?.Option;
                Assert.NotNull(selfOption);
            }
            {
                // 入力
                Assert.False(oneWayFluidPipeConnector.ConnectedTargets.ContainsKey(fluidPipe0));
            }

            // 東端のパイプに注入しても、一方向パイプを逆流して西へ流れないことを確認
            // Inject into the eastmost pipe and confirm nothing flows west through the one-way pipe
            const double addingAmount = 10d;
            var remain = fluidPipe1.AddLiquid(new FluidStack(addingAmount, FluidId), default);
            Assert.AreEqual(0, remain.Amount);

            const int ticks = 100;
            for (var i = 0; i < ticks; i++)
            {
                GameUpdater.RunFrames(1);
            }

            Assert.AreEqual(0d, fluidPipe0.GetAmount(), 0.0001d);
            Assert.AreEqual(0d, oneWayFluidPipe.GetAmount(), 0.0001d);
            Assert.AreEqual(10d, fluidPipe1.GetAmount(), 0.0001d);
            Assert.AreEqual(FluidMaster.EmptyFluidId, fluidPipe0.GetFluidId());
            Assert.AreEqual(FluidMaster.EmptyFluidId, oneWayFluidPipe.GetFluidId());
        }

        /// <summary>
        ///     一方向パイプの順方向には流れ、最終的に全パイプの水位が均等化されることのテスト
        ///     Fluid flows forward through a one-way pipe and all levels eventually equalize
        /// </summary>
        [Test]
        public void OneWayForwardFlowTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.OneWayFluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oneWayFluidPipeBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);

            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var oneWayFluidPipe = oneWayFluidPipeBlock.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();

            const double addingAmount = 30d;
            fluidPipe0.AddLiquid(new FluidStack(addingAmount, FluidId), default);

            // 減衰振動が静定するまで進める（20秒 = 400 tick）
            // Advance until the damped oscillation settles (20 seconds = 400 ticks)
            const int ticks = 400;
            for (var i = 0; i < ticks; i++)
            {
                GameUpdater.RunFrames(1);
            }

            // 逆流できないため「東側ほど水位が高いか等しい」状態で静定し、概ね均等（各10前後）になる
            // Since backflow is impossible, it settles with levels non-decreasing toward east, roughly equal (around 10 each)
            Assert.AreEqual(10d, fluidPipe0.GetAmount(), 3d);
            Assert.AreEqual(10d, oneWayFluidPipe.GetAmount(), 3d);
            Assert.AreEqual(10d, fluidPipe1.GetAmount(), 3d);
            Assert.LessOrEqual(fluidPipe0.GetAmount(), oneWayFluidPipe.GetAmount() + 0.01d);
            Assert.LessOrEqual(oneWayFluidPipe.GetAmount(), fluidPipe1.GetAmount() + 0.01d);
            Assert.AreEqual(addingAmount, fluidPipe0.GetAmount() + oneWayFluidPipe.GetAmount() + fluidPipe1.GetAmount(), 0.0001d);
        }

        /// <summary>
        ///     液体の総量が一定であることのテスト
        ///     Total fluid amount stays constant
        /// </summary>
        [Test]
        public void FluidTotalAmountTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.OneWayFluidPipe, Vector3Int.right * 2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oneWayFluidPipeBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 3, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock2);

            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            var oneWayFluidPipe = oneWayFluidPipeBlock.GetComponent<FluidPipeComponent>();
            var fluidPipe2 = fluidPipeBlock2.GetComponent<FluidPipeComponent>();

            const double addingAmount = 10d;
            var addingStack = new FluidStack(addingAmount, FluidId);
            var remainAmount = fluidPipe0.AddLiquid(addingStack, default);

            Assert.AreEqual(0, remainAmount.Amount);

            var totalAmount = fluidPipe0.GetAmount() + fluidPipe1.GetAmount() + oneWayFluidPipe.GetAmount() + fluidPipe2.GetAmount();

            // tick数でループを制御（10秒 = 200 tick）
            // Loop controlled by tick count (10 seconds = 200 ticks)
            const int ticks = 200;
            for (var i = 0; i < ticks; i++)
            {
                GameUpdater.RunFrames(1);
            }

            var lastTotalAmount = fluidPipe0.GetAmount() + fluidPipe1.GetAmount() + oneWayFluidPipe.GetAmount() + fluidPipe2.GetAmount();
            Assert.AreEqual(totalAmount, lastTotalAmount, 0.0001d);
        }

        /// <summary>
        ///     中央に注入した液体が両隣へ対称に分かれ、最終的に3本の水位が均等化されることのテスト
        ///     Fluid injected at the center splits symmetrically and all three levels equalize
        /// </summary>
        [Test]
        public void FluidSplitTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            // 012 という並び
            // Layout: 0-1-2 in a row

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock2);

            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            var fluidPipe2 = fluidPipeBlock2.GetComponent<FluidPipeComponent>();

            const double addingAmount = 20d;
            var addingStack = new FluidStack(addingAmount, FluidId);
            fluidPipe1.AddLiquid(addingStack, default);

            // 静定まで進める（10秒 = 200 tick）
            // Advance until settled (10 seconds = 200 ticks)
            const int ticks = 200;
            for (var i = 0; i < ticks; i++)
            {
                GameUpdater.RunFrames(1);
            }

            // 対称に分かれ、各パイプ約6.67で均等化する
            // Splits symmetrically; each pipe settles around 6.67
            Assert.AreEqual(fluidPipe0.GetAmount(), fluidPipe2.GetAmount(), 0.0001d);
            Assert.AreEqual(addingAmount / 3d, fluidPipe0.GetAmount(), 1d);
            Assert.AreEqual(addingAmount / 3d, fluidPipe1.GetAmount(), 1d);

            // 総量をテスト
            // Verify the total amount
            Assert.AreEqual(addingAmount, fluidPipe0.GetAmount() + fluidPipe1.GetAmount() + fluidPipe2.GetAmount(), 0.0001d);
        }

        /// <summary>
        ///     スロッシング（水の揺れ戻し）が減衰して静定することの確認
        ///     Sloshing (back-and-forth motion) damps out and settles
        /// </summary>
        [Test]
        public void FluidSloshingDampingTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);

            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();

            const double amount = 10d;
            fluidPipe0.AddLiquid(new FluidStack(amount, FluidId), default);

            // 静定まで進める（10秒 = 200 tick）
            // Advance until settled (10 seconds = 200 ticks)
            for (var i = 0; i < 200; i++)
            {
                GameUpdater.RunFrames(1);
            }

            // 半々で静定し、以降は動かない
            // Settles at half-and-half and stays put afterwards
            Assert.AreEqual(5d, fluidPipe0.GetAmount(), 0.5d);
            Assert.AreEqual(5d, fluidPipe1.GetAmount(), 0.5d);

            var before0 = fluidPipe0.GetAmount();
            for (var i = 0; i < 100; i++)
            {
                GameUpdater.RunFrames(1);
            }
            Assert.AreEqual(before0, fluidPipe0.GetAmount(), 0.1d);
        }

        /// <summary>
        ///     異なる液体が混ざらないことのテスト
        ///     Different fluids never mix
        /// </summary>
        [Test]
        public void FluidMixTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);

            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();

            var fluid0Guid = Guid.Parse("00000000-0000-0000-1234-000000000001");
            var fluid0 = MasterHolder.FluidMaster.GetFluidId(fluid0Guid);
            var fluid1Guid = Guid.Parse("00000000-0000-0000-1234-000000000002");
            var fluid1 = MasterHolder.FluidMaster.GetFluidId(fluid1Guid);

            const double fluid0Amount = 10d;
            const double fluid1Amount = 20d;

            fluidPipe0.AddLiquid(new FluidStack(fluid0Amount, fluid0), default);
            fluidPipe1.AddLiquid(new FluidStack(fluid1Amount, fluid1), default);

            for (var i = 0; i < 10; i++)
            {
                // 0.1秒 = 2tick
                // 0.1 seconds = 2 ticks
                GameUpdater.RunFrames(2);

                Assert.AreEqual(fluid0Amount, fluidPipe0.GetAmount());
                Assert.AreEqual(fluid1Amount, fluidPipe1.GetAmount());
            }
        }

        /// <summary>
        ///     液体の定義に水と蒸気があることのテスト
        ///     The fluid master defines Water and Steam
        /// </summary>
        [Test]
        public void FluidMasterTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var fluidMaster = MasterHolder.FluidMaster;

            var names = fluidMaster.GetAllFluidIds().Select(id => fluidMaster.GetFluidMaster(id)).ToDictionary(f => f.Name);

            Assert.Contains("Water", names.Keys);
            Assert.Contains("Steam", names.Keys);
        }

        /// <summary>
        ///     FluidPipeのIBlockStateObservableの実装のテスト。通知はtick末尾に変化したパイプへ1回ずつバッチ発火される
        ///     IBlockStateObservable test: notifications fire batched, once per changed pipe at the tick tail
        /// </summary>
        [Test]
        public void FluidPipeNetworkTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);

            var fluidPipe0 = fluidPipeBlock0.GetComponent<FluidPipeComponent>();
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();

            var fluidId = Guid.Parse("00000000-0000-0000-1234-000000000001");
            var fluid = MasterHolder.FluidMaster.GetFluidId(fluidId);

            const double fluid0Amount = 10d;
            const double fluid1Amount = 20d;

            fluidPipe0.AddLiquid(new FluidStack(fluid0Amount, fluid), default);
            fluidPipe1.AddLiquid(new FluidStack(fluid1Amount, fluid), default);

            var callCount = 0;

            fluidPipe0.OnChangeBlockState.Subscribe(_ => { callCount++; });
            fluidPipe1.OnChangeBlockState.Subscribe(_ => { callCount++; });

            const int steps = 10;

            // 毎フレーム1tick固定なので、1tickずつ進行
            // Each frame advances exactly 1 tick
            for (var i = 0; i < steps; i++) GameUpdater.RunFrames(1);

            // 水位差がある間は毎tick両パイプの内容量が変化し、それぞれ1回ずつ通知される（2回/tick × 10tick）
            // While levels differ, both pipes change every tick and notify once each: 2 per tick over 10 ticks
            Assert.AreEqual(20, callCount);
        }
    }

    public static class FluidPipeExtension
    {
        public static double GetAmount(this FluidPipeComponent fluidPipe)
        {
            return fluidPipe.Node.Amount;
        }

        public static FluidId GetFluidId(this FluidPipeComponent fluidPipe)
        {
            return fluidPipe.Node.FluidId;
        }
    }
}
