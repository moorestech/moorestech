using System.Collections.Generic;
using MainGame.GameLogic.Inventory;
using MainGame.Network.Event;
using MainGame.Network.Interface.Receive;
using MainGame.Network.Interface.Send;
using Maingame.Types;
using NUnit.Framework;

namespace Test.EditModeTest.GameLogic
{
    public class PlayerInventoryItemMoveTest
    {
        [Test]
        public void ItemMoveCountTest()
        {
            var sendItem = new SendItemMove();
            var items = new InventoryDataStoreCache(new PlayerInventoryUpdateEvent(),new InventoryUpdateEvent());
            
            var itemMove = new PlayerInventoryItemMove(sendItem,items);
            
            //移動アイテムを追加する
            items.UpdateSlotInventory(new OnPlayerInventorySlotUpdateProperties(0,new ItemStack(1,10)));
            items.UpdateSlotInventory(new OnPlayerInventorySlotUpdateProperties(1,new ItemStack(1,5)));
            items.UpdateSlotInventory(new OnPlayerInventorySlotUpdateProperties(2,new ItemStack(1,1)));
            
            
            //アイテムを移動する
            itemMove.MoveAllItemStack(1,15);
            //移動後のアイテムを確認する
            Assert.AreEqual(1,sendItem.FromSlot);
            Assert.AreEqual(15,sendItem.ToSlot);
            Assert.AreEqual(10,sendItem.ItemCount);
            
            
            //半分アイテムを移動する
            itemMove.MoveHalfItemStack(1,15); 
            Assert.AreEqual(1,sendItem.FromSlot);
            Assert.AreEqual(15,sendItem.ToSlot);
            Assert.AreEqual(5,sendItem.ItemCount);
            
            //一個アイテムを移動する
            itemMove.MoveOneItemStack(1,15);
            Assert.AreEqual(1,sendItem.FromSlot);
            Assert.AreEqual(15,sendItem.ToSlot);
            Assert.AreEqual(1,sendItem.ItemCount);
            
            
        }
    }

    class SendItemMove : ISendPlayerInventoryMoveItemProtocol
    {
        public int FromSlot;
        public int ToSlot;
        public int ItemCount;
        
        public void Send(int playerId, int fromSlot, int toSlot, int itemCount)
        {
        }
    }
}