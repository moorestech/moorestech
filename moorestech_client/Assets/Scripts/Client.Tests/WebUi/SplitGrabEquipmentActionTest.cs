using System;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.WebUiHost.Game.Actions;
using Core.Master;
using Game.Context;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// 装備枠起点と境界を検証
    /// Tests equipment origin and bounds
    /// </summary>
    public class SplitGrabEquipmentActionTest
    {
        private const int EquipmentSlot = 1;
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 装備枠の空スロットはinvalid_slotではなくempty_slotになる()
        {
            var (handler, _, _) = CreateHandler();

            // invalid_slot が返るなら装備枠がパース段階で弾かれている（起点として受理されていない）
            // An invalid_slot result would mean the equipment slot is rejected at parse time and never becomes an origin
            var result = Execute(handler, EquipmentSlot);

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("empty_slot", result.Error);
        }

        [Test]
        public void 装備枠の1個スタックは半分が0なので何も動かさず成功する()
        {
            var (handler, controller, equipment) = CreateHandler();
            equipment.ApplySlotUpdate(EquipmentSlot, ServerContext.ItemStackFactory.Create(ToolItemId(), 1));

            var result = Execute(handler, EquipmentSlot);

            Assert.IsTrue(result.Ok);
            Assert.AreEqual(ItemMaster.EmptyItemId, controller.GrabInventory.Id);
            Assert.AreEqual(1, equipment.Slots[EquipmentSlot].Count);
        }

        [Test]
        public void 装備枠数を超えるスロットは拒否される()
        {
            var (handler, _, equipment) = CreateHandler();

            // メインの枠数で境界を切っていると装備枠外のスロットが通ってしまう
            // Bounding by the main slot count instead would let a slot beyond the equipment range through
            var result = Execute(handler, equipment.Slots.Count);

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("invalid_slot", result.Error);
        }

        private ActionResult Execute(SplitGrabActionHandler handler, int equipmentSlot)
        {
            var payload = new JObject { ["from"] = new JObject { ["area"] = "equipment", ["slot"] = equipmentSlot } };
            return handler.ExecuteAsync(payload).GetAwaiter().GetResult();
        }

        private (SplitGrabActionHandler handler, LocalPlayerInventoryController controller, LocalPlayerEquipment equipment) CreateHandler()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 装備枠が2枠未満のマスタではこのテストの前提が崩れる
            // The test premise breaks on a master with fewer than two equipment slots
            Assert.Less(EquipmentSlot, MasterHolder.ItemMaster.Items.EquipmentSlotCount);

            var equipment = new LocalPlayerEquipment();
            var controller = new LocalPlayerInventoryController(new LocalPlayerInventory(), equipment);
            return (new SplitGrabActionHandler(controller), controller, equipment);
        }

        private ItemId ToolItemId()
        {
            return MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
        }
    }
}
