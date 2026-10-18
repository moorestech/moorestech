using Tests.CombinedTest.Core.Fluid;
using System;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using Game.Block.Interface.Component.ConnectJudge;
using static Tests.CombinedTest.Core.Machine.MachineFluidIOTest;

namespace Tests.CombinedTest.Core.Machine
{
    public class MachineFluidOutputTest
    {
        // 流体の速度モデルが落ち着くのに十分な tick 数(ゲーム内 60 秒)
        // Enough ticks for the fluid velocity model to settle (60 in-game seconds)
        private static readonly uint FluidSettleTicks = GameUpdater.SecondsToTicks(60);

        [Test]
        public void FluidMachineOutputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.forward * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidMachineBlock);
            // 液体が入るパイプを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock3);
            var fluidContainers = MachineFluidTestUtil.GetOutputFluidContainers(fluidMachineBlock.GetComponent<VanillaMachineBlockInventoryComponent>());
            Assert.AreEqual(2, fluidContainers.Count);
            const double fluidAmount1 = 40d;
            const double fluidAmount2 = 50d;
            fluidContainers[0].AddLiquid(new FluidStack(fluidAmount1, FluidId1));
            fluidContainers[1].AddLiquid(new FluidStack(fluidAmount2, FluidId2));
            var fluidMachineConnector = fluidMachineBlock.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>>>();
            Assert.AreEqual(2, fluidMachineConnector.ConnectedTargets.Count);
            // アップデート（液体が流れるのを待つ）
            // 液体が流れ切るまで tick を進める。壁時計の待ちは速い PC でも秒数ぶん必ず払うので tick 数に置き換えた
            // Advance ticks until the fluid has flowed through; the wall-clock wait cost its full seconds even on a fast PC, so it is counted in ticks
            GameUpdater.RunFrames(FluidSettleTicks);
            // 液体がパイプに転送されていることを確認
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            var fluidPipe2 = fluidPipeBlock2.GetComponent<FluidPipeComponent>();
            var fluidPipe3 = fluidPipeBlock3.GetComponent<FluidPipeComponent>();
            Assert.AreEqual(FluidId1, fluidPipe1.GetFluidId());
            Assert.AreEqual(fluidAmount1, fluidPipe1.GetAmount(), 0.01f);
            Assert.AreEqual(FluidId2, fluidPipe2.GetFluidId());
            Assert.AreEqual(fluidAmount2, fluidPipe2.GetAmount(), 0.01f);
            Assert.AreEqual(0, fluidPipe3.GetAmount()); // 接続されてないので0
            Assert.AreEqual(0, fluidContainers[0].Amount, 0.01f);
            Assert.AreEqual(0, fluidContainers[1].Amount, 0.01f);
        }
    }
}
