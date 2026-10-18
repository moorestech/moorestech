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
        [Test]
        public void FluidMachineOutputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            // 機械を設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.forward * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidMachineBlock);
            // 液体が入るパイプを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(-1, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock3);
            // 機械に液体を設定
            var fluidContainers = MachineFluidTestUtil.GetOutputFluidContainers(fluidMachineBlock.GetComponent<VanillaMachineBlockInventoryComponent>());
            Assert.AreEqual(2, fluidContainers.Count);
            const double fluidAmount1 = 40d;
            const double fluidAmount2 = 50d;
            fluidContainers[0].AddLiquid(new FluidStack(fluidAmount1, FluidId1));
            fluidContainers[1].AddLiquid(new FluidStack(fluidAmount2, FluidId2));
            // 機械の接続状態を確認
            var fluidMachineConnector = fluidMachineBlock.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>>>();
            Assert.AreEqual(2, fluidMachineConnector.ConnectedTargets.Count);
            // アップデート（液体が流れるのを待つ）
            var startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateOneTick();
                var elapsedTime = DateTime.Now - startTime;
                if (10 < elapsedTime.TotalSeconds) break; // 10秒待機
            }
            // 液体がパイプに転送されていることを確認
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            var fluidPipe2 = fluidPipeBlock2.GetComponent<FluidPipeComponent>();
            var fluidPipe3 = fluidPipeBlock3.GetComponent<FluidPipeComponent>();
            Assert.AreEqual(FluidId1, fluidPipe1.GetFluidId());
            Assert.AreEqual(fluidAmount1, fluidPipe1.GetAmount(), 0.01f);
            Assert.AreEqual(FluidId2, fluidPipe2.GetFluidId());
            Assert.AreEqual(fluidAmount2, fluidPipe2.GetAmount(), 0.01f);
            Assert.AreEqual(0, fluidPipe3.GetAmount()); // 接続されてないので0
            // 液体タンク側が0担っていることを確認
            Assert.AreEqual(0, fluidContainers[0].Amount, 0.01f);
            Assert.AreEqual(0, fluidContainers[1].Amount, 0.01f);
        }
    }
}
