using Core.Config.Item;
using Core.Item;
using Core.Item.Config;
using Core.Item.Implementation;
using Core.Item.Util;
using Game.PlayerInventory.Interface;
using NUnit.Framework;
using PlayerInventory;
using PlayerInventory.Event;
using Test.Module;

namespace Test.UnitTest.Game
{
    public class PlayerInventoryTest
    {
        [Test]
        public void InsertToGetTest()
        {
            var itemConfig = new TestItemConfig();
            var itemStackFactory = new ItemStackFactory(itemConfig);

            var playerInventory = new PlayerInventoryData(0, new PlayerInventoryUpdateEvent(), itemStackFactory);
            int id = 5;
            var count = itemConfig.GetItemConfig(id).Stack;
            //Insert test
            var result = playerInventory.InsertItem(0, itemStackFactory.Create(id, count));
            Assert.AreEqual(ItemConst.EmptyItemId, result.Id);

            result = playerInventory.InsertItem(0, itemStackFactory.Create(id, count));
            Assert.AreEqual(id, result.Id);
            Assert.AreEqual(count, result.Count);

            result = playerInventory.InsertItem(0, itemStackFactory.Create(id + 1, 1));
            Assert.AreEqual(id + 1, result.Id);
            Assert.AreEqual(1, result.Count);

            //drop and inset item test
            result = playerInventory.DropItem(0, 3);
            Assert.AreEqual(id, result.Id);
            Assert.AreEqual(3, result.Count);

            result = playerInventory.GetItem(0);
            Assert.AreEqual(id, result.Id);
            Assert.AreEqual(count - 3, result.Count);

            result = playerInventory.InsertItem(0, itemStackFactory.Create(id, count));
            Assert.AreEqual(id, result.Id);
            Assert.AreEqual(count - 3, result.Count);

            result = playerInventory.DropItem(0, count - 3);
            Assert.AreEqual(id, result.Id);
            Assert.AreEqual(count - 3, result.Count);
        }

        [Test]
        public void UseHotBarTest()
        {
            var itemStackFactory = new ItemStackFactory(new TestItemConfig());
            var playerInventory = new PlayerInventoryData(0, new PlayerInventoryUpdateEvent(), itemStackFactory);
            int id = 5;
            int count = 3;
            int slot = 27;

            var result = playerInventory.InsertItem(slot, itemStackFactory.Create(id, count));
            Assert.AreEqual(ItemConst.EmptyItemId, result.Id);

            result = playerInventory.UseHotBar(slot);
            Assert.AreEqual(id, result.Id);
            Assert.AreEqual(1, result.Count);
            playerInventory.UseHotBar(slot);
            playerInventory.UseHotBar(slot);
            result = playerInventory.UseHotBar(slot);
            Assert.AreEqual(ItemConst.EmptyItemId, result.Id);

            result = playerInventory.GetItem(slot);
            Assert.AreEqual(ItemConst.EmptyItemId, result.Id);
        }
    }
}