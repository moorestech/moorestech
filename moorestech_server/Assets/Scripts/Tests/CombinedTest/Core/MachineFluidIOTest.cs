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
        public void FluidMachineInputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // パイプを設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 0, BlockDirection.North, out var fluidPipeBlock);
            // 機械を設置 (パイプの隣に設置)
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.right * 1, BlockDirection.North, out var fluidMachineBlock);
            
            var fluidPipe = fluidPipeBlock.GetComponent<Game.Block.Blocks.Fluid.FluidPipeComponent>();
            var fluidMachineInventory = fluidMachineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            
            // パイプに液体を設定
            var fluidGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
            var fluidId = MasterHolder.FluidMaster.GetFluidId(fluidGuid);
            const double fluidAmount = 50d;
            var fluidStack = new FluidStack(fluidAmount, fluidId);
            fluidPipe.AddLiquid(fluidStack, FluidContainer.Empty);
            
            // 初期状態の確認
            Assert.AreEqual(fluidAmount, fluidPipe.GetAmount(), "Initial pipe fluid amount should match");
            var fluidContainers = GetFluidContainers(fluidMachineInventory);
            Assert.AreEqual(0, fluidContainers[0].Amount, "Machine fluid container should be empty initially");
            
            // アップデート（液体が流れるのを待つ）
            var startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateWithWait();
                
                var elapsedTime = DateTime.Now - startTime;
                if (elapsedTime.TotalSeconds > 5) break; // 5秒待機
            }
            
            // パイプが空になり、機械のインプットスロットに液体が入っていることを確認
            Assert.AreEqual(0d, fluidPipe.GetAmount(), 0.1d, "Pipe should be empty after transfer");
            Assert.AreEqual(fluidAmount, fluidContainers[0].Amount, 0.1d, "Machine should contain all the fluid");
            Assert.AreEqual(fluidId, fluidContainers[0].FluidId, "Fluid ID should match in machine");
        }
        
        [Test]
        public void FluidMachineOutputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // 機械を設置
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidMachineId, Vector3Int.right * 0, BlockDirection.North, out var fluidMachineBlock);
            // パイプを設置 (機械の隣に設置)
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FluidPipe, Vector3Int.right * 1, BlockDirection.North, out var fluidPipeBlock);
            
            var fluidPipe = fluidPipeBlock.GetComponent<Game.Block.Blocks.Fluid.FluidPipeComponent>();
            var fluidMachineInventory = fluidMachineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            
            // 機械のアウトプットスロットに液体を設定
            var fluidGuid = Guid.Parse("00000000-0000-0000-1234-000000000002"); // Steam
            var fluidId = MasterHolder.FluidMaster.GetFluidId(fluidGuid);
            const double fluidAmount = 40d;
            var fluidStack = new FluidStack(fluidAmount, fluidId);
            
            // リフレクションを使用してアウトプットスロットに液体を設定
            var vanillaMachineOutputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventoryComponent)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(fluidMachineInventory);
            
            var fluidOutputSlot = vanillaMachineOutputInventory.FluidOutputSlot;
            fluidOutputSlot[0].AddLiquid(fluidStack, FluidContainer.Empty);
            
            // 初期状態の確認
            Assert.AreEqual(0d, fluidPipe.GetAmount(), "Pipe should be empty initially");
            Assert.AreEqual(fluidAmount, fluidOutputSlot[0].Amount, "Machine output should contain fluid initially");
            
            // アップデート（液体が流れるのを待つ）
            var startTime = DateTime.Now;
            while (true)
            {
                GameUpdater.UpdateWithWait();
                
                var elapsedTime = DateTime.Now - startTime;
                if (elapsedTime.TotalSeconds > 5) break; // 5秒待機
            }
            
            // 機械のアウトプットスロットが空になり、パイプに液体が入っていることを確認
            Assert.AreEqual(0d, fluidOutputSlot[0].Amount, 0.1d, "Machine output should be empty after transfer");
            Assert.AreEqual(fluidAmount, fluidPipe.GetAmount(), 0.1d, "Pipe should contain all the fluid");
            Assert.AreEqual(fluidId, fluidPipe.GetFluidId(), "Fluid ID should match in pipe");
        }
        
        [Test]
        public void FluidProcessingOutputTest()
        {
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
                
                fluidContainers[i].AddLiquid(fluidStack, FluidContainer.Empty);
                
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
    
    public static class FluidPipeExtension
    {
        public static FluidContainer GetFluidContainer(this Game.Block.Blocks.Fluid.FluidPipeComponent fluidPipe)
        {
            var field = typeof(Game.Block.Blocks.Fluid.FluidPipeComponent).GetField("_fluidContainer", BindingFlags.NonPublic | BindingFlags.Instance);
            return (FluidContainer)field.GetValue(fluidPipe);
        }
        
        public static double GetAmount(this Game.Block.Blocks.Fluid.FluidPipeComponent fluidPipe)
        {
            return fluidPipe.GetFluidContainer().Amount;
        }
        
        public static FluidId GetFluidId(this Game.Block.Blocks.Fluid.FluidPipeComponent fluidPipe)
        {
            return fluidPipe.GetFluidContainer().FluidId;
        }
    }
}
