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
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, CraftRecipeId)).ToList());
            
            //Grabインベントリにアイテムがないことを確認
            
            Assert.AreEqual(0, playerInventoryData.GrabInventory.GetItem(0).Id);
            Assert.AreEqual(0, playerInventoryData.GrabInventory.GetItem(0).Count);
        }
        
        [Test]
        public void CanCraftTest()
        {
            //アイテムがあるときにクラフトできるかのテスト
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigData(CraftRecipeId);
            
            //必要なアイテムをインベントリに追加
            foreach (var item in craftConfig.CraftItems)
            {
                playerInventoryData.MainOpenableInventory.InsertItem(item);
            }
            
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, CraftRecipeId)).ToList());
            
            //Grabインベントリにアイテムがあることを確認
            var resultItem = craftConfig.Result;
            
            Assert.AreEqual(resultItem.Id, playerInventoryData.GrabInventory.GetItem(0).Id);
            Assert.AreEqual(resultItem.Count, playerInventoryData.GrabInventory.GetItem(0).Count);
        }

        [Test]
        public void CanNotOneItemIsMissingTest()
        {
            //アイテムが一つ足りないときにクラフトできないかのテスト
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigData(CraftRecipeId);
            
            //必要なアイテムをインベントリに追加
            foreach (var item in craftConfig.CraftItems)
            {
                playerInventoryData.MainOpenableInventory.InsertItem(item);
            }
            
            //アイテムのインサートはホットバーから優先的にアイテムが入るので、ホットバーのインデックスをチェックする
            var hotBarSlot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            //一つのアイテムを消費
            var oneSubItem = playerInventoryData.MainOpenableInventory.GetItem(hotBarSlot).SubItem(1);
            playerInventoryData.MainOpenableInventory.SetItem(hotBarSlot,oneSubItem);
            
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, CraftRecipeId)).ToList());
            
            
            //Grabインベントリにアイテムがないことを確認
            var resultItem = craftConfig.Result;
            
            Assert.AreEqual(0, playerInventoryData.GrabInventory.GetItem(0).Id);
            Assert.AreEqual(0, playerInventoryData.GrabInventory.GetItem(0).Count);
            
            //インベントリに結果アイテムがないことを確認
            foreach(var item in playerInventoryData.MainOpenableInventory.Items)
            {
                if (item.Id == craftConfig.Result.Id)
                {
                    Assert.Fail();
                }
            }
            Assert.Pass();
        }

        [Test]
        public void ItemFullToCanNotCraftTest()
        {
            //グラブインベントリのアイテムが満杯の時にクラフトできないテスト
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInv = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigData(CraftRecipeId);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            //必要なアイテムをインベントリに追加
            for (var i = 0; i < craftConfig.CraftItems.Count; i++)
            {
                var item = craftConfig.CraftItems[i];
                playerInv.MainOpenableInventory.SetItem(i,item);
            }

            //グラブインベントリにアイテムを追加
            playerInv.GrabInventory.SetItem(0,itemStackFactory.Create(10,100));
            
            //クラフト実行
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, CraftRecipeId)).ToList());
            
            //アイテムが維持されていることをテスト
            Assert.AreEqual(10, playerInv.GrabInventory.GetItem(0).Id);
            Assert.AreEqual(100, playerInv.GrabInventory.GetItem(0).Count);
            
            //プレイヤーインベントリ
            for (int i = 0; i < craftConfig.CraftItems.Count; i++)
            {
                var item = craftConfig.CraftItems[i];
                Assert.AreEqual(item.Id, playerInv.MainOpenableInventory.GetItem(i).Id);
                Assert.AreEqual(item.Count, playerInv.MainOpenableInventory.GetItem(i).Count);
            }
        }
    }
}