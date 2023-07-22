using System;
using System.Reflection;
using Core.Block.BlockFactory;
using Core.Block.Blocks.Machine;
using Core.Block.Blocks.Machine.Inventory;
using Core.Block.Blocks.Machine.InventoryController;
using Core.Block.Event;
using Core.Block.RecipeConfig;
using Core.ConfigJson;
using Core.Item;
using Core.Item.Config;
using Core.Update;
using Game.Crafting;
using Game.Crafting.Config;
using Game.PlayerInventory.Interface;
using Game.Save.Interface;
using Game.Save.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory;
using PlayerInventory.Event;
using Server.Boot;


using Test.Module.TestMod;
using World.DataStore;
using World.Event;

namespace Test.UnitTest.Game.SaveLoad
{
    public class MachineSaveLoadTest
    {
        //インベントリのあるブロックを追加した時のテスト
        //レシピやブロックが変わった時はテストコードを修正してください
        [Test]
        public void InventoryBlockTest()
        {
            //機械の追加
            var (itemStackFactory, blockFactory, worldBlockDatastore, _, assembleSaveJsonText,_) =
                CreateBlockTestModule();
            var machine = (VanillaMachine) blockFactory.Create(1, 10);
            worldBlockDatastore.AddBlock(machine, 0, 0, BlockDirection.North);


            //レシピ用のアイテムを追加
            machine.InsertItem(itemStackFactory.Create(1, 3));
            machine.InsertItem(itemStackFactory.Create(2, 1));
            //処理を開始
            GameUpdater.Update();
            //別のアイテムを追加
            machine.InsertItem(itemStackFactory.Create(5, 6));
            machine.InsertItem(itemStackFactory.Create(2, 4));

            //リフレクションで機械の状態を設定
            //機械のレシピの残り時間設定
            var vanillaMachineRunProcess = (VanillaMachineRunProcess) typeof(VanillaMachine)
                .GetField("_vanillaMachineRunProcess", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(machine);
            typeof(VanillaMachineRunProcess)
                .GetField("_remainingMillSecond", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vanillaMachineRunProcess, 300);
            //ステータスをセット
            typeof(VanillaMachineRunProcess)
                .GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vanillaMachineRunProcess, ProcessState.Processing);

            //機械のアウトプットスロットの設定
            var _vanillaMachineInventory = (VanillaMachineBlockInventory) typeof(VanillaMachine)
                .GetField("_vanillaMachineBlockInventory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(machine);

            var outputInventory = (VanillaMachineOutputInventory) typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_vanillaMachineInventory);

            outputInventory.SetItem(1, itemStackFactory.Create(1, 1));
            outputInventory.SetItem(2, itemStackFactory.Create(3, 2));

            //レシピIDを取得
            var recipeId = vanillaMachineRunProcess.RecipeDataId;

            var json = assembleSaveJsonText.AssembleSaveJson();
            Console.WriteLine(json);
            //配置したブロックを削除
            worldBlockDatastore.RemoveBlock( 0, 0);


            //ロードした時に機械の状態が正しいことを確認
            var (_, _, loadWorldBlockDatastore, _, _,loadJsonFile) = CreateBlockTestModule();
            
            loadJsonFile.Load(json);

            var loadMachine = (VanillaMachine) loadWorldBlockDatastore.GetBlock(0, 0);
            Console.WriteLine(machine.GetHashCode());
            Console.WriteLine(loadMachine.GetHashCode());
            //ブロックID、intIDが同じであることを確認
            Assert.AreEqual(machine.BlockId, loadMachine.BlockId);
            Assert.AreEqual(machine.EntityId, loadMachine.EntityId);


            //機械のレシピの残り時間のチェック
            var loadVanillaMachineRunProcess = (VanillaMachineRunProcess) typeof(VanillaMachine)
                .GetField("_vanillaMachineRunProcess", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachine);
            Assert.AreEqual((double) 300, (double) loadVanillaMachineRunProcess.RemainingMillSecond);
            //レシピIDのチェック
            Assert.AreEqual(recipeId, loadVanillaMachineRunProcess.RecipeDataId);
            //機械のステータスのチェック
            Assert.AreEqual(ProcessState.Processing, loadVanillaMachineRunProcess.CurrentState);


            var loadMachineInventory = (VanillaMachineBlockInventory) typeof(VanillaMachine)
                .GetField("_vanillaMachineBlockInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachine);
            //インプットスロットのチェック
            var inputInventoryField = (VanillaMachineInputInventory) typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachineInventory);
            Assert.AreEqual(itemStackFactory.Create(5, 6), inputInventoryField.InputSlot[0]);
            Assert.AreEqual(itemStackFactory.Create(2, 4), inputInventoryField.InputSlot[1]);

            //アウトプットスロットのチェック
            var outputInventoryField = (VanillaMachineOutputInventory) typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachineInventory);
            Assert.AreEqual(itemStackFactory.CreatEmpty(), outputInventoryField.OutputSlot[0]);
            Assert.AreEqual(itemStackFactory.Create(1, 1), outputInventoryField.OutputSlot[1]);
            Assert.AreEqual(itemStackFactory.Create(3, 2), outputInventoryField.OutputSlot[2]);
        }

        private (ItemStackFactory, BlockFactory, IWorldBlockDatastore, PlayerInventoryDataStore, AssembleSaveJsonText,WorldLoaderFromJson)
            CreateBlockTestModule()
        {
            
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var playerInventoryDataStore = serviceProvider.GetService<PlayerInventoryDataStore>();
            var loadJsonFile = serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;

            return (itemStackFactory, blockFactory, worldBlockDatastore, playerInventoryDataStore, assembleSaveJsonText,loadJsonFile);
        }
    }
}