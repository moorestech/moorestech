using System;
using System.Data;
using System.Linq;
using Core.Item;
using Core.Item.Config;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class OneClickCraftProtocolTest
    {
        const int PlayerId = 0;
        const int CraftRecipeId = 1;
        
        [Test]
        public void CanNotCraftTest()
        {
            //アイテムがないときにクラフトできないかのテスト
            var (packet, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, CraftRecipeId)).ToList());
            
            var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            Assert.AreEqual(0, playerInventoryData.MainOpenableInventory.GetItem(slot).Id);
            Assert.AreEqual(0, playerInventoryData.MainOpenableInventory.GetItem(slot).Count);
        }
        
        [Test]
        public void CanCraftTest()
        {
            //アイテムがあるときにクラフトできるかのテスト
            var (packet, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigData(CraftRecipeId);
            
            //必要なアイテムをインベントリに追加
            for (var i = 0; i < craftConfig.CraftItems.Count; i++)
            {
                var item = craftConfig.CraftItems[i];
                playerInventoryData.MainOpenableInventory.SetItem(i,item);
            }

            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, CraftRecipeId)).ToList());
            
            var resultItem = craftConfig.ResultItem;
            
            var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            Assert.AreEqual(resultItem.Id, playerInventoryData.MainOpenableInventory.GetItem(slot).Id);
            Assert.AreEqual(resultItem.Count, playerInventoryData.MainOpenableInventory.GetItem(slot).Count);
        }

        [Test]
        public void CanNotOneItemIsMissingTest()
        {
            //アイテムが一つ足りないときにクラフトできないかのテスト
            var (packet, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigData(CraftRecipeId);
            
            //必要なアイテムをインベントリに追加
            for (var i = 0; i < craftConfig.CraftItems.Count; i++)
            {
                var item = craftConfig.CraftItems[i];
                playerInventoryData.MainOpenableInventory.SetItem(i,item);
            }

            //一つのアイテムを消費
            var oneSubItem = playerInventoryData.MainOpenableInventory.GetItem(0).SubItem(1);
            playerInventoryData.MainOpenableInventory.SetItem(0,oneSubItem);
            
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, CraftRecipeId)).ToList());
            
            //アイテムがクラフトされていないことをテスト
            var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            Assert.AreEqual(0, playerInventoryData.MainOpenableInventory.GetItem(slot).Id);
            Assert.AreEqual(0, playerInventoryData.MainOpenableInventory.GetItem(slot).Count);

        }

        [Test]
        public void ItemFullToCanNotCraftTest()
        {
            //グラブインベントリのアイテムが満杯の時にクラフトできないテスト
            var (packet, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInv = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigData(CraftRecipeId);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            
            //不要なアテムを追加
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var item = itemStackFactory.Create(10, 100);
                playerInv.MainOpenableInventory.SetItem(i,item);
            }
            
            
            //必要なアイテムをインベントリに追加
            for (var i = 0; i < craftConfig.CraftItems.Count; i++)
            {
                var item = craftConfig.CraftItems[i];
                playerInv.MainOpenableInventory.SetItem(i,item);
            }

            
            //クラフト実行
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, CraftRecipeId)).ToList());
            
            //アイテムが維持されていることをテスト
            for (int i = 0; i < craftConfig.CraftItems.Count; i++)
            {
                var item = craftConfig.CraftItems[i];
                Assert.AreEqual(item.Id, playerInv.MainOpenableInventory.GetItem(i).Id);
                Assert.AreEqual(item.Count, playerInv.MainOpenableInventory.GetItem(i).Count);
            }
        }
    }
}