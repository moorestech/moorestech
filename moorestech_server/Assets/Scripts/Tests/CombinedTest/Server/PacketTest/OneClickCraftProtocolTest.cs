using System.Linq;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.OneClickCraft;

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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            
            var craftElement = MasterHolder.CraftRecipeMaster.CraftRecipes.Data[CraftRecipeId];
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, craftElement.CraftRecipeGuid)).ToList());
            
            var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            Assert.AreEqual(0, playerInventoryData.MainOpenableInventory.GetItem(slot).Id.AsPrimitive());
            Assert.AreEqual(0, playerInventoryData.MainOpenableInventory.GetItem(slot).Count);
        }
        
        [Test]
        public void CanCraftTest()
        {
            //アイテムがあるときにクラフトできるかのテスト
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftElement = MasterHolder.CraftRecipeMaster.CraftRecipes.Data[CraftRecipeId];
            
            //必要なアイテムをインベントリに追加
            for (var i = 0; i < craftElement.RequiredItems.Length; i++)
            {
                var info = craftElement.RequiredItems[i];
                var item = ServerContext.ItemStackFactory.Create(info.ItemGuid, info.Count);
                playerInventoryData.MainOpenableInventory.SetItem(i, item);
            }
            
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, craftElement.CraftRecipeGuid)).ToList());
            
            var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            var resultItemGuid = MasterHolder.ItemMaster.GetItemId(craftElement.CraftResultItemGuid);
            Assert.AreEqual(resultItemGuid, playerInventoryData.MainOpenableInventory.GetItem(slot).Id);
            Assert.AreEqual(craftElement.CraftResultCount, playerInventoryData.MainOpenableInventory.GetItem(slot).Count);
        }
        
        [Test]
        public void CanNotOneItemIsMissingTest()
        {
            //アイテムが一つ足りないときにクラフトできないかのテスト
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftElement = MasterHolder.CraftRecipeMaster.CraftRecipes.Data[CraftRecipeId];
            
            //必要なアイテムをインベントリに追加
            for (var i = 0; i < craftElement.RequiredItems.Length; i++)
            {
                var info = craftElement.RequiredItems[i];
                var item = ServerContext.ItemStackFactory.Create(info.ItemGuid, info.Count);
                playerInventoryData.MainOpenableInventory.SetItem(i, item);
            }
            
            //一つのアイテムを消費
            var oneSubItem = playerInventoryData.MainOpenableInventory.GetItem(0).SubItem(1);
            playerInventoryData.MainOpenableInventory.SetItem(0, oneSubItem);
            
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, craftElement.CraftRecipeGuid)).ToList());
            
            //アイテムがクラフトされていないことをテスト
            var slot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            Assert.AreEqual(0, playerInventoryData.MainOpenableInventory.GetItem(slot).Id.AsPrimitive());
            Assert.AreEqual(0, playerInventoryData.MainOpenableInventory.GetItem(slot).Count);
        }
        
        [Test]
        public void ItemFullToCanNotCraftTest()
        {
            //グラブインベントリのアイテムが満杯の時にクラフトできないテスト
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInv = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var itemStackFactory = ServerContext.ItemStackFactory;
            var craftElement = MasterHolder.CraftRecipeMaster.CraftRecipes.Data[CraftRecipeId];
            
            
            //不要なアテムを追加
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var item = itemStackFactory.Create(new ItemId(10), 100);
                playerInv.MainOpenableInventory.SetItem(i, item);
            }
            
            
            //必要なアイテムをインベントリに追加
            for (var i = 0; i < craftElement.RequiredItems.Length; i++)
            {
                var info = craftElement.RequiredItems[i];
                var item = itemStackFactory.Create(info.ItemGuid, info.Count);
                playerInv.MainOpenableInventory.SetItem(i, item);
            }
            
            
            //クラフト実行
            var craftGuid = craftElement.CraftRecipeGuid;
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, craftGuid)).ToList());
            
            //アイテムが維持されていることをテスト
            for (var i = 0; i < craftElement.RequiredItems.Length; i++)
            {
                var info = craftElement.RequiredItems[i];
                var itemId = MasterHolder.ItemMaster.GetItemId(info.ItemGuid);
                Assert.AreEqual(itemId, playerInv.MainOpenableInventory.GetItem(i).Id.AsPrimitive());
                Assert.AreEqual(info.Count, playerInv.MainOpenableInventory.GetItem(i).Count);
            }
        }
    }
}