
using Core.Config.Item;
using Core.Item;
using Core.Item.Config;
using Core.Item.Implementation;
using Core.Item.Util;
using NUnit.Framework;
using PlayerInventory;
using PlayerInventory.Event;

namespace Test.UnitTest.Game
{
    public class PlayerInventoryTest
    {
        [Test]
        public void InsertToGetTest()
        {
            var itemConfig = new TestItemConfig();
            var itemStackFactory = new ItemStackFactory(itemConfig);
            
            var playerInventory = new PlayerInventoryData(0,new PlayerInventoryUpdateEvent(),itemStackFactory);
            int id = 5;
            var amount = itemConfig.GetItemConfig(id).Stack;
            //Insert test
            var result = playerInventory.InsertItem(0,itemStackFactory.Create(id,amount));
            Assert.AreEqual(ItemConst.NullItemId,result.Id);
            
            result = playerInventory.InsertItem(0,itemStackFactory.Create(id,amount));
            Assert.AreEqual(id,result.Id);
            Assert.AreEqual(amount,result.Amount);
            
            result = playerInventory.InsertItem(0,itemStackFactory.Create(id+1,1));
            Assert.AreEqual(id + 1,result.Id);
            Assert.AreEqual(1,result.Amount);

            //drop and inset item test
            result = playerInventory.DropItem(0, 3);
            Assert.AreEqual(id,result.Id);
            Assert.AreEqual(3,result.Amount);
            
            result = playerInventory.GetItem(0);
            Assert.AreEqual(id,result.Id);
            Assert.AreEqual(amount - 3,result.Amount);
            
            result = playerInventory.InsertItem(0,itemStackFactory.Create(id,amount));
            Assert.AreEqual(id,result.Id);
            Assert.AreEqual(amount - 3,result.Amount);
            
            result = playerInventory.DropItem(0, amount - 3);
            Assert.AreEqual(id,result.Id);
            Assert.AreEqual(amount - 3,result.Amount);
            


        }

        [Test]
        public void UseHotBarTest()
        {
            var itemStackFactory = new ItemStackFactory(new TestItemConfig());
            var playerInventory = new PlayerInventoryData(0,new PlayerInventoryUpdateEvent(),itemStackFactory);
            int id = 5;
            int amount = 3;
            int slot = 27;
            
            var result = playerInventory.InsertItem(slot,itemStackFactory.Create(id,amount));
            Assert.AreEqual(ItemConst.NullItemId,result.Id);
            
            result = playerInventory.UseHotBar(slot);
            Assert.AreEqual(id,result.Id);
            Assert.AreEqual(1,result.Amount);
            playerInventory.UseHotBar(slot);
            playerInventory.UseHotBar(slot);
            result = playerInventory.UseHotBar(slot);
            Assert.AreEqual(ItemConst.NullItemId,result.Id);

            result = playerInventory.GetItem(slot);
            Assert.AreEqual(ItemConst.NullItemId,result.Id);

        }
    }
}