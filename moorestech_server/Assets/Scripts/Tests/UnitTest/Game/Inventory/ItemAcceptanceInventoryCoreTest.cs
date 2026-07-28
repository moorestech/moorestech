using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.Inventory
{
    /// <summary>
    ///     受入制限をインベントリ自身（Core.Inventory）が守ることを検証する
    ///     Verifies that the inventory itself (Core.Inventory) enforces the acceptance restriction
    /// </summary>
    public class ItemAcceptanceInventoryCoreTest
    {
        private static readonly ItemId AcceptableItemId = new(1);
        private static readonly ItemId RejectedItemId = new(2);

        [SetUp]
        public void SetUpServerContext()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 受入不可アイテムはInsertItem直呼びでも入らない()
        {
            var inventory = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 1, 3);

            var remainItem = inventory.InsertItem(RejectedItemId, 5);

            Assert.AreEqual(RejectedItemId, remainItem.Id);
            Assert.AreEqual(5, remainItem.Count);
            for (var slot = 0; slot < inventory.GetSlotSize(); slot++) Assert.AreEqual(0, inventory.GetItem(slot).Count);
        }

        [Test]
        public void InsertItem直呼びは既存スタックへ上限まで積み増しし総数が保存される()
        {
            // 上限3・スロット0に既存1個・投入5個 → 両スロットが上限まで埋まり総数6個が保たれる
            // Cap 3 with one existing item and 5 inserted: both slots fill to the cap and the total stays 6
            var inventory = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 3, 2);
            inventory.SetItem(0, AcceptableItemId, 1);

            var remainItem = inventory.InsertItem(AcceptableItemId, 5);

            Assert.AreEqual(3, inventory.GetItem(0).Count);
            Assert.AreEqual(3, inventory.GetItem(1).Count);
            Assert.AreEqual(0, remainItem.Count);
            Assert.AreEqual(6, CountItems(inventory, AcceptableItemId) + remainItem.Count);
        }

        [Test]
        public void 全スロットが上限に達したInsertItemは余りを返し増殖しない()
        {
            var inventory = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 1, 2);

            var remainItem = inventory.InsertItem(AcceptableItemId, 5);

            Assert.AreEqual(1, inventory.GetItem(0).Count);
            Assert.AreEqual(1, inventory.GetItem(1).Count);
            Assert.AreEqual(3, remainItem.Count);
            Assert.AreEqual(5, CountItems(inventory, AcceptableItemId) + remainItem.Count);
        }

        [Test]
        public void ReplaceItem直呼びは上限超過分を余りとして返す()
        {
            var inventory = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 3, 1);
            inventory.SetItem(0, AcceptableItemId, 1);

            var remainItem = inventory.ReplaceItem(0, AcceptableItemId, 5);

            Assert.AreEqual(3, inventory.GetItem(0).Count);
            Assert.AreEqual(AcceptableItemId, remainItem.Id);
            Assert.AreEqual(3, remainItem.Count);
        }

        [Test]
        public void 受入不可アイテムのReplaceItemは書き込まず入力をそのまま返す()
        {
            var inventory = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 3, 1);

            var remainItem = inventory.ReplaceItem(0, RejectedItemId, 2);

            Assert.AreEqual(0, inventory.GetItem(0).Count);
            Assert.AreEqual(RejectedItemId, remainItem.Id);
            Assert.AreEqual(2, remainItem.Count);
        }

        [Test]
        public void 空アイテムでのReplaceItemは制限に関わらず取り出せる()
        {
            var inventory = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 1, 1);
            inventory.SetItem(0, AcceptableItemId, 1);

            var takenItem = inventory.ReplaceItem(0, ServerContext.ItemStackFactory.CreatEmpty());

            Assert.AreEqual(AcceptableItemId, takenItem.Id);
            Assert.AreEqual(1, takenItem.Count);
            Assert.AreEqual(0, inventory.GetItem(0).Count);
        }

        [Test]
        public void InsertionCheckは受入制限を反映する()
        {
            var inventory = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 1, 3);
            var itemStackFactory = ServerContext.ItemStackFactory;

            Assert.IsFalse(inventory.InsertionCheck(new List<IItemStack> { itemStackFactory.Create(RejectedItemId, 1) }));
            Assert.IsFalse(inventory.InsertionCheck(new List<IItemStack> { itemStackFactory.Create(AcceptableItemId, 4) }));
            Assert.IsTrue(inventory.InsertionCheck(new List<IItemStack> { itemStackFactory.Create(AcceptableItemId, 3) }));

            // 検査だけでインベントリが書き換わっていないことを確認する
            // Ensure the check itself did not modify the inventory
            for (var slot = 0; slot < inventory.GetSlotSize(); slot++) Assert.AreEqual(0, inventory.GetItem(slot).Count);
        }

        [Test]
        public void Insertサービス経由でも上限を超えず総数が保存される()
        {
            var source = new OpenableInventoryItemDataStoreService((_, _) => { }, ServerContext.ItemStackFactory, 1);
            source.SetItem(0, AcceptableItemId, 5);
            var destination = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 3, 2);
            destination.SetItem(0, AcceptableItemId, 1);

            InventoryItemInsertService.Insert(source, 0, destination, 5);

            Assert.AreEqual(3, destination.GetItem(0).Count);
            Assert.AreEqual(3, destination.GetItem(1).Count);
            Assert.AreEqual(6, CountItems(destination, AcceptableItemId) + source.GetItem(0).Count);
        }

        [Test]
        public void 受入制限インベントリのソートはスロット配置も上限も壊さない()
        {
            // 上限1のスロットが3つ埋まった状態。整理すると1スロットへ結合され上限違反になる
            // Three slots each at the cap of 1; sorting would merge them into one slot and break the cap
            var inventory = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId, RejectedItemId }, 1, 3);
            inventory.SetItem(0, AcceptableItemId, 1);
            inventory.SetItem(1, RejectedItemId, 1);
            inventory.SetItem(2, AcceptableItemId, 1);

            InventorySortService.Sort(inventory, new List<int>());

            Assert.AreEqual(2, CountItems(inventory, AcceptableItemId));
            Assert.AreEqual(1, CountItems(inventory, RejectedItemId));
            for (var slot = 0; slot < inventory.GetSlotSize(); slot++) Assert.LessOrEqual(inventory.GetItem(slot).Count, 1);
            Assert.AreEqual(AcceptableItemId, inventory.GetItem(0).Id);
            Assert.AreEqual(RejectedItemId, inventory.GetItem(1).Id);
            Assert.AreEqual(AcceptableItemId, inventory.GetItem(2).Id);
        }

        private static int CountItems(IOpenableInventory inventory, ItemId itemId)
        {
            var totalCount = 0;
            for (var slot = 0; slot < inventory.GetSlotSize(); slot++)
                if (inventory.GetItem(slot).Id == itemId)
                    totalCount += inventory.GetItem(slot).Count;

            return totalCount;
        }
    }
}
