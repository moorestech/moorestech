using System.Linq;
using Core.Master;
using Game.Context;
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
        private const int PlayerId = 0;
        private const int CraftRecipeId = 1;
        
        [Test]
        public void CanNotCraftTest()
        {
            //アイテムがないときにクラフトできないかのテスト
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftConfig = ServerContext.CraftingConfig.GetCraftingConfigData(CraftRecipeId);
            
            //必要なアイテムをインベントリに追加
            for (var i = 0; i < craftConfig.CraftRequiredItemInfos.Count; i++)
            {
                var info = craftConfig.CraftRequiredItemInfos[i];
                playerInventoryData.MainOpenableInventory.SetItem(i, info.ItemStack);
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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftConfig = ServerContext.CraftingConfig.GetCraftingConfigData(CraftRecipeId);
            
            //必要なアイテムをインベントリに追加
            for (var i = 0; i < craftConfig.CraftRequiredItemInfos.Count; i++)
            {
                var info = craftConfig.CraftRequiredItemInfos[i];
                playerInventoryData.MainOpenableInventory.SetItem(i, info.ItemStack);
            }
            
            //一つのアイテムを消費
            var oneSubItem = playerInventoryData.MainOpenableInventory.GetItem(0).SubItem(1);
            playerInventoryData.MainOpenableInventory.SetItem(0, oneSubItem);
            
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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var playerInv = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftConfig = ServerContext.CraftingConfig.GetCraftingConfigData(CraftRecipeId);
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            
            //不要なアテムを追加
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var item = itemStackFactory.Create(new ItemId(10), 100);
                playerInv.MainOpenableInventory.SetItem(i, item);
            }
            
            
            //必要なアイテムをインベントリに追加
            for (var i = 0; i < craftConfig.CraftRequiredItemInfos.Count; i++)
            {
                var info = craftConfig.CraftRequiredItemInfos[i];
                playerInv.MainOpenableInventory.SetItem(i, info.ItemStack);
            }
            
            
            //クラフト実行
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, CraftRecipeId)).ToList());
            
            //アイテムが維持されていることをテスト
            for (var i = 0; i < craftConfig.CraftRequiredItemInfos.Count; i++)
            {
                var info = craftConfig.CraftRequiredItemInfos[i];
                Assert.AreEqual(info.ItemStack.Id, playerInv.MainOpenableInventory.GetItem(i).Id);
                Assert.AreEqual(info.ItemStack.Count, playerInv.MainOpenableInventory.GetItem(i).Count);
            }
        }
    }
}