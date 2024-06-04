using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Const;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class MachineIoTest
    {
        //アイテムが通常通り処理されるかのテスト
        [Test]
        public void ItemProcessingOutputTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.MachineIoTestModDirectory);
            GameUpdater.ResetUpdate();
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var blockFactory = ServerContext.BlockFactory;
            var machineRecipeConfig = ServerContext.MachineRecipeConfig;
            
            var recipe = machineRecipeConfig.GetAllRecipeData()[0];
            
            
            var block = blockFactory.Create(recipe.BlockId, 1, new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var blockInventory = block.ComponentManager.GetComponent<VanillaMachineBlockInventoryComponent>();
            foreach (var inputItem in recipe.ItemInputs)
                blockInventory.InsertItem(itemStackFactory.Create(inputItem.Id, inputItem.Count));
            
            
            var craftTime = DateTime.Now.AddMilliseconds(recipe.Time);
            //最大クラフト時間を超過するまでクラフトする
            while (craftTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();
            
            //検証
            var (input, output) = GetInputOutputSlot(blockInventory);
            
            foreach (var inputItem in input) Assert.AreEqual(ItemConst.EmptyItemId, inputItem.Id);
            
            for (var i = 0; i < output.Count; i++) Assert.AreEqual(recipe.ItemOutputs[i], output[i]);
        }
        
        public (List<IItemStack>, List<IItemStack>) GetInputOutputSlot(VanillaMachineBlockInventoryComponent vanillaMachineInventory)
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