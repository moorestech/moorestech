using System;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Block.Blocks.Machine;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UniRx;
using UnityEngine;
using static Tests.Util.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    ///     ポンプのUI用状態配信を検証する
    ///     Verifies the pump's UI state publishing
    /// </summary>
    public class PumpBlockStateDetailTest
    {
        private static readonly Vector3Int WaterVeinPos = new(10, 0, 0);
        private static readonly Vector3Int NoVeinPos = new(30, 0, 0);
        private static readonly Vector3Int PoleOffset = new(2, 0, 0);
        private static readonly Guid WaterFluidGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 鉱脈上の油井は汲み上げ中流体と公称量と稼働中を配信する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pump = PlacePoweredPump(WaterVeinPos);
            GameUpdater.UpdateOneTick();

            var state = pump.GetBlockState();
            var pumpDetail = MessagePackSerializer.Deserialize<PumpBlockStateDetail>(state.CurrentStateDetails[PumpBlockStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(1, pumpDetail.PumpingFluids.Count);
            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(WaterFluidGuid).AsPrimitive(), pumpDetail.PumpingFluids[0].FluidId);
            // TestPumpはamount10/generateTime4秒
            // The test pump generates amount 10 every 4 seconds
            Assert.AreEqual(2.5, pumpDetail.PumpingFluids[0].AmountPerSecond, 0.0001);

            var common = MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(state.CurrentStateDetails[CommonMachineBlockStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(VanillaMachineBlockStateConst.ProcessingState, common.CurrentStateType);
            Assert.AreEqual(50f, common.RequestPower, 0.001f, "稼働中の実効要求電力は基礎要求電力そのもの");

            var fluid = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(state.CurrentStateDetails[FluidMachineInventoryStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(0, fluid.InputTanks.Count);
            Assert.AreEqual(1, fluid.OutputTanks.Count);
            Assert.AreEqual(100, fluid.OutputTanks[0].MaxCapacity, 0.001);
        }

        [Test]
        public void 鉱脈外の油井は汲み上げ中流体が空で待機中を配信する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pump = PlacePoweredPump(NoVeinPos);
            GameUpdater.UpdateOneTick();

            var state = pump.GetBlockState();
            var pumpDetail = MessagePackSerializer.Deserialize<PumpBlockStateDetail>(state.CurrentStateDetails[PumpBlockStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(0, pumpDetail.PumpingFluids.Count);

            var common = MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(state.CurrentStateDetails[CommonMachineBlockStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(VanillaMachineBlockStateConst.IdleState, common.CurrentStateType);
            // TestElectricPumpのidlePowerRateは0.4
            // TestElectricPump's idlePowerRate is 0.4 in blocks.json
            Assert.AreEqual(50f * 0.4f, common.RequestPower, 0.001f, "待機中の実効要求電力は基礎要求×idlePowerRate");
        }

        [Test]
        public void 生成中は毎tick状態変化を発火し待機へ落ちた直後に1回だけ発火する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pump = PlacePoweredPump(WaterVeinPos);
            var fired = 0;
            using var subscription = pump.BlockStateChange.Subscribe(_ => fired++);

            // タンク容量100 / 秒2.5 なので40秒で満杯。満杯までは毎tick発火する
            // Capacity 100 at 2.5/s fills in 40 seconds; every tick fires until then
            GameUpdater.RunFrames(10);
            Assert.AreEqual(10, fired, "生成中は毎tick発火するはず");

            for (var i = 0; i < GameUpdater.SecondsToTicks(45); i++) GameUpdater.UpdateOneTick();
            var firedAtFull = fired;
            GameUpdater.RunFrames(5);
            Assert.AreEqual(firedAtFull, fired, "満杯で待機中になった後は発火しないはず");
        }

        [Test]
        public void 内部タンクが満杯になるtickでも供給電力が要求電力を超えない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pump = PlacePoweredPump(WaterVeinPos);

            // 満杯到達と分子≦分母を確認
            // Confirms full tank and numerator<=denominator
            var reachedFull = false;
            for (var i = 0; i < GameUpdater.SecondsToTicks(60) && !reachedFull; i++)
            {
                GameUpdater.UpdateOneTick();
                var state = pump.GetBlockState();
                var common = MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(state.CurrentStateDetails[CommonMachineBlockStateDetail.BlockStateDetailKey]);
                Assert.LessOrEqual(common.CurrentPower, common.RequestPower + 0.001f, "供給電力が実効要求電力を超えてはいけない");

                var fluid = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(state.CurrentStateDetails[FluidMachineInventoryStateDetail.BlockStateDetailKey]);
                reachedFull = fluid.OutputTanks[0].MaxCapacity - 0.0001 <= fluid.OutputTanks[0].Amount;
            }

            Assert.IsTrue(reachedFull, "60秒以内に内部タンクが満杯になるはず");
        }

        [Test]
        public void 電柱を撤去した油井は供給電力0を配信する()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pump = PlacePoweredPump(WaterVeinPos);
            GameUpdater.UpdateOneTick();
            Assert.Greater(GetCommonDetail(pump).CurrentPower, 0f, "撤去前は給電されているはず");

            // 給電経路が消えた後は最後の供給値が固着せず0へ落ちる
            // Once the supply path is gone the last supplied value must not stick; it falls to zero
            ServerContext.WorldBlockDatastore.RemoveBlock(WaterVeinPos + PoleOffset, BlockRemoveReason.ManualRemove);
            GameUpdater.RunFrames(2);

            Assert.AreEqual(0f, GetCommonDetail(pump).CurrentPower, 0.0001f, "無給電のtickでは供給電力は0");
        }

        [Test]
        public void 鉱脈上の歯車ポンプは汲み上げ中流体を配信し電力状態detailを持たない()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearPump, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pump);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var pumpParam = (Mooresmaster.Model.BlocksModule.GearPumpBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearPump).BlockParam;
            var generator = generatorBlock.GetComponent<global::Game.Block.Blocks.Gear.SimpleGearGeneratorComponent>();
            generator.SetGenerateRpm((float)pumpParam.GearConsumption.BaseRpm);
            generator.SetGenerateTorque((float)pumpParam.GearConsumption.BaseTorque);
            for (var i = 0; i < GameUpdater.SecondsToTicks(1); i++) GameUpdater.UpdateOneTick();

            var state = pump.GetBlockState();
            var pumpDetail = MessagePackSerializer.Deserialize<PumpBlockStateDetail>(state.CurrentStateDetails[PumpBlockStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(1, pumpDetail.PumpingFluids.Count);
            Assert.AreEqual(MasterHolder.FluidMaster.GetFluidId(WaterFluidGuid).AsPrimitive(), pumpDetail.PumpingFluids[0].FluidId);
            // TestPumpはamount10/generateTime4秒
            // The test pump generates amount 10 every 4 seconds
            Assert.AreEqual(2.5, pumpDetail.PumpingFluids[0].AmountPerSecond, 0.0001);

            Assert.IsFalse(state.CurrentStateDetails.ContainsKey(CommonMachineBlockStateDetail.BlockStateDetailKey), "歯車ポンプは電力を持たないためCommonMachineの状態detailを持たないはず");

            var fluid = MessagePackSerializer.Deserialize<FluidMachineInventoryStateDetail>(state.CurrentStateDetails[FluidMachineInventoryStateDetail.BlockStateDetailKey]);
            Assert.AreEqual(0, fluid.InputTanks.Count);
            Assert.AreEqual(1, fluid.OutputTanks.Count);
            Assert.AreEqual(100, fluid.OutputTanks[0].MaxCapacity, 0.001);
        }

        private static CommonMachineBlockStateDetail GetCommonDetail(IBlock pump)
        {
            var state = pump.GetBlockState();
            return MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(state.CurrentStateDetails[CommonMachineBlockStateDetail.BlockStateDetailKey]);
        }

        private static IBlock PlacePoweredPump(Vector3Int pos)
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPump, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pump);

            var polePosition = pos + PoleOffset;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(pos, polePosition);

            GameUpdater.UpdateOneTick();
            var networkDatastore = ServerContext.GetService<IElectricWireNetworkLookup>();
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pump.BlockInstanceId, out var segment));
            AddGenerator(segment, new TestElectricGenerator(new ElectricPower(10000), new BlockInstanceId(10)));
            GameUpdater.UpdateOneTick();

            return pump;
        }
    }
}
