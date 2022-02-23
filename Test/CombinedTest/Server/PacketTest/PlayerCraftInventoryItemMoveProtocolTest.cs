using System.Collections.Generic;
using Core.Item;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Util;

namespace Test.CombinedTest.Server.PacketTest
{
    public class PlayerCraftInventoryItemMoveProtocolTest
    {
        private const short PacketId = 13;
        
        [Test]
        public void InventoryItemMove()
        {
            int playerId = 1;
            int playerSlotIndex = 2;
            int blockInventorySlotIndex = 0;

            //初期設定----------------------------------------------------------

            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            //プレイヤーのインベントリの設定
            var craftingInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).CraftingOpenableInventory;


            //アイテムの設定
            craftingInventory.SetItem(0, itemStackFactory.Create(1, 5));
            craftingInventory.SetItem(1, itemStackFactory.Create(1, 1));
            craftingInventory.SetItem(2, itemStackFactory.Create(2, 1));

            //実際に移動させてテスト
            //全てのアイテムを移動させるテスト
            packet.GetPacketResponse(CraftInventoryItemMove(0, 3, 5, playerId));
            Assert.AreEqual(craftingInventory.GetItem(0), itemStackFactory.CreatEmpty());
            Assert.AreEqual(craftingInventory.GetItem(3), itemStackFactory.Create(1, 5));

            //一部のアイテムを移動させるテスト
            packet.GetPacketResponse(CraftInventoryItemMove(3, 0, 3, playerId));
            Assert.AreEqual(craftingInventory.GetItem(0), itemStackFactory.Create(1, 3));
            Assert.AreEqual(craftingInventory.GetItem(3), itemStackFactory.Create(1, 2));

            //一部のアイテムを移動しようとするが他にスロットがあるため失敗するテスト
            packet.GetPacketResponse(CraftInventoryItemMove(0, 2, 1, playerId));
            Assert.AreEqual(craftingInventory.GetItem(0), itemStackFactory.Create(1, 3));
            Assert.AreEqual(craftingInventory.GetItem(2), itemStackFactory.Create(2, 1));

            //全てのアイテムを移動させるテスト
            packet.GetPacketResponse(CraftInventoryItemMove(0, 2, 3, playerId));
            Assert.AreEqual(craftingInventory.GetItem(0), itemStackFactory.Create(2, 1));
            Assert.AreEqual(craftingInventory.GetItem(2), itemStackFactory.Create(1, 3));

            //アイテムを加算するテスト
            packet.GetPacketResponse(CraftInventoryItemMove(2, 1, 3, playerId));
            Assert.AreEqual(craftingInventory.GetItem(2), itemStackFactory.CreatEmpty());
            Assert.AreEqual(craftingInventory.GetItem(1), itemStackFactory.Create(1, 4));
            
            
            //全てのアイテムを同じスロットにアイテムを移動させるテスト
            packet.GetPacketResponse(CraftInventoryItemMove(1, 1, 4, playerId));
            Assert.AreEqual(craftingInventory.GetItem(1), itemStackFactory.Create(1, 4));
        }

        private List<byte> CraftInventoryItemMove(int fromSlot, int toSlot, int itemCount, int playerId)
        {
            var payload = new List<byte>();
            payload.AddRange(ToByteList.Convert((short) PacketId));
            payload.AddRange(ToByteList.Convert(playerId));
            payload.AddRange(ToByteList.Convert(fromSlot));
            payload.AddRange(ToByteList.Convert(toSlot));
            payload.AddRange(ToByteList.Convert(itemCount));
            return payload;
        }
    }
}