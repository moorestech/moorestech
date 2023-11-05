using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Const;
using Core.Item;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.InventoryController;
using Game.Block.Interface;
using Game.Block.Interface.RecipeConfig;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class MachineIoTest
    {
        //アイテムが通常通り処理されるかのテスト
        [Test]
        public void ItemProcessingOutputTest()
        {
            var (_, serviceProvider) =
                new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.MachineIoTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var machineRecipeConfig = serviceProvider.GetService<IMachineRecipeConfig>();

            var recipe = machineRecipeConfig.GetAllRecipeData()[0];


            var block = (VanillaMachineBase)blockFactory.Create(recipe.BlockId, 1);
            foreach (var inputItem in recipe.ItemInputs)
                block.InsertItem(itemStackFactory.Create(inputItem.Id, inputItem.Count));


            var craftTime = DateTime.Now.AddMilliseconds(recipe.Time);
            //最大クラフト時間を超過するまでクラフトする
            while (craftTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1) GameUpdater.Update();

            //検証
            var (input, output) = GetInputOutputSlot(block);

            foreach (var inputItem in input) Assert.AreEqual(ItemConst.EmptyItemId, inputItem.Id);

            for (var i = 0; i < output.Count; i++) Assert.AreEqual(recipe.ItemOutputs[i], output[i]);
        }

        public (List<IItemStack>, List<IItemStack>) GetInputOutputSlot(VanillaMachineBase machineBase)
        {
            var _vanillaMachineInventory = (VanillaMachineBlockInventory)typeof(VanillaMachineBase)
                .GetField("_vanillaMachineBlockInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(machineBase);
            var _vanillaMachineInputInventory = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_vanillaMachineInventory);
            var _vanillaMachineOutputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_vanillaMachineInventory);

            var inputSlot = _vanillaMachineInputInventory.InputSlot.Where(i => i.Count != 0).ToList();
            inputSlot.Sort((a, b) => a.Id - b.Id);

            var outputSlot = _vanillaMachineOutputInventory.OutputSlot.Where(i => i.Count != 0).ToList();
            outputSlot.Sort((a, b) => a.Id - b.Id);

            return (inputSlot, outputSlot);
        }
    }
}