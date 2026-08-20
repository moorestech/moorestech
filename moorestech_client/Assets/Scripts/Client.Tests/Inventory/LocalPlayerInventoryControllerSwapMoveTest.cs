using System;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Inventory
{
    /// <summary>
    ///     別アイテムIDのスロットへ移動したときの楽観更新が、サーバーのSwapSlotと同じ結果になることを検証する
    ///     Verifies that the optimistic update for a move onto a different-id slot matches the server's SwapSlot outcome
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
        public void 同アイテムのスロットへの移動は従来どおり加算される()
        {
            var controller = CreateController();
            controller.SetGrabItem(ServerContext.ItemStackFactory.Create(ItemId(HeldItemGuid), 4));
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(ItemId(HeldItemGuid), 7));

            controller.MoveItem(LocalMoveInventoryType.Grab, 0, LocalMoveInventoryType.MainOrSub, MainSlot, 4, isMoveSendData: false);

            Assert.AreEqual(11, controller.LocalPlayerInventory[MainSlot].Count);
            Assert.AreEqual(0, controller.GrabInventory.Count);
        }

        private LocalPlayerInventoryController CreateController()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return new LocalPlayerInventoryController(new LocalPlayerInventory(), new LocalPlayerEquipment());
        }

        private ItemId ItemId(Guid itemGuid)
        {
            return MasterHolder.ItemMaster.GetItemId(itemGuid);
        }
    }
}
