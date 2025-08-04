using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class AssemblePlayerInventorySaveJsonTextTest
    {
        [Test]
        public void OnePlayerTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var playerInventory = saveServiceProvider.GetService<IPlayerInventoryDataStore>();
            var itemStackFactory = ServerContext.ItemStackFactory;
            var assembleJsonText = saveServiceProvider.GetService<AssembleSaveJsonText>();
            
            var playerEntityId = 100;
            
            //プレイヤーインベントリの作成
            var inventory = playerInventory.GetInventoryData(playerEntityId);
            
            //セットするアイテムを定義する
            var mainItems = new Dictionary<int, IItemStack>();
            mainItems.Add(0, itemStackFactory.Create(new ItemId(2), 10));
            mainItems.Add(10, itemStackFactory.Create(new ItemId(5), 1));
            mainItems.Add(30, itemStackFactory.Create(new ItemId(10), 10));
            mainItems.Add(PlayerInventoryConst.MainInventorySize - 1, itemStackFactory.Create(new ItemId(12), 11));
            
            var craftItems = new Dictionary<int, IItemStack>();
            craftItems.Add(0, itemStackFactory.Create(new ItemId(2), 5));
            craftItems.Add(1, itemStackFactory.Create(new ItemId(3), 4));
            craftItems.Add(7, itemStackFactory.Create(new ItemId(4), 7));
            
            //メインアイテムをセットする
            foreach (var item in mainItems) inventory.MainOpenableInventory.SetItem(item.Key, item.Value);
            
            
            //セーブする
            var json = assembleJsonText.AssembleSaveJson();
            
            
            //セーブしたデータをロードする
            var (_, loadServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);
            var loadedPlayerInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>()
                .GetInventoryData(playerEntityId);
            
            //メインのインベントリのチェック
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                if (mainItems.ContainsKey(i))
                {
                    Assert.AreEqual(mainItems[i], loadedPlayerInventory.MainOpenableInventory.GetItem(i));
                    continue;
                }
                
                Assert.AreEqual(itemStackFactory.CreatEmpty(), loadedPlayerInventory.MainOpenableInventory.GetItem(i));
            }
        }
        
        /// <summary>
        ///     複数ユーザーの時インベントリのデータが正しくセーブできるか
        /// </summary>
        [Test]
        public void MultiplePlayerSaveTest()
        {
            var (_, saveServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            var playerInventory = saveServiceProvider.GetService<IPlayerInventoryDataStore>();
            var itemStackFactory = ServerContext.ItemStackFactory;
            var seed = 13143;
            
            //プレイヤーのインベントリを作成
            var playerItems = new Dictionary<int, Dictionary<int, IItemStack>>();
            var random = new Random(seed);
            for (var i = 0; i < 20; i++)
            {
                var playerId = random.Next();
                playerItems.Add(playerId, CreateSetItems(random, itemStackFactory));
            }
            
            //プレイヤーインベントリにアイテムをセットする
            foreach (var playerItem in playerItems)
            {
                var inventory = playerInventory.GetInventoryData(playerItem.Key);
                foreach (var item in playerItem.Value) inventory.MainOpenableInventory.SetItem(item.Key, item.Value);
            }
            
            //セーブする
            var json = saveServiceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();
            
            
            //セーブしたデータをロードする
            var (_, loadServiceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);
            var loadedPlayerInventory = loadServiceProvider.GetService<IPlayerInventoryDataStore>();
            
            //データを検証する
            foreach (var playerItem in playerItems)
            {
                var loadedInventory = loadedPlayerInventory.GetInventoryData(playerItem.Key);
                //インベントリのチェック
                for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
                {
                    if (playerItem.Value.ContainsKey(i))
                    {
                        Assert.AreEqual(playerItem.Value[i], loadedInventory.MainOpenableInventory.GetItem(i));
                        continue;
                    }
                    
                    Assert.AreEqual(itemStackFactory.CreatEmpty(), loadedInventory.MainOpenableInventory.GetItem(i));
                }
            }
        }
        
        private Dictionary<int, IItemStack> CreateSetItems(Random random, IItemStackFactory itemStackFactory)
        {
            var items = new Dictionary<int, IItemStack>();
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                if (random.Next(0, 2) == 0) continue;
                var id = new ItemId(random.Next(1, 20));
                var count = random.Next(1, 20);
                items.Add(i, itemStackFactory.Create(id, count));
            }
            
            return items;
        }
    }
}