using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Gear;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearMachineIoTest
    {
        public int GearMachineRecipeIndex = 3;
        
        //アイテムが通常通り処理されるかのテスト
        [Test]
        public void ItemProcessingOutputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[GearMachineRecipeIndex];
            
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            var block = blockFactory.Create(blockId, new BlockInstanceId(1), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var blockInventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.InputItems)
            {
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
            
            var gearEnergyTransformer = block.GetComponent<GearEnergyTransformer>();
            var gearMachineParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearMachine).BlockParam as GearMachineBlockParam;
            var machineProcessor = block.GetComponent<VanillaMachineProcessorComponent>();
            
            //最大クラフト時間を超過するまでクラフトする
            var craftTime = DateTime.Now.AddSeconds(recipe.Time);
            while (craftTime.AddSeconds(0.4).CompareTo(DateTime.Now) == 1)
            {
                var requiredRpm = new RPM(gearMachineParam.RequiredRpm);
                var requiredTorque = new Torque(gearMachineParam.RequireTorque);
                gearEnergyTransformer.SupplyPower(requiredRpm, requiredTorque, true);
                machineProcessor.Update();
                GameUpdater.Wait();
                GameUpdater.UpdateDeltaTime();
            }
            
            //検証
            AssertInventory(blockInventory, recipe);
        }
        
        
        [Test]
        // RPM、トルクが足りないときに処理に時間がかかるテスト
        public void NotEnoughTorqueOrRpmTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[GearMachineRecipeIndex];
            
            var recipeBlockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            var lackRpmBlock = blockFactory.Create(recipeBlockId, new BlockInstanceId(1), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var lackTorqueBlock = blockFactory.Create(recipeBlockId, new BlockInstanceId(2), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.zero));
            
            var lackRpmInventory = lackRpmBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            var lackTorqueInventory = lackTorqueBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            
            foreach (var inputItem in recipe.InputItems)
            {
                lackRpmInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
                lackTorqueInventory.InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));
            }
            
            var lackRpmGearMachine = lackRpmBlock.GetComponent<GearEnergyTransformer>();
            var lackTorqueGearMachine = lackTorqueBlock.GetComponent<GearEnergyTransformer>();
            var gearMachineParam = lackRpmBlock.BlockMasterElement.BlockParam as GearMachineBlockParam;
            
            var lackRpmProcessor = lackRpmBlock.GetComponent<VanillaMachineProcessorComponent>();
            var lackTorqueProcessor = lackTorqueBlock.GetComponent<VanillaMachineProcessorComponent>();
            
            //最大クラフト時間を超過するまでクラフトする
            var craftTime = DateTime.Now.AddSeconds(recipe.Time * 2);
            while (craftTime.AddSeconds(0.3).CompareTo(DateTime.Now) == 1)
            {
                var rpm = new RPM(gearMachineParam.RequiredRpm / 2f);
                lackRpmGearMachine.SupplyPower(rpm, new Torque(gearMachineParam.RequireTorque), true);
                lackTorqueGearMachine.SupplyPower(new RPM(gearMachineParam.RequiredRpm), (Torque)gearMachineParam.RequireTorque / 2f, true);
                
                lackRpmProcessor.Update();
                lackTorqueProcessor.Update();
                
                GameUpdater.Wait();
                GameUpdater.UpdateDeltaTime();
            }
            
            //検証
            AssertInventory(lackRpmInventory, recipe);
            AssertInventory(lackTorqueInventory, recipe);
        }
        
        private void AssertInventory(VanillaMachineBlockInventoryComponent inventory, MachineRecipeMasterElement recipe)
        {
            (List<IItemStack> input, List<IItemStack> output) = GetInputOutputSlot(inventory);
            
            Assert.AreEqual(0, input.Count);
            foreach (var inputItem in input) Assert.AreEqual(ItemMaster.EmptyItemId, inputItem.Id);
            
            Assert.AreNotEqual(0, output.Count);
            for (var i = 0; i < output.Count; i++)
            {
                var outputItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[i].ItemGuid);
                Assert.AreEqual(outputItemId, output[i].Id);
                Assert.AreEqual(recipe.OutputItems[i].Count, output[i].Count);
            }
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