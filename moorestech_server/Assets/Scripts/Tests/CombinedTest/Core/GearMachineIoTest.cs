using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Const;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.RecipeConfig;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearMachineIoTest
    {
        public int GearMachineRecipeIndex = 1;
        
        //アイテムが通常通り処理されるかのテスト
        [Test]
        public void ItemProcessingOutputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.MachineIoTestModDirectory);
            GameUpdater.ResetUpdate();
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            var machineRecipeConfig = ServerContext.MachineRecipeConfig;
            
            var recipe = machineRecipeConfig.GetAllRecipeData()[GearMachineRecipeIndex];
            
            var block = blockFactory.Create(recipe.BlockId, new BlockInstanceId(1), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var blockInventory = block.ComponentManager.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.ItemInputs)
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.Id, inputItem.Count));
            
            var gearMachineComponent = block.ComponentManager.GetComponent<VanillaGearMachineComponent>();
            var gearMachineParam = (GearMachineConfigParam)block.BlockConfigData.Param;
            
            //最大クラフト時間を超過するまでクラフトする
            var craftTime = DateTime.Now.AddMilliseconds(recipe.Time);
            while (craftTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                gearMachineComponent.SupplyPower(gearMachineParam.RequiredRpm, gearMachineParam.RequiredTorque, true);
                GameUpdater.UpdateWithWait();
            }
            
            //検証
            AssertInventory(blockInventory, recipe);
        }
        
        
        [Test]
        // RPM、トルクが足りないときに処理に時間がかかるテスト
        public void NotEnoughTorqueOrRpmTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.MachineIoTestModDirectory);
            GameUpdater.ResetUpdate();
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            var machineRecipeConfig = ServerContext.MachineRecipeConfig;
            
            var recipe = machineRecipeConfig.GetAllRecipeData()[GearMachineRecipeIndex];
            
            var lackRpmBlock = blockFactory.Create(recipe.BlockId, new BlockInstanceId(1), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var lackTorqueBlock = blockFactory.Create(recipe.BlockId, new BlockInstanceId(2), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.zero));
            
            var lackRpmInventory = lackRpmBlock.ComponentManager.GetComponent<VanillaMachineBlockInventoryComponent>();
            var lackTorqueInventory = lackTorqueBlock.ComponentManager.GetComponent<VanillaMachineBlockInventoryComponent>();
            
            foreach (var inputItem in recipe.ItemInputs)
            {
                lackRpmInventory.InsertItem(itemStackFactory.Create(inputItem.Id, inputItem.Count));
                lackTorqueInventory.InsertItem(itemStackFactory.Create(inputItem.Id, inputItem.Count));
            }
            
            var lackRpmGearMachine = lackRpmBlock.ComponentManager.GetComponent<VanillaGearMachineComponent>();
            var lackTorqueGearMachine = lackTorqueBlock.ComponentManager.GetComponent<VanillaGearMachineComponent>();
            var gearMachineParam = (GearMachineConfigParam)lackRpmBlock.BlockConfigData.Param;
            
            //最大クラフト時間を超過するまでクラフトする
            var craftTime = DateTime.Now.AddMilliseconds(recipe.Time);
            while (craftTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                lackRpmGearMachine.SupplyPower(gearMachineParam.RequiredRpm / 2f, gearMachineParam.RequiredTorque, true);
                lackTorqueGearMachine.SupplyPower(gearMachineParam.RequiredRpm, gearMachineParam.RequiredTorque / 2f, true);
                GameUpdater.UpdateWithWait();
            }
            
            //検証
            AssertInventory(lackRpmInventory, recipe);
            AssertInventory(lackTorqueInventory, recipe);
        }
        
        private void AssertInventory(VanillaMachineBlockInventoryComponent inventory, MachineRecipeData recipe)
        {
            (List<IItemStack> input, List<IItemStack> output) = GetInputOutputSlot(inventory);
            
            foreach (var inputItem in input) Assert.AreEqual(ItemConst.EmptyItemId, inputItem.Id);
            
            for (var i = 0; i < output.Count; i++) Assert.AreEqual(recipe.ItemOutputs[i], output[i]);
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
            inputSlot.Sort((a, b) => a.Id - b.Id);
            
            var outputSlot = vanillaMachineOutputInventory.OutputSlot.Where(i => i.Count != 0).ToList();
            outputSlot.Sort((a, b) => a.Id - b.Id);
            
            return (inputSlot, outputSlot);
        }
    }
}