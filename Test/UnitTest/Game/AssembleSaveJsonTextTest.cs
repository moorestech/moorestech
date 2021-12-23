using System;
using System.Reflection;
using Core.Block;
using Core.Block.BlockFactory;
using Core.Block.Machine;
using Core.Block.RecipeConfig;
using Core.Block.RecipeConfig.Data;
using Core.Item;
using Core.Item.Config;
using Core.Update;
using Game.Save.Json;
using Game.World.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PlayerInventory;
using PlayerInventory.Event;
using Server;
using Test.TestConfig;
using World;
using World.DataStore;
using World.Event;

namespace Test.UnitTest.Game
{
    public class AssembleSaveJsonTextTest
    {
        //何もデータがない時のテスト
        [Test]
        public void NoneTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var json = assembleSaveJsonText.AssembleSaveJson();
            Assert.AreEqual("{\"world\":[],\"playerInventory\":[]}",json);
        }
        
        //ブロックを追加した時のテスト
        [Test]
        public void SimpleBlockPlacedTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var assembleSaveJsonText = serviceProvider.GetService<AssembleSaveJsonText>();
            var worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();

            worldBlockDatastore.AddBlock(new NormalBlock( 10, 10), 0, 0,BlockDirection.North);
            worldBlockDatastore.AddBlock(new NormalBlock( 15, 100), 10,-15,BlockDirection.North);
            
            var json = assembleSaveJsonText.AssembleSaveJson();
            
            var (_, loadServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            loadServiceProvider.GetService<AssembleSaveJsonText>().LoadJson(json);
            var worldLoadBlockDatastore = loadServiceProvider.GetService<IWorldBlockDatastore>();
            
            var block1 = worldLoadBlockDatastore.GetBlock(0, 0);
            Assert.AreEqual(10, block1.GetBlockId());
            Assert.AreEqual(10, block1.GetIntId());
            
            var block2 = worldLoadBlockDatastore.GetBlock(10, -15);
            Assert.AreEqual(15, block2.GetBlockId());
            Assert.AreEqual(100, block2.GetIntId());
        }
        
        //インベントリのあるブロックを追加した時のテスト
        //レシピやブロックが変わった時はテストコードを修正してください
        [Test]
        public void InventoryBlockTest()
        {
            //機械の追加
            var (itemStackFactory,blockFactory,worldBlockDatastore,_,assembleSaveJsonText) = CreateBlockTestModule();
            var machine = (NormalMachine)blockFactory.Create(2, 10);
            worldBlockDatastore.AddBlock(machine, 0, 0,BlockDirection.North);
            
            
            //レシピ用のアイテムを追加
            machine.InsertItem(itemStackFactory.Create(1,3));
            machine.InsertItem(itemStackFactory.Create(2,1));
            //処理を開始
            GameUpdate.Update();
            //別のアイテムを追加
            machine.InsertItem(itemStackFactory.Create(5,6));
            machine.InsertItem(itemStackFactory.Create(2,4));
            
            //リフレクションで機械の状態を設定
            Type machineType = machine.GetType();
            //機械のレシピの残り時間設定
            var remainingMillSecond = machineType.GetField("_remainingMillSecond",BindingFlags.NonPublic | BindingFlags.Instance);
            remainingMillSecond.SetValue(machine,300);
            
            //機械のアウトプットスロットの設定
            var outputInventory = (NormalMachineOutputInventory)machineType.GetField("_normalMachineOutputInventory",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(machine);
            outputInventory.SetItem(1,itemStackFactory.Create(1,1));
            outputInventory.SetItem(2,itemStackFactory.Create(3,2));
            
            //レシピIDを取得
            var _processingRecipeData = (IMachineRecipeData)machineType.GetField("_processingRecipeData",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(machine);
            var recipeId = _processingRecipeData.RecipeId;

            var json = assembleSaveJsonText.AssembleSaveJson();
            Console.WriteLine(json);
            //配置したブロックを削除
            worldBlockDatastore.AddBlock(blockFactory.Create(0,0),0,0,BlockDirection.North);
            
            
            
            
            //ロードした時に機械の状態が正しいことを確認
            var (_,_,loadWorldBlockDatastore,_,loadAssembleSaveJsonText) = CreateBlockTestModule();
            loadAssembleSaveJsonText.LoadJson(json);
            
            var loadMachine = (NormalMachine)loadWorldBlockDatastore.GetBlock(0,0);
            Console.WriteLine(machine.GetHashCode());
            Console.WriteLine(loadMachine.GetHashCode());
            //ブロックID、intIDが同じであることを確認
            Assert.AreEqual(machine.GetBlockId(),loadMachine.GetBlockId());
            Assert.AreEqual(machine.GetIntId(),loadMachine.GetIntId());
            
            var loadMachineType = loadMachine.GetType();
            
            //機械のレシピの残り時間のチェック
            var loadRemainingMillSecond = loadMachineType.GetField("_remainingMillSecond",BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual((double)300,(double)loadRemainingMillSecond.GetValue(loadMachine));
            
            //機械のステータスのチェック
            var loadMachineStatus = loadMachineType.GetField("_state",BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual(ProcessState.Processing,(ProcessState)loadMachineStatus.GetValue(loadMachine));
            
            //インプットスロットのチェック
            var inputInventoryField = (NormalMachineInputInventory)loadMachineType.GetField("_normalMachineInputInventory",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(loadMachine);
            Assert.AreEqual(itemStackFactory.Create(5,6),inputInventoryField.InputSlot[0]);
            Assert.AreEqual(itemStackFactory.Create(2,4),inputInventoryField.InputSlot[1]);
            
            //アウトプットスロットのチェック
            var outputInventoryField = (NormalMachineOutputInventory)loadMachineType.GetField("_normalMachineOutputInventory",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(loadMachine);
            Assert.AreEqual(itemStackFactory.CreatEmpty(),outputInventoryField.OutputSlot[0]);
            Assert.AreEqual(itemStackFactory.Create(1,1),outputInventoryField.OutputSlot[1]);
            Assert.AreEqual(itemStackFactory.Create(3,2),outputInventoryField.OutputSlot[2]);
            
            //レシピIDのチェック
            var loadProcessingRecipeData = (IMachineRecipeData)loadMachineType.GetField("_processingRecipeData",BindingFlags.NonPublic | BindingFlags.Instance).GetValue(loadMachine);
            Assert.AreEqual(recipeId,loadProcessingRecipeData.RecipeId);
        }

        private (ItemStackFactory,BlockFactory,WorldBlockDatastore,PlayerInventoryDataStore,AssembleSaveJsonText) CreateBlockTestModule()
        {
            var itemFactory = new ItemStackFactory(new TestItemConfig());
            var blockFactory = new BlockFactory(new AllMachineBlockConfig(),new VanillaIBlockTemplates(new TestMachineRecipeConfig(itemFactory),itemFactory));
            var worldBlockDatastore = new WorldBlockDatastore(new BlockPlaceEvent(),blockFactory);
            var playerInventoryDataStore = new PlayerInventoryDataStore(new PlayerInventoryUpdateEvent(), itemFactory);
            var assembleSaveJsonText = new AssembleSaveJsonText(playerInventoryDataStore,worldBlockDatastore);

            return (itemFactory,blockFactory, worldBlockDatastore,playerInventoryDataStore, assembleSaveJsonText);
        }
       
    }
}