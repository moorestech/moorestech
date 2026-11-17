using System;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Inventory.Drag
{
    /// <summary>
    /// 装備の枠を起点にした集めと分割（空・1 個・枠数の境界）を確かめる（L0）
    /// Verifies collect and split starting from an equipment slot (empty, one item, slot-count bound) (L0)
    /// </summary>
    public class PlayerItemActionEquipmentTest
    {
        private const int EquipmentSlot = 1;
        private const int MainSlot = 1;
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 空手で装備の枠をクリックした集め先はその装備の枠()
        {
            var operations = new RecordingPlayerInventoryOperations(9, 2);

            var result = new PlayerItemActionExecutor(operations).Execute(new PlayerItemAction[] { new CollectAction(PlayerSlotRef.Equipment(EquipmentSlot)) });

            Assert.IsInstanceOf<UiCommandAccepted>(result);
            CollectionAssert.AreEqual(new[] { PlayerSlotRef.Equipment(EquipmentSlot).ToString() }, operations.Collects);
        }

        [Test]
        public void 装備の枠の分割は空なら拒否し1個なら何も動かさず枠数の番地は警告して受理する()
        {
            var (executor, controller, equipment) = CreateExecutor();
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(ToolItemId(), 4));

            // 空の装備の枠は「空の枠」で拒否する
            // An empty equipment slot is rejected as an empty slot
            var empty = executor.Execute(new PlayerItemAction[] { new SplitToGrabAction(PlayerSlotRef.Equipment(EquipmentSlot)) });
            Assert.AreEqual(UiCommandRejectReason.ItemSourceSlotEmpty, ((UiCommandRejected)empty).Reason);

            // 1 個は半分が 0 なので何も動かさず受理する
            // One item halves to zero, so nothing moves and it is accepted
            equipment.ApplySlotUpdate(EquipmentSlot, ServerContext.ItemStackFactory.Create(ToolItemId(), 1));
            Assert.IsInstanceOf<UiCommandAccepted>(executor.Execute(new PlayerItemAction[] { new SplitToGrabAction(PlayerSlotRef.Equipment(EquipmentSlot)) }));
            Assert.AreEqual(ItemMaster.EmptyItemId, controller.GrabInventory.Id);
            Assert.AreEqual(1, equipment.Slots[EquipmentSlot].Count);

            // 装備の枠数の番地はメインの枠数で切らず、古い番地として警告して受理する
            // The slot-count index is bounded by equipment, not main, and is accepted with a stale-slot warning
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"split on stale slot"));
            Assert.IsInstanceOf<UiCommandAccepted>(executor.Execute(new PlayerItemAction[] { new SplitToGrabAction(PlayerSlotRef.Equipment(equipment.Slots.Count)) }));
            Assert.AreEqual(4, controller.LocalPlayerInventory[MainSlot].Count);
        }

        private static (PlayerItemActionExecutor executor, LocalPlayerInventoryController controller, LocalPlayerEquipment equipment) CreateExecutor()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.Less(EquipmentSlot, MasterHolder.ItemMaster.Items.EquipmentSlotCount, "テスト前提: 装備の枠が 2 つ以上");
            var equipment = new LocalPlayerEquipment();
            var controller = new LocalPlayerInventoryController(new LocalPlayerInventory(), equipment);
            return (new PlayerItemActionExecutor(new LocalPlayerInventoryOperations(controller, equipment)), controller, equipment);
        }

        private static ItemId ToolItemId()
        {
            return MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
        }
    }
}
