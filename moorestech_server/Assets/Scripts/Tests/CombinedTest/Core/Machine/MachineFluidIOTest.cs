using Tests.CombinedTest.Core.Fluid;
using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.UnlockState;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using Game.Block.Interface.Component.ConnectJudge;
using static Tests.CombinedTest.Core.Machine.MachineInventoryTestHelper;
namespace Tests.CombinedTest.Core.Machine
{
    public class MachineFluidIOTest
    {
        public static FluidId FluidId1 => MasterHolder.FluidMaster.GetFluidId(new("00000000-0000-0000-1234-000000000001"));
        public static FluidId FluidId2 => MasterHolder.FluidMaster.GetFluidId(new("00000000-0000-0000-1234-000000000002"));
        public static FluidId FluidId3 => MasterHolder.FluidMaster.GetFluidId(new("00000000-0000-0000-1234-000000000003"));
        [Test]
        public void FluidMachineInputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            // 機械を設置し、入力液体2種のレシピを選択する（束縛前提。タンク2は束縛外）
            // Place the machine and select the recipe with 2 input fluids (binding requires this; tank 2 stays unbound)
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.forward * 0, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidMachineBlock);
            ServerContext.GetService<IGameUnlockStateDataController>().UnlockMachineRecipe(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.LockedMachineRecipe);
            MachineRecipeSelectTestUtil.SelectRecipe(fluidMachineBlock, recipe);
            // 液体を入れるパイプを設定
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock1);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(1, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock2);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(0, 0, 5), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fluidPipeBlock3);
            // パイプに液体を設定（タンク0はfluid1、タンク1はfluid2、タンク2向けのfluid3は束縛外）
            // Set up pipe fluids (tank 0 = fluid1, tank 1 = fluid2; fluid3 targets the unbound tank 2)
            const double fluidAmount1 = 50d;
            var fluidPipe1 = fluidPipeBlock1.GetComponent<FluidPipeComponent>();
            fluidPipe1.AddLiquid(new FluidStack(fluidAmount1, FluidId1), default);
            Assert.AreEqual(fluidAmount1, fluidPipe1.GetAmount());
            Assert.AreEqual(FluidId1, fluidPipe1.GetFluidId());
            const double fluidAmount2 = 40d;
            var fluidPipe2 = fluidPipeBlock2.GetComponent<FluidPipeComponent>();
            fluidPipe2.AddLiquid(new FluidStack(fluidAmount2, FluidId2), default);
            Assert.AreEqual(fluidAmount2, fluidPipe2.GetAmount());
            Assert.AreEqual(FluidId2, fluidPipe2.GetFluidId());
            const double fluidAmount3 = 30d;
            var fluidPipe3 = fluidPipeBlock3.GetComponent<FluidPipeComponent>();
            fluidPipe3.AddLiquid(new FluidStack(fluidAmount3, FluidId3), default);
            Assert.AreEqual(fluidAmount3, fluidPipe3.GetAmount());
            Assert.AreEqual(FluidId3, fluidPipe3.GetFluidId());
            // パイプの接続状態を確認
            Assert.AreEqual(1, fluidPipeBlock1.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>>>().ConnectedTargets.Count);
            Assert.AreEqual(1, fluidPipeBlock2.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>>>().ConnectedTargets.Count);
            Assert.AreEqual(1, fluidPipeBlock3.GetComponent<BlockConnectorComponent<IFluidInventory, DefaultContext<IFluidInventory>>>().ConnectedTargets.Count);
            // アップデート（液体が流れるのを待つ）
            var startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateOneTick();
                var elapsedTime = DateTime.Now - startTime;
                if (10 < elapsedTime.TotalSeconds) break; // 10秒待機
            }
            // 束縛済みタンク(0・1)は転送済み、束縛外タンク2向けのfluid3はパイプに残ることを確認
            // Bound tanks (0, 1) finish transferring; fluid3 for the unbound tank 2 stays in its pipe
            Assert.AreEqual(0, fluidPipe1.GetAmount(), 0.01f);
            Assert.AreEqual(0, fluidPipe2.GetAmount(), 0.01f);
            Assert.AreEqual(fluidAmount3, fluidPipe3.GetAmount(), 0.01f);
            var fluidContainers = GetInputFluidContainers(fluidMachineBlock.GetComponent<VanillaMachineBlockInventoryComponent>());
            Assert.AreEqual(3, fluidContainers.Count);
            Assert.AreEqual(FluidId1, fluidContainers[0].FluidId);
            Assert.AreEqual(fluidAmount1, fluidContainers[0].Amount, 0.01f);
            Assert.AreEqual(FluidId2, fluidContainers[1].FluidId);
            Assert.AreEqual(fluidAmount2, fluidContainers[1].Amount, 0.01f);
            Assert.AreEqual(FluidMaster.EmptyFluidId, fluidContainers[2].FluidId);
            Assert.AreEqual(0, fluidContainers[2].Amount, 0.01f);
        }
    }
}
