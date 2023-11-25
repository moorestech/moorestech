using System;
using System.Data;
using System.Linq;
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
            
            //インベントリにアイテムがないことを確認
            foreach(var item in playerInventoryData.MainOpenableInventory.Items)
            {
                Assert.AreEqual(0, item.Count);
            }
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
            
            //インベントリにアイテムがあることを確認
            var resultItem = craftConfig.Result;
            //アイテムのインサートはホットバーから優先的にアイテムが入るので、ホットバーのインデックスをチェックする
            
            //TODO Grabインベントリに入っていることをテストする
            throw new NotImplementedException();
            
            var hotBarSlot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            Assert.AreEqual(resultItem.Id, playerInventoryData.MainOpenableInventory.GetItem(hotBarSlot).Id);
            Assert.AreEqual(resultItem.Count, playerInventoryData.MainOpenableInventory.GetItem(hotBarSlot).Count);
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
        public void ItemFullToCnantCraftTest()
        {
            //TODO アイテムが満杯の時にクラフトできないテスト
            throw new NotImplementedException();
        }

        public void GrabInventoryFukllTocanNotCraftTest()
        {
            //TODO Garbインベントリが満杯の時にクラフトできないテスト
            throw new NotImplementedException();
        }
    }
}