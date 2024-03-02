using System.Reflection;
using Core.Item;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.Machine.InventoryController;
using Game.Block.Interface;
using Game.PlayerInventory;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class MachineSaveLoadTest
    {
        //インベントリのあるブロックを追加した時のテスト
        //レシピやブロックが変わった時はテストコードを修正してください
        [Test]
        public void InventoryBlockTest()
        {
            //機械の追加
            var (itemStackFactory, blockFactory, worldBlockDatastore, _, assembleSaveJsonText, _) = CreateBlockTestModule();
            GameUpdater.ResetUpdate();
            
            var machine = (VanillaMachineBase)blockFactory.Create(1, 10);
            worldBlockDatastore.AddBlock(machine, 0, 0, BlockDirection.North);


            //レシピ用のアイテムを追加
            machine.InsertItem(itemStackFactory.Create(1, 3));
            machine.InsertItem(itemStackFactory.Create(2, 1));
            //処理を開始
            GameUpdater.UpdateWithWait();
            //別のアイテムを追加
            machine.InsertItem(itemStackFactory.Create(5, 6));
            machine.InsertItem(itemStackFactory.Create(2, 4));

            //リフレクションで機械の状態を設定
            //機械のレシピの残り時間設定
            var vanillaMachineRunProcess = (VanillaMachineRunProcess)typeof(VanillaMachineBase)
                .GetField("_vanillaMachineRunProcess", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(machine);
            typeof(VanillaMachineRunProcess)
                .GetProperty("RemainingMillSecond")
                .SetValue(vanillaMachineRunProcess, 300);
            //ステータスをセット
            typeof(VanillaMachineRunProcess)
                .GetProperty("CurrentState")
                .SetValue(vanillaMachineRunProcess, ProcessState.Processing);

            //機械のアウトプットスロットの設定
            var _vanillaMachineInventory = (VanillaMachineBlockInventory)typeof(VanillaMachineBase)
                .GetField("_vanillaMachineBlockInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(machine);

            var outputInventory = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_vanillaMachineInventory);

            outputInventory.SetItem(1, itemStackFactory.Create(1, 1));
            outputInventory.SetItem(2, itemStackFactory.Create(3, 2));

            //レシピIDを取得
            var recipeId = vanillaMachineRunProcess.RecipeDataId;

            var json = assembleSaveJsonText.AssembleSaveJson();
            Debug.Log(json);
            //配置したブロックを削除
            worldBlockDatastore.RemoveBlock(0, 0);


            //ロードした時に機械の状態が正しいことを確認
            var (_, _, loadWorldBlockDatastore, _, _, loadJsonFile) = CreateBlockTestModule();

            loadJsonFile.Load(json);

            var loadMachine = (VanillaMachineBase)loadWorldBlockDatastore.GetBlock(0, 0);
            Debug.Log(machine.GetHashCode());
            Debug.Log(loadMachine.GetHashCode());
            //ブロックID、intIDが同じであることを確認
            Assert.AreEqual(machine.BlockId, loadMachine.BlockId);
            Assert.AreEqual(machine.EntityId, loadMachine.EntityId);


            //機械のレシピの残り時間のチェック
            var loadVanillaMachineRunProcess = (VanillaMachineRunProcess)typeof(VanillaMachineBase)
                .GetField("_vanillaMachineRunProcess", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachine);
            Assert.AreEqual(300, loadVanillaMachineRunProcess.RemainingMillSecond);
            //レシピIDのチェック
            Assert.AreEqual(recipeId, loadVanillaMachineRunProcess.RecipeDataId);
            //機械のステータスのチェック
            Assert.AreEqual(ProcessState.Processing, loadVanillaMachineRunProcess.CurrentState);


            var loadMachineInventory = (VanillaMachineBlockInventory)typeof(VanillaMachineBase)
                .GetField("_vanillaMachineBlockInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachine);
            //インプットスロットのチェック
            var inputInventoryField = (VanillaMachineInputInventory)typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineInputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachineInventory);
            Assert.AreEqual(itemStackFactory.Create(5, 6), inputInventoryField.InputSlot[0]);
            Assert.AreEqual(itemStackFactory.Create(2, 4), inputInventoryField.InputSlot[1]);

            //アウトプットスロットのチェック
            var outputInventoryField = (VanillaMachineOutputInventory)typeof(VanillaMachineBlockInventory)
                .GetField("_vanillaMachineOutputInventory", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadMachineInventory);
            Assert.AreEqual(itemStackFactory.CreatEmpty(), outputInventoryField.OutputSlot[0]);
            Assert.AreEqual(itemStackFactory.Create(1, 1), outputInventoryField.OutputSlot[1]);
            Assert.AreEqual(itemStackFactory.Create(3, 2), outputInventoryField.OutputSlot[2]);
        }

        private (ItemStackFactory, IBlockFactory, IWorldBlockDatastore, PlayerInventoryDataStore, AssembleSaveJsonText,
            WorldLoaderFromJson)
            CreateBlockTestModule()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var playerInventoryDataStore = serviceProvider.GetService<PlayerInventoryDataStore>();
            var loadJsonFile = serviceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;

            return (itemStackFactory, blockFactory, worldBlockDatastore, playerInventoryDataStore, assembleSaveJsonText,
                loadJsonFile);
        }
    }
}