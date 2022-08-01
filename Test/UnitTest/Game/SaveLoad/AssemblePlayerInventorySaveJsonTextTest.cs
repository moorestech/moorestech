using System;
using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface;
using Game.Save.Interface;
using Game.Save.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;


using Test.Module.TestMod;

namespace Test.UnitTest.Game.SaveLoad
{
    public class AssemblePlayerInventorySaveJsonTextTest
    {
        [Test]
        public void OnePlayerTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInventory = saveServiceProvider.GetService<IPlayerInventoryDataStore>();
            var itemStackFactory = saveServiceProvider.GetService<ItemStackFactory>();
            var assembleJsonText = saveServiceProvider.GetService<AssembleSaveJsonText>();
            
            int playerEntityId = 100;
            
            //プレイヤーインベントリの作成
            var inventory =  playerInventory.GetInventoryData(playerEntityId);

            //セットするアイテムを定義する
            var mainItems = new Dictionary<int, IItemStack>();
            mainItems.Add(0,itemStackFactory.Create(2,10));
            mainItems.Add(10,itemStackFactory.Create(5,1));
            mainItems.Add(30,itemStackFactory.Create(10,10));
            mainItems.Add(PlayerInventoryConst.MainInventorySize - 1,itemStackFactory.Create(12,11));
            
            var craftItems = new Dictionary<int, IItemStack>();
            craftItems.Add(0,itemStackFactory.Create(2,5));
            craftItems.Add(1,itemStackFactory.Create(3,4));
            craftItems.Add(7,itemStackFactory.Create(4,7));
            
            //メインアイテムをセットする
            foreach (var item in mainItems)
            {
                inventory.MainOpenableInventory.SetItem(item.Key,item.Value);
            }
            //クラフトアイテムをセットする
            foreach (var item in craftItems)
            {
                inventory.CraftingOpenableInventory.SetItem(item.Key,item.Value);
            }
            
            
            
            
            
            
            //セーブする
            var json = assembleJsonText.AssembleSaveJson();
            
            
            
            
            
            
            
            
            //セーブしたデータをロードする
            var (_, loadServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            (loadServiceProvider.GetService<ILoadRepository>() as LoadJsonFile).Load(json);
            var loadedPlayerInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerEntityId);

            //メインのインベントリのチェック
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                if (mainItems.ContainsKey(i))
                {
                    Assert.AreEqual(mainItems[i],loadedPlayerInventory.MainOpenableInventory.GetItem(i));
                    continue;
                }
                Assert.AreEqual(itemStackFactory.CreatEmpty(),loadedPlayerInventory.MainOpenableInventory.GetItem(i));
            }
            //クラフトのインベントリのチェック
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                if (craftItems.ContainsKey(i))
                {
                    Assert.AreEqual(craftItems[i],loadedPlayerInventory.CraftingOpenableInventory.GetItem(i));
                    continue;
                }
                Assert.AreEqual(itemStackFactory.CreatEmpty(),loadedPlayerInventory.CraftingOpenableInventory.GetItem(i));
            }
        }

        /// <summary>
        /// 複数ユーザーの時インベントリのデータが正しくセーブできるか
        /// </summary>
        [Test]
        public void MultiplePlayerSaveTest()
        {
            var (_, saveServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInventory = saveServiceProvider.GetService<IPlayerInventoryDataStore>();
            var itemStackFactory = saveServiceProvider.GetService<ItemStackFactory>();
            int seed = 13143;
            
            //プレイヤーのインベントリを作成
            var playerItems = new Dictionary<int, Dictionary<int, IItemStack>>();
            var random = new Random(seed);
            for (int i = 0; i < 20; i++)
            {
                var playerId = random.Next();
                playerItems.Add(playerId,CreateSetItems(random,itemStackFactory));
            }
            
            //プレイヤーインベントリにアイテムをセットする
            foreach (var playerItem in playerItems)
            {
                var inventory = playerInventory.GetInventoryData(playerItem.Key);
                foreach (var item in playerItem.Value)
                {
                    inventory.MainOpenableInventory.SetItem(item.Key,item.Value);
                }
            }
            
            //セーブする
            var json = saveServiceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();
            
            
            //セーブしたデータをロードする
            var (_, loadServiceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            (loadServiceProvider.GetService<ILoadRepository>() as LoadJsonFile).Load(json);
            var loadedPlayerInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>();
            
            //データを検証する
            foreach (var playerItem in playerItems)
            {
                var loadedInventory = loadedPlayerInventory.GetInventoryData(playerItem.Key);
                //インベントリのチェック
                for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
                {
                    if (playerItem.Value.ContainsKey(i))
                    {
                        Assert.AreEqual(playerItem.Value[i],loadedInventory.MainOpenableInventory.GetItem(i));
                        continue;
                    }
                    Assert.AreEqual(itemStackFactory.CreatEmpty(),loadedInventory.MainOpenableInventory.GetItem(i));
                }
            }
            
        }

        Dictionary<int, IItemStack> CreateSetItems(Random random,ItemStackFactory itemStackFactory)
        {
            var items = new Dictionary<int, IItemStack>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                if (random.Next(0,2) == 0)
                {
                    continue;
                }
                var id = random.Next(1,100);
                var count = random.Next(1,20);
                items.Add(i,itemStackFactory.Create(id,count));
            }

            return items;
        }
        
    }
}