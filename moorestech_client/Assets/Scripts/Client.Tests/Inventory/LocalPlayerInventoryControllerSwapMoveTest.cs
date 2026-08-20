using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface.Subscription;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Inventory
{
    /// <summary>
    ///     別ID移動時の楽観更新がSwapSlotと一致するか検証
    ///     Verifies the optimistic update for a different-id move matches SwapSlot
    /// </summary>
    public class LocalPlayerInventoryControllerSwapMoveTest
    {
        private const int MainSlot = 5;
        private static readonly Guid HeldItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid PlacedItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        [Test]
        public void grabの全量を別アイテムのスロットへ移動すると入れ替わる()
        {
            var controller = CreateController();
            controller.SetGrabItem(ServerContext.ItemStackFactory.Create(ItemId(HeldItemGuid), 4));
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(ItemId(PlacedItemGuid), 7));

            // 送信を切ってローカル反映だけを見る（サーバー往復はInventoryItemMoveProtocolTestが担う）
            // Sending is disabled so only the local application is observed (the server round trip is InventoryItemMoveProtocolTest's job)
            controller.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, MainSlot, 4, isMoveSendData: false);

            Assert.AreEqual(ItemId(HeldItemGuid), controller.LocalPlayerInventory[MainSlot].Id);
            Assert.AreEqual(4, controller.LocalPlayerInventory[MainSlot].Count);
            Assert.AreEqual(ItemId(PlacedItemGuid), controller.GrabInventory.Id);
            Assert.AreEqual(7, controller.GrabInventory.Count);
        }

        [Test]
        public void 別アイテムのスロットへの部分移動は何も起きない()
        {
            var controller = CreateController();
            controller.SetGrabItem(ServerContext.ItemStackFactory.Create(ItemId(HeldItemGuid), 4));
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(ItemId(PlacedItemGuid), 7));

            controller.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, MainSlot, 1, isMoveSendData: false);

            Assert.AreEqual(ItemId(PlacedItemGuid), controller.LocalPlayerInventory[MainSlot].Id);
            Assert.AreEqual(7, controller.LocalPlayerInventory[MainSlot].Count);
            Assert.AreEqual(ItemId(HeldItemGuid), controller.GrabInventory.Id);
            Assert.AreEqual(4, controller.GrabInventory.Count);
        }

        [Test]
        public void 移動元が空なら行先を吸い出さない()
        {
            var controller = CreateController();
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(ItemId(PlacedItemGuid), 7));

            // grabは空のまま。count=0の移動が行先スタックを吸い出さないことを確認する
            // grab stays empty; verify a count=0 move never drains the target stack
            controller.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, MainSlot, 0, isMoveSendData: false);

            Assert.AreEqual(ItemId(PlacedItemGuid), controller.LocalPlayerInventory[MainSlot].Id);
            Assert.AreEqual(7, controller.LocalPlayerInventory[MainSlot].Count);
            Assert.AreEqual(0, controller.GrabInventory.Count);
        }

        [Test]
        public void 同アイテムのスロットへの移動は従来どおり加算される()
        {
            var controller = CreateController();
            controller.SetGrabItem(ServerContext.ItemStackFactory.Create(ItemId(HeldItemGuid), 4));
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(ItemId(HeldItemGuid), 7));

            controller.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, MainSlot, 4, isMoveSendData: false);

            Assert.AreEqual(ItemId(HeldItemGuid), controller.LocalPlayerInventory[MainSlot].Id);
            Assert.AreEqual(11, controller.LocalPlayerInventory[MainSlot].Count);
            Assert.AreEqual(0, controller.GrabInventory.Count);
        }

        [Test]
        public void grabの全量を別アイテムの装備スロットへ移動すると入れ替わる()
        {
            var (controller, equipment) = CreateControllerWithEquipment();
            const int equipmentSlot = 0;
            controller.SetGrabItem(ServerContext.ItemStackFactory.Create(ItemId(HeldItemGuid), 4));
            equipment.ApplySlotUpdate(equipmentSlot, ServerContext.ItemStackFactory.Create(ItemId(PlacedItemGuid), 7));

            controller.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.Equipment, equipmentSlot, 4, isMoveSendData: false);

            Assert.AreEqual(ItemId(HeldItemGuid), equipment.Slots[equipmentSlot].Id);
            Assert.AreEqual(4, equipment.Slots[equipmentSlot].Count);
            Assert.AreEqual(ItemId(PlacedItemGuid), controller.GrabInventory.Id);
            Assert.AreEqual(7, controller.GrabInventory.Count);
        }

        [Test]
        public void grabの全量を別アイテムのサブインベントリスロットへ移動すると入れ替わる()
        {
            const int subSlot = 0;
            var subInventory = new FakeSubInventory(1);
            var controller = CreateController();
            controller.SetSubInventory(subInventory);
            var combinedSlot = controller.LocalPlayerInventory.MainSlotCount + subSlot;
            controller.SetGrabItem(ServerContext.ItemStackFactory.Create(ItemId(HeldItemGuid), 4));
            subInventory.SubInventory[subSlot] = ServerContext.ItemStackFactory.Create(ItemId(PlacedItemGuid), 7);

            controller.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, combinedSlot, 4, isMoveSendData: false);

            Assert.AreEqual(ItemId(HeldItemGuid), controller.LocalPlayerInventory[combinedSlot].Id);
            Assert.AreEqual(4, controller.LocalPlayerInventory[combinedSlot].Count);
            Assert.AreEqual(ItemId(PlacedItemGuid), controller.GrabInventory.Id);
            Assert.AreEqual(7, controller.GrabInventory.Count);
        }

        private LocalPlayerInventoryController CreateController()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return new LocalPlayerInventoryController(new LocalPlayerInventory(), new LocalPlayerEquipment());
        }

        private (LocalPlayerInventoryController controller, LocalPlayerEquipment equipment) CreateControllerWithEquipment()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var equipment = new LocalPlayerEquipment();
            return (new LocalPlayerInventoryController(new LocalPlayerInventory(), equipment), equipment);
        }

        // MoveItem結合indexのサブインベントリ用スタブ
        // A test-only stub for MoveItem's combined-index sub-inventory path
        private class FakeSubInventory : ISubInventory
        {
            public FakeSubInventory(int count)
            {
                Count = count;
                SubInventory = new List<IItemStack>();
                for (var i = 0; i < count; i++) SubInventory.Add(ServerContext.ItemStackFactory.CreatEmpty());
                SubInventorySlotObjects = new List<ItemSlotView>();
            }

            public List<IItemStack> SubInventory { get; }
            public IReadOnlyList<ItemSlotView> SubInventorySlotObjects { get; }
            public int Count { get; }
            public ISubInventoryIdentifier ISubInventoryIdentifier => null;
        }

        private ItemId ItemId(Guid itemGuid)
        {
            return MasterHolder.ItemMaster.GetItemId(itemGuid);
        }
    }
}
