using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Inventory
{
    public class SharedInsertionOrderTest
    {
        [SetUp]
        public void Setup()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TestCase(true, 0, 1)]
        [TestCase(false, 1, 0)]
        public void 通常挿入は近傍順と単一スタック制約と通知順を保つ(bool multiple, int remainderCount, int nearCount)
        {
            var factory = ServerContext.ItemStackFactory;
            var changed = new List<int>();
            var inventory = new OpenableInventoryItemDataStoreService((slot, _) => changed.Add(slot), factory, 3,
                new OpenableInventoryItemDataStoreServiceOption { AllowMultipleStacksPerItemOnInsert = multiple });
            inventory.SetItem(2, ForUnitTestItemId.ComputeDataId, 99);
            changed.Clear();

            // [空,空,A99]は近傍枠1へだけ溢れ、単一制約なら余りを返す
            // [empty,empty,A99] spills only into nearby slot 1, or returns excess under the single-stack restriction
            var remainder = inventory.InsertItem(ForUnitTestItemId.ComputeDataId, 2);
            Assert.AreEqual(remainderCount, remainder.Count);
            CollectionAssert.AreEqual(new[] { 0, nearCount, 100 }, inventory.InventoryItems.Select(item => item.Count));
            CollectionAssert.AreEqual(multiple ? new[] { 2, 1 } : new[] { 2 }, changed);
        }

        [Test]
        public void 共通充填は指定した同種優先順で余りと総数を保つ()
        {
            var factory = ServerContext.ItemStackFactory;
            var item = ForUnitTestItemId.ComputeDataId;
            IList<IItemStack> slots = new List<IItemStack> { factory.CreatEmpty(), factory.CreatEmpty(), factory.Create(item, 99) };
            var order = new[] { 2, 0, 1 };
            Assert.AreEqual(0, InventoryInsertItem.InsertInOrder(factory.Create(item, 2), slots, order).Count);
            CollectionAssert.AreEqual(new[] { 1, 0, 100 }, slots.Select(stack => stack.Count));
            Assert.AreEqual(0, InventoryInsertItem.InsertInOrder(factory.Create(item, 100), slots, order).Count);
            var remainder = InventoryInsertItem.InsertInOrder(factory.Create(item, 100), slots, order);
            CollectionAssert.AreEqual(new[] { 100, 100, 100 }, slots.Select(stack => stack.Count));
            Assert.AreEqual(1, remainder.Count);
            Assert.AreEqual(301, slots.Sum(stack => stack.Count) + remainder.Count);
        }

        [Test]
        public void 挿入可否は在庫と通知を変更しない()
        {
            var factory = ServerContext.ItemStackFactory;
            var notifications = 0;
            var inventory = new OpenableInventoryItemDataStoreService((_, _) => notifications++, factory, 3);
            inventory.SetItem(2, ForUnitTestItemId.ComputeDataId, 99);
            var before = inventory.InventoryItems.ToArray();
            notifications = 0;
            Assert.IsTrue(inventory.InsertionCheck(new List<IItemStack> { factory.Create(ForUnitTestItemId.ComputeDataId, 2) }));
            CollectionAssert.AreEqual(before, inventory.InventoryItems);
            Assert.AreEqual(0, notifications);
        }
    }
}
