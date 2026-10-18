using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.MachineFluidInventoryTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    public class MachineFluidProcessingTest
    {
        [Test]
        public void FluidProcessingOutputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[7];
            // マシンブロックの配置
            // Place the machine block
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            // 必要素材を入れる
            // Set up the required materials
            var inputFluidContainers = GetInputFluidContainers(blockInventory);
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                var inputFluid = recipe.InputFluids[i];
                var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                var fluidStack = new FluidStack(inputFluid.Amount, fluidId);
                // インサートするインデックスを1個ずらす
                var insertIndex = i + 1;
                insertIndex = inputFluidContainers.Count == insertIndex ? 0 : insertIndex;
                inputFluidContainers[insertIndex].AddLiquid(fluidStack);
                Assert.AreEqual(fluidId, inputFluidContainers[insertIndex].FluidId, "Fluid ID should match");
                Assert.AreEqual(inputFluid.Amount, inputFluidContainers[insertIndex].Amount, "Fluid amount should match");
            }
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
            // クラフト実行
            // Perform the crafting
            var blockMachineComponent = block.GetComponent<VanillaMachineProcessorComponent>();
            var startTime = DateTime.Now;
            var endTime = startTime.AddSeconds(recipe.Time + 0.2); // レシピ時間 + 余裕時間
            while (DateTime.Now < endTime)
            {
                blockMachineComponent.SupplyExternalPower(10000);
                GameUpdater.UpdateOneTick();
            }
            // 検証
            // Verification
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                Assert.AreEqual(0, inputFluidContainers[i].Amount, $"Fluid in container {i} should be consumed");
                Assert.AreEqual(FluidMaster.EmptyFluidId, inputFluidContainers[i].FluidId, $"Fluid ID in container {i} should be reset to empty");
            }
            var outputFluidContainers = MachineFluidTestUtil.GetOutputFluidContainers(blockInventory);
            for (int i = 0; i < recipe.OutputFluids.Length; i++)
            {
                var expectedFluidId = MasterHolder.FluidMaster.GetFluidId(recipe.OutputFluids[i].FluidGuid);
                Assert.AreEqual(expectedFluidId, outputFluidContainers[i].FluidId, $"Output fluid {i} ID should match");
                Assert.AreEqual(recipe.OutputFluids[i].Amount, outputFluidContainers[i].Amount, $"Output fluid {i} amount should match");
            }
            var (_, outputSlot) = GetInputOutputSlot(blockInventory);
            Assert.AreNotEqual(0, outputSlot.Count, "Output slot should not be empty");
            for (var i = 0; i < recipe.OutputItems.Length; i++)
            {
                var expectedOutputId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[i].ItemGuid);
                Assert.AreEqual(expectedOutputId, outputSlot[i].Id, $"Output item {i} ID should match");
                Assert.AreEqual(recipe.OutputItems[i].Count, outputSlot[i].Count, $"Output item {i} count should match");
            }
        }
    }
}
