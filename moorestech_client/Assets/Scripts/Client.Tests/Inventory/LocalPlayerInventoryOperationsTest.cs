using System;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Ui.State.Inventory.Actions;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Inventory
{
    /// <summary>
    /// 本番の持ち物の口がコントローラの拒否コードを移動の結果の列挙へ写すことを確かめる
    /// Verifies the production inventory port maps the controller's deny codes to the move outcome enum
    /// </summary>
    public class LocalPlayerInventoryOperationsTest
    {
        private const int MainSlot = 5;
        private static readonly Guid ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 空の枠から掴むとSourceEmptyになる()
        {
            var operations = Create(out _);

            Assert.AreEqual(PlayerItemMoveOutcome.SourceEmpty, operations.MoveToGrab(PlayerSlotRef.Main(MainSlot), 1));
        }

        [Test]
        public void 枠の個数より多く掴むとSourceCountShortで枠は変わらない()
        {
            var operations = Create(out var controller);
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(ItemGuid), 10));

            Assert.AreEqual(PlayerItemMoveOutcome.SourceCountShort, operations.MoveToGrab(PlayerSlotRef.Main(MainSlot), 11));
            Assert.AreEqual(10, controller.LocalPlayerInventory[MainSlot].Count);
        }

        private static LocalPlayerInventoryOperations Create(out LocalPlayerInventoryController controller)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var equipment = new LocalPlayerEquipment();
            controller = new LocalPlayerInventoryController(new LocalPlayerInventory(), equipment);
            return new LocalPlayerInventoryOperations(controller, equipment);
        }
    }
}
