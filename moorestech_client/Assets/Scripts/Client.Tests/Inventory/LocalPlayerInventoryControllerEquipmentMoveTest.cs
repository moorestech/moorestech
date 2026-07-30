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
    ///     装備スロットを移動元・移動先にしたローカル移動が成立することを検証する
    ///     Verifies that local moves with an equipment slot as source or destination actually apply
    /// </summary>
    public class LocalPlayerInventoryControllerEquipmentMoveTest
    {
        private const int MainSlot = 5;
        private const int EquipmentSlot = 1;
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void メインから装備へ移動すると装備に入りメインが減る()
        {
            var (controller, equipment) = CreateController();
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(ToolItemId(), 10));

            // 送信を切ってローカル反映だけを見る（サーバー往復はEquipmentInventoryTestが担う）
            // Sending is disabled so only the local application is observed (the server round trip is EquipmentInventoryTest's job)
            controller.MoveItem(LocalMoveInventoryType.MainOrSub, MainSlot, LocalMoveInventoryType.Equipment, EquipmentSlot, 4, isMoveSendData: false);

            Assert.AreEqual(ToolItemId(), equipment.Slots[EquipmentSlot].Id);
            Assert.AreEqual(4, equipment.Slots[EquipmentSlot].Count);
            Assert.AreEqual(6, controller.LocalPlayerInventory[MainSlot].Count);
        }

        [Test]
        public void 装備からメインへ移動すると装備が空になりメインが増える()
        {
            var (controller, equipment) = CreateController();
            equipment.ApplySlotUpdate(EquipmentSlot, ServerContext.ItemStackFactory.Create(ToolItemId(), 3));

            controller.MoveItem(LocalMoveInventoryType.Equipment, EquipmentSlot, LocalMoveInventoryType.MainOrSub, MainSlot, 3, isMoveSendData: false);

            Assert.AreEqual(0, equipment.Slots[EquipmentSlot].Count);
            Assert.AreEqual(ToolItemId(), controller.LocalPlayerInventory[MainSlot].Id);
            Assert.AreEqual(3, controller.LocalPlayerInventory[MainSlot].Count);
        }

        [Test]
        public void 装備からgrabへの分割はgrabへ入り装備に残りが残る()
        {
            var (controller, equipment) = CreateController();
            equipment.ApplySlotUpdate(EquipmentSlot, ServerContext.ItemStackFactory.Create(ToolItemId(), 5));

            // inventory.split が装備枠を起点にしたときのローカル反映（半分をgrabへ）
            // The local effect of inventory.split originating from an equipment slot (half goes to grab)
            controller.MoveItem(LocalMoveInventoryType.Equipment, EquipmentSlot, LocalMoveInventoryType.Grab, 0, 2, isMoveSendData: false);

            Assert.AreEqual(ToolItemId(), controller.GrabInventory.Id);
            Assert.AreEqual(2, controller.GrabInventory.Count);
            Assert.AreEqual(3, equipment.Slots[EquipmentSlot].Count);
        }

        private (LocalPlayerInventoryController controller, LocalPlayerEquipment equipment) CreateController()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 装備枠が2枠未満のマスタではこのテストの前提が崩れる
            // The test premise breaks on a master with fewer than two equipment slots
            Assert.Less(EquipmentSlot, MasterHolder.ToolMaster.EquipmentSlotCount);

            var equipment = new LocalPlayerEquipment();
            return (new LocalPlayerInventoryController(new LocalPlayerInventory(), equipment), equipment);
        }

        private ItemId ToolItemId()
        {
            return MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
        }
    }
}
