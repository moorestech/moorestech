using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Core.Update;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class MachineFluidIOTest
    {
        [Test]
        public void FluidProcessingOutputTest()
        {
            /* いったんFluid関連の処理を作り終わるまでコメントアウトしておく
             
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[9]; // L:229
            
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            var block = blockFactory.Create(blockId, new BlockInstanceId(1), new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            
            
            // 必要素材を入れる
            // Set up the required materials
            var fluidContainers = GetFluidContainers(blockInventory);
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                var inputFluid = recipe.InputFluids[i];
                var fluidId = MasterHolder.FluidMaster.GetFluidId(inputFluid.FluidGuid);
                var fluidStack = new FluidStack(inputFluid.Amount, fluidId);
                
                // TODO この入れ方は普通に間違っているので、
                fluidContainers[i].AddLiquid(fluidStack, FluidContainer.Empty, out _);
                
                Assert.AreEqual(fluidId, fluidContainers[i].FluidId, "Fluid ID should match");
                Assert.AreEqual(inputFluid.Amount, fluidContainers[i].Amount, "Fluid amount should match");
            }
            
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
            
            // クラフト実行
            // Perform the crafting
            var blockMachineComponent = block.GetComponent<VanillaElectricMachineComponent>();
            var craftTime = DateTime.Now.AddSeconds(recipe.Time);
            while (craftTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                blockMachineComponent.SupplyEnergy(new ElectricPower(10000));
                GameUpdater.UpdateWithWait();
            }
            
            // 検証
            // Verification
            for (var i = 0; i < recipe.InputFluids.Length; i++)
            {
                Assert.AreEqual(0, fluidContainers[i].Amount, $"Fluid in container {i} should be consumed");
                Assert.AreEqual(FluidMaster.EmptyFluidId, fluidContainers[i].FluidId, $"Fluid ID in container {i} should be reset to empty");
            }
            for (int i = 0; i < recipe.OutputFluids.Length; i++)
            {
                var expectedFluidId = MasterHolder.FluidMaster.GetFluidId(recipe.OutputFluids[i].FluidGuid);
                Assert.AreEqual(expectedFluidId, fluidContainers[i].FluidId, $"Output fluid {i} ID should match");
                Assert.AreEqual(recipe.OutputFluids[i].Amount, fluidContainers[i].Amount, $"Output fluid {i} amount should match");
            }
            
            var (_, outputSlot) = GetInputOutputSlot(blockInventory);
            
            Assert.AreNotEqual(0, outputSlot.Count, "Output slot should not be empty");
            for (var i = 0; i < recipe.OutputItems.Length; i++)
            {
                var expectedOutputId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[i].ItemGuid);
                Assert.AreEqual(expectedOutputId, outputSlot[i].Id, $"Output item {i} ID should match");
                Assert.AreEqual(recipe.OutputItems[i].Count, outputSlot[i].Count, $"Output item {i} count should match");
            }
            
            */
        }
        
        private IReadOnlyList<FluidContainer> GetFluidContainers(VanillaMachineBlockInventoryComponent blockInventory)
        {
            var vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(blockInventory);
            
            return vanillaMachineInputInventory.FluidInputSlot;
        }
        
        private (List<IItemStack>, List<IItemStack>) GetInputOutputSlot(VanillaMachineBlockInventoryComponent vanillaMachineInventory)
        {
            var vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineInventory);
            var vanillaMachineOutputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(vanillaMachineInventory);
            
            var inputSlot = vanillaMachineInputInventory.InputSlot.Where(i => i.Count != 0).ToList();
            inputSlot.Sort((a, b) => a.Id.AsPrimitive() - b.Id.AsPrimitive());
            
            var outputSlot = vanillaMachineOutputInventory.OutputSlot.Where(i => i.Count != 0).ToList();
            outputSlot.Sort((a, b) => a.Id.AsPrimitive() - b.Id.AsPrimitive());
            
            return (inputSlot, outputSlot);
        }
    }
}
