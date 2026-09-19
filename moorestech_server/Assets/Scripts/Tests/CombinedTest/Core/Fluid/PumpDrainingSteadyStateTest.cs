using System;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
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
    ///     満杯タンク×下流の消費が生成より遅い定常域で、ポンプの稼働ラベルと要求電力がtick単位で振動しないことを検証する
    ///     Verifies the pump's state label and request do not flip per tick in the full-tank, slow-downstream steady state
    /// </summary>
    public class PumpDrainingSteadyStateTest
    {
        private static readonly Vector3Int WaterVeinPos = new(10, 0, 0);
        private static readonly Vector3Int PipePos = new(9, 0, 0);
        private static readonly Vector3Int PoleOffset = new(2, 0, 0);

        // 下流の消費量（毎tick）。TestElectricPumpの生成量 2.5/秒 = 0.125/tick より少なくする
        // Downstream consumption per tick, below the test pump's 2.5/s = 0.125/tick generation
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

            // タンク100とパイプ100を 2.5/秒 で満たすので80秒で満杯。余裕を見て90秒回す
            // Tank 100 plus pipe 100 at 2.5/s fill in 80 seconds; run 90 for margin
            for (var i = 0; i < GameUpdater.SecondsToTicks(90); i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(pipeNode.Capacity, pipeNode.Amount, 0.001, "前提: 下流パイプが満杯になっているはず");

            // 配信は前tick末にラッチした基準なので、搬出再開から2tickで稼働へ移る
            // Publishing uses the basis latched at the previous tick's end, so the pump turns generating within two ticks of draining
            for (var i = 0; i < 2; i++)
            {
                pipeNode.Amount -= DrainPerTick;
                GameUpdater.UpdateOneTick();
            }

            // 下流が生成より遅く消費し続ける定常域。搬出が続く限り稼働ラベルと満額要求を保つ
            // A downstream that keeps consuming slower than generation; while draining, the label and full request hold
            for (var i = 0; i < 40; i++)
            {
                pipeNode.Amount -= DrainPerTick;
                GameUpdater.UpdateOneTick();
                var common = GetCommonDetail(pump);
                Assert.AreEqual(VanillaMachineBlockStateConst.ProcessingState, common.CurrentStateType, $"搬出中のtick {i} で待機へ振動した");
                Assert.AreEqual(param.RequiredPower, common.RequestPower, 0.001f, $"搬出中のtick {i} で要求電力が振動した");
            }

            // 下流が完全に詰まったら搬出0になり、数tick以内に待機へ落ちる
            // Once downstream fully clogs the push drops to zero and the pump settles into idle within a few ticks
            GameUpdater.RunFrames(3);
            Assert.AreEqual(VanillaMachineBlockStateConst.IdleState, GetCommonDetail(pump).CurrentStateType, "搬出が止まった満杯ポンプは待機になるはず");
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
