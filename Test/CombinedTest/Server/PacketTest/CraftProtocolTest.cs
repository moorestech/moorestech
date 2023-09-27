#if NET6_0
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Item.Config;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Event;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;
using Server.Protocol.PacketResponse;

using Server.Util;

using Test.Module.TestMod;


namespace Test.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// クラフト実行プロトコルのテストと、クラフト時のイベント発火テスト
    /// </summary>
    public class CraftProtocolTest
    {
        private const int PlayerId = 1;
        private const int TestCraftItemId = 6;
        
        [Test]
        public void CraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            //クラフトインベントリの作成
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).GrabInventory;
            //CraftConfigの作成
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[0];
            var craftingEvent = serviceProvider.GetService<ICraftingEvent>();
            
            //craftingInventoryにアイテムを入れる
            for (int i = 0; i < craftConfig.CraftItemInfos.Count; i++)
            {
                craftInventory.SetItem(i,craftConfig.CraftItemInfos[i].ItemStack);
            }
            
            
            //イベントをサブスクライブ
            craftingEvent.Subscribe(i =>
            {
                //イベントのアイテムが正しく取得できているか検証
                Assert.AreEqual(craftConfig.Result.Id,i.itemId);
                Assert.AreEqual(craftConfig.Result.Count,i.itemCount);
            });
            
            
            //プロトコルでクラフト実行
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId,0)).ToList());
            
            
            //クラフト結果がResultSlotにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result,grabInventory.GetItem(0));
        }

        [Test]
        public void AllCraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var itemConfig = serviceProvider.GetService<IItemConfig>();
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[2]; //id2のレシピはこのテスト用のレシピ
            var craftingEvent = serviceProvider.GetService<ICraftingEvent>();

            //craftingInventoryに2つ分のアイテムを入れる
            craftInventory.SetItem(0,itemStackFactory.Create(TestCraftItemId,2));
            
            
            //イベントをサブスクライブ
            craftingEvent.Subscribe(i =>
            {
                //イベントのアイテムが正しく取得できているか検証
                Assert.AreEqual(craftConfig.Result.Id,i.itemId);
                Assert.AreEqual(160,i.itemCount);
            });
            
            //プロトコルでクラフト実行
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId,1)).ToList());
            
            
            //クラフト結果がメインインベントリにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result.Id,mainInventory.GetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(0)).Id);
            Assert.AreEqual(100,mainInventory.GetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(0) ).Count);
            Assert.AreEqual(60,mainInventory.GetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(1) ).Count);
        }

        [Test]
        public void OneStackCraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[2]; //id2のレシピはこのテスト用のレシピ
            var craftingEvent = serviceProvider.GetService<ICraftingEvent>();
            
            //craftingInventoryに2つ分のアイテムを入れる
            craftInventory.SetItem(0,itemStackFactory.Create(TestCraftItemId,2));
            
            
            craftingEvent.Subscribe(i =>
            {
                //イベントのアイテムが正しく取得できているか検証
                Assert.AreEqual(craftConfig.Result.Id,i.itemId);
                Assert.AreEqual(80,i.itemCount);
            });
            
            //プロトコルでクラフト実行
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId,2)).ToList());
            
            
            //クラフト結果がメインインベントリにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result.Id,mainInventory.GetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(0) ).Id);
            Assert.AreEqual(80,mainInventory.GetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(0) ).Count);
        }
    }
}
#endif