using System;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using Game.Gear.Common;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.Util.ElectricNetworkReflectionTestUtil;

namespace Tests.CombinedTest.Core.Fluid
{
    /// <summary>
    ///     満杯×下流消費が遅い定常域
    ///     稼働ラベルの振動を検証
    ///     要求電力の振動を検証
    ///     Full tank, slow-downstream steady state
    ///     Verifies the state label does not flip
    ///     Verifies the request power does not flip
    /// </summary>
    public class PumpDrainingSteadyStateTest
    {
        private static readonly Vector3Int WaterVeinPos = new(10, 0, 0);
        private static readonly Vector3Int PipePos = new(9, 0, 0);
        private static readonly Vector3Int PoleOffset = new(2, 0, 0);

        // 生成量0.125/tick未満に設定
        // Set below the 0.125/tick generation rate
        private const double DrainPerTick = 0.05;

        [Test]
        public void 満杯でも搬出が続く間は稼働と満額要求を毎tick保ち搬出が止まると待機へ落ちる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pump = PlacePoweredPump();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, PipePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pipeBlock);
            var pipeNode = pipeBlock.GetComponent<FluidPipeComponent>().Node;
            var param = (Mooresmaster.Model.BlocksModule.ElectricPumpBlockParam)pump.BlockMasterElement.BlockParam;

            // 80秒で満杯、余裕見て90秒
            // Full at 80s; run 90s for margin
            for (var i = 0; i < GameUpdater.SecondsToTicks(90); i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(pipeNode.Capacity, pipeNode.Amount, 0.001, "前提: 下流パイプが満杯になっているはず");

            // 配信は前tick末にラッチした基準なので、搬出再開から2tickで稼働へ移る
            // Publishing uses the basis latched at the previous tick's end, so the pump turns generating within two ticks of draining
            for (var i = 0; i < 2; i++)
            {
                pipeNode.Amount -= DrainPerTick;
                GameUpdater.UpdateOneTick();
            }

            // 定常域：搬出中は稼働+満額要求を保持
            // Steady state: draining keeps generating + full request
            for (var i = 0; i < 40; i++)
            {
                pipeNode.Amount -= DrainPerTick;
                GameUpdater.UpdateOneTick();
                var common = GetCommonDetail(pump);
                Assert.AreEqual(VanillaMachineBlockStateConst.ProcessingState, common.CurrentStateType, $"搬出中のtick {i} で待機へ振動した");
                Assert.AreEqual(param.RequiredPower, common.RequestPower, 0.001f, $"搬出中のtick {i} で要求電力が振動した");
            }

            // 詰まったら数tickで待機へ
            // Clogged: idle within a few ticks
            GameUpdater.RunFrames(3);
            Assert.AreEqual(VanillaMachineBlockStateConst.IdleState, GetCommonDetail(pump).CurrentStateType, "搬出が止まった満杯ポンプは待機になるはず");
        }

        [Test]
        public void 歯車ポンプも満杯でも搬出が続く間は稼働と満額トルク要求を毎tick保ち搬出が止まると待機へ落ちる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            world.TryAddBlock(ForUnitTestModBlockId.GearPump, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pump);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pipeBlock);
            world.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, new Vector3Int(1, 0, 0), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _);

            var pumpParam = (GearPumpBlockParam)pump.BlockMasterElement.BlockParam;
            var gear = pump.GetComponent<GearEnergyTransformer>();
            var gearPump = pump.GetComponent<GearPumpComponent>();
            var baseRpm = new RPM((float)pumpParam.GearConsumption.BaseRpm);
            var fullTorque = GearConsumptionCalculator.CalcRequiredTorque(pumpParam.GearConsumption, baseRpm).AsPrimitive();

            // 実生成レートから遅い消費量を算出
            // Derive a slower drain from the actual generation rate
            var fullRatePerSec = pumpParam.GenerateFluid.items.Sum(g => g.Amount / Math.Max(0.0001, g.GenerateTime));
            var drainPerTick = fullRatePerSec / GameUpdater.TicksPerSecond * 0.4;

            var pipeNode = pipeBlock.GetComponent<FluidPipeComponent>().Node;

            // タンクとパイプが確実に満杯になるまで生成させる
            // Generate long enough that the tank and pipe are reliably full
            for (var i = 0; i < GameUpdater.SecondsToTicks(90); i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(pipeNode.Capacity, pipeNode.Amount, 0.001, "前提: 下流パイプが満杯になっているはず");
            Assert.IsFalse(gearPump.CanGenerateFluid, "前提: 満杯・搬出なしのポンプは待機のはず");

            // 配信は前tick末にラッチした基準なので、搬出再開から2tickで稼働へ移る
            // Publishing uses the basis latched at the previous tick's end, so the pump turns generating within two ticks of draining
            for (var i = 0; i < 2; i++)
            {
                pipeNode.Amount -= drainPerTick;
                GameUpdater.UpdateOneTick();
            }

            // 定常域：稼働+満額トルク要求を保持
            // Steady state: keep generating + full torque request
            for (var i = 0; i < 40; i++)
            {
                pipeNode.Amount -= drainPerTick;
                GameUpdater.UpdateOneTick();
                Assert.IsTrue(gearPump.CanGenerateFluid, $"搬出中のtick {i} で待機へ振動した");
                Assert.AreEqual(fullTorque, gear.GetRequiredTorque(baseRpm, true).AsPrimitive(), 0.0001f, $"搬出中のtick {i} で要求トルクが振動した");
            }

            // 詰まったら数tickで待機へ
            // Clogged: idle within a few ticks
            GameUpdater.RunFrames(3);
            Assert.IsFalse(gearPump.CanGenerateFluid, "搬出が止まった満杯ポンプは待機になるはず");
        }

        private static CommonMachineBlockStateDetail GetCommonDetail(IBlock pump)
        {
            var state = pump.GetBlockState();
            return MessagePackSerializer.Deserialize<CommonMachineBlockStateDetail>(state.CurrentStateDetails[CommonMachineBlockStateDetail.BlockStateDetailKey]);
        }

        private static IBlock PlacePoweredPump()
        {
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPump, WaterVeinPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pump);

            var polePosition = WaterVeinPos + PoleOffset;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, polePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(WaterVeinPos, polePosition);

            GameUpdater.UpdateOneTick();
            var networkDatastore = ServerContext.GetService<IElectricWireNetworkLookup>();
            Assert.IsTrue(networkDatastore.TryGetEnergySegment(pump.BlockInstanceId, out var segment));
            AddGenerator(segment, new TestElectricGenerator(new ElectricPower(10000), new BlockInstanceId(10)));
            GameUpdater.UpdateOneTick();

            return pump;
        }
    }
}
