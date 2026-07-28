using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     IItemAcceptanceInventoryの受入制限を移動サービスが尊重することを検証する
    ///     Verifies that the move services honor the restrictions declared by IItemAcceptanceInventory
    /// </summary>
    public class ItemAcceptanceInventoryTest
    {
        private static readonly ItemId AcceptableItemId = new(1);
        private static readonly ItemId RejectedItemId = new(2);

        [SetUp]
        public void SetUpServerContext()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 受入不可アイテムの移動は何も起きない()
        {
            var source = CreateSourceInventory(1);
            source.SetItem(0, RejectedItemId, 5);
            var destination = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 1, 1);

            InventoryItemMoveService.Move(source, 0, destination, 0, 5);

            AssertSlot(source, 0, RejectedItemId, 5);
            Assert.AreEqual(0, destination.GetItem(0).Count);
        }

        [Test]
        public void スロット上限1のインベントリへスタック移動すると1個だけ入り残りは元に残る()
        {
            var source = CreateSourceInventory(1);
            source.SetItem(0, AcceptableItemId, 5);
            var destination = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 1, 1);

            InventoryItemMoveService.Move(source, 0, destination, 0, 5);

            AssertSlot(destination, 0, AcceptableItemId, 1);
            AssertSlot(source, 0, AcceptableItemId, 4);
        }

        [Test]
        public void 上限に達したスロットへの追加移動は何も起きない()
        {
            var source = CreateSourceInventory(1);
            source.SetItem(0, AcceptableItemId, 5);
            var destination = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 1, 1);
            destination.SetItem(0, AcceptableItemId, 1);

            InventoryItemMoveService.Move(source, 0, destination, 0, 5);

            AssertSlot(destination, 0, AcceptableItemId, 1);
            AssertSlot(source, 0, AcceptableItemId, 5);
        }

        [Test]
        public void 受入不可アイテムを移動先に置く入れ替えは実行されない()
        {
            var source = CreateSourceInventory(1);
            source.SetItem(0, RejectedItemId, 1);
            var destination = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 1, 1);
            destination.SetItem(0, AcceptableItemId, 1);

            InventoryItemMoveService.Move(source, 0, destination, 0, 1);

            AssertSlot(source, 0, RejectedItemId, 1);
            AssertSlot(destination, 0, AcceptableItemId, 1);
        }

        [Test]
        public void 受入制限インベントリが移動元の入れ替えでも受入不可アイテムは戻ってこない()
        {
            // 制限インベントリ側にも書き戻しが発生するswapの逆方向を検証する
            // Verifies the reverse swap direction that also writes back into the restricted inventory
            var restricted = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 1, 1);
            restricted.SetItem(0, AcceptableItemId, 1);
            var other = CreateSourceInventory(1);
            other.SetItem(0, RejectedItemId, 1);

            InventoryItemMoveService.Move(restricted, 0, other, 0, 1);

            AssertSlot(restricted, 0, AcceptableItemId, 1);
            AssertSlot(other, 0, RejectedItemId, 1);
        }

        [Test]
        public void スロット上限を超える個数の入れ替えは実行されない()
        {
            var source = CreateSourceInventory(1);
            source.SetItem(0, AcceptableItemId, 3);
            var destination = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId, RejectedItemId }, 1, 1);
            destination.SetItem(0, RejectedItemId, 1);

            InventoryItemMoveService.Move(source, 0, destination, 0, 3);

            AssertSlot(source, 0, AcceptableItemId, 3);
            AssertSlot(destination, 0, RejectedItemId, 1);
        }

        [Test]
        public void 制約を満たす入れ替えは実行される()
        {
            var source = CreateSourceInventory(1);
            source.SetItem(0, AcceptableItemId, 1);
            var destination = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId, RejectedItemId }, 1, 1);
            destination.SetItem(0, RejectedItemId, 1);

            InventoryItemMoveService.Move(source, 0, destination, 0, 1);

            AssertSlot(source, 0, RejectedItemId, 1);
            AssertSlot(destination, 0, AcceptableItemId, 1);
        }

        [Test]
        public void 受入不可アイテムのInsertは何も起きない()
        {
            var source = CreateSourceInventory(1);
            source.SetItem(0, RejectedItemId, 5);
            var destination = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 1, 3);

            InventoryItemInsertService.Insert(source, 0, destination, 5);

            AssertSlot(source, 0, RejectedItemId, 5);
            for (var slot = 0; slot < destination.GetSlotSize(); slot++) Assert.AreEqual(0, destination.GetItem(slot).Count);
        }

        [Test]
        public void スロット上限1のインベントリへのInsertは各スロットに1個ずつしか入らない()
        {
            var source = CreateSourceInventory(1);
            source.SetItem(0, AcceptableItemId, 5);
            var destination = new FakeAcceptanceInventory(new List<ItemId> { AcceptableItemId }, 1, 3);

            InventoryItemInsertService.Insert(source, 0, destination, 5);

            for (var slot = 0; slot < destination.GetSlotSize(); slot++) AssertSlot(destination, slot, AcceptableItemId, 1);
            AssertSlot(source, 0, AcceptableItemId, 2);
        }

        private static OpenableInventoryItemDataStoreService CreateSourceInventory(int slotCount)
        {
            return new OpenableInventoryItemDataStoreService(IgnoreInventoryUpdate, ServerContext.ItemStackFactory, slotCount);
        }

        private static void IgnoreInventoryUpdate(int slot, IItemStack itemStack)
        {
        }

        private static void AssertSlot(IOpenableInventory inventory, int slot, ItemId expectedItemId, int expectedCount)
        {
            Assert.AreEqual(expectedItemId, inventory.GetItem(slot).Id);
            Assert.AreEqual(expectedCount, inventory.GetItem(slot).Count);
        }
    }
}
