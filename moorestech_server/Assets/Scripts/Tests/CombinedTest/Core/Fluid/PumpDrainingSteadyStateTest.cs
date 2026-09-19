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
using static Tests.CombinedTest.Core.Fluid.PumpStateDetailTestUtil;

namespace Tests.CombinedTest.Core.Fluid
{
    /// <summary>
    ///     満杯×下流消費が遅い定常域
    ///     稼働ラベルの振動を検証
    ///     要求を搬出量へ按分し振動しないことを検証
    ///     Full tank, slow-downstream steady state
    ///     Verifies the state label does not flip
    ///     Verifies the request is prorated to the drain and does not flip
    /// </summary>
    public class PumpDrainingSteadyStateTest
    {
        private static readonly Vector3Int PipePos = new(9, 0, 0);

        // 生成量0.125/tick未満かつ待機倍率と別の按分になる値
        // Below the 0.125/tick generation and prorated apart from the idle rate
        private const double DrainPerTick = 0.03;

        [Test]
        public void 満杯でも搬出が続く間は稼働と搬出量に按分した要求を毎tick保ち搬出が止まると待機へ落ちる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var pump = PlacePoweredPump(WaterVeinPos);
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, PipePos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pipeBlock);
            var pipeNode = pipeBlock.GetComponent<FluidPipeComponent>().Node;
            var param = (Mooresmaster.Model.BlocksModule.ElectricPumpBlockParam)pump.BlockMasterElement.BlockParam;

            // 定常域の要求は搬出量÷満額生成量の按分になる
            // The steady-state request is prorated by drain over full generation
            var fullGenerationPerTick = param.GenerateFluid.items.Sum(g => g.Amount / Math.Max(0.0001, g.GenerateTime)) / GameUpdater.TicksPerSecond;
            var expectedRequest = (float)(param.RequiredPower * DrainPerTick / fullGenerationPerTick);

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

            // 定常域：搬出中は稼働+按分要求を保持
            // Steady state: draining keeps generating + the prorated request
            for (var i = 0; i < 40; i++)
            {
                pipeNode.Amount -= DrainPerTick;
                GameUpdater.UpdateOneTick();
                var common = GetCommonDetail(pump);
                Assert.AreEqual(VanillaMachineBlockStateConst.ProcessingState, common.CurrentStateType, $"搬出中のtick {i} で待機へ振動した");
                Assert.AreEqual(expectedRequest, common.RequestPower, param.RequiredPower * 0.001f, $"搬出中のtick {i} で要求電力が按分値から外れた");
            }

            // 詰まったら数tickで待機へ
            // Clogged: idle within a few ticks
            GameUpdater.RunFrames(3);
            Assert.AreEqual(VanillaMachineBlockStateConst.IdleState, GetCommonDetail(pump).CurrentStateType, "搬出が止まった満杯ポンプは待機になるはず");
        }

        [Test]
        public void 歯車ポンプも満杯でも搬出が続く間は稼働と搬出量に按分したトルク要求を毎tick保ち搬出が止まると待機へ落ちる()
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

            // 実生成レートの4割を搬出させ、要求トルクもその按分になることを見る
            // Drain 40% of the actual generation rate and expect the torque request to be prorated alike
            const double drainRatio = 0.4;
            var fullRatePerSec = pumpParam.GenerateFluid.items.Sum(g => g.Amount / Math.Max(0.0001, g.GenerateTime));
            var drainPerTick = fullRatePerSec / GameUpdater.TicksPerSecond * drainRatio;
            var expectedTorque = fullTorque * (float)drainRatio;

            var pipeNode = pipeBlock.GetComponent<FluidPipeComponent>().Node;

            // 満杯になるまで確実に生成させる
            // Generate until reliably full
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

            // 定常域：稼働+按分トルク要求を保持
            // Steady state: keep generating + the prorated torque request
            for (var i = 0; i < 40; i++)
            {
                pipeNode.Amount -= drainPerTick;
                GameUpdater.UpdateOneTick();
                Assert.IsTrue(gearPump.CanGenerateFluid, $"搬出中のtick {i} で待機へ振動した");
                Assert.AreEqual(expectedTorque, gear.GetRequiredTorque(baseRpm, true).AsPrimitive(), fullTorque * 0.001f, $"搬出中のtick {i} で要求トルクが按分値から外れた");
            }

            // 詰まったら数tickで待機へ
            // Clogged: idle within a few ticks
            GameUpdater.RunFrames(3);
            Assert.IsFalse(gearPump.CanGenerateFluid, "搬出が止まった満杯ポンプは待機になるはず");
        }
    }
}
