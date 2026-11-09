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

namespace Client.Tests.WebUi.WireContracts
{
    /// <summary>
    /// コントローラの移動の結果が列挙になっても、inventory.move_item の Web の結果（失敗コードの綴り・何もしない移動の成功）は変わらない
    /// The Web results of inventory.move_item (error code spelling, success for no-effect moves) stay the same after the controller's move result became an enum
    /// </summary>
    public class MoveItemWireResultTest
    {
        private const int MainSlot = 5;
        private static readonly Guid ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        [Test]
        public void 空の枠からの移動はempty_slotになる()
        {
            var (handler, _) = CreateHandler();

            var result = Execute(handler, 1);

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("empty_slot", result.Error);
        }

        [Test]
        public void 枠の個数より多い移動はinsufficient_countになる()
        {
            var (handler, controller) = CreateHandler();
            controller.SetMainItem(MainSlot, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(ItemGuid), 3));

            var result = Execute(handler, 4);

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("insufficient_count", result.Error);
        }

        [Test]
        public void 同じ枠への移動は空の枠でも成功になる()
        {
            var (handler, _) = CreateHandler();

            var result = handler.ExecuteAsync(Payload("main", MainSlot, 1)).GetAwaiter().GetResult();

            Assert.IsTrue(result.Ok);
        }

        [TestCase(LocalItemMoveResult.Moved, true, null)]
        [TestCase(LocalItemMoveResult.NoEffect, true, null)]
        [TestCase(LocalItemMoveResult.SourceEmpty, false, "empty_slot")]
        [TestCase(LocalItemMoveResult.SourceCountShort, false, "insufficient_count")]
        public void 移動の結果の全値がWebの結果へ写る(LocalItemMoveResult moveResult, bool ok, string wireCode)
        {
            var result = LocalItemMoveWireResult.ToActionResult(moveResult);

            Assert.AreEqual(ok, result.Ok);
            Assert.AreEqual(wireCode, result.Error);
            Assert.AreEqual(4, Enum.GetValues(typeof(LocalItemMoveResult)).Length, "値を足したらこの表にも足す");
        }

        private static ActionResult Execute(MoveItemActionHandler handler, int count)
        {
            return handler.ExecuteAsync(Payload("grab", 0, count)).GetAwaiter().GetResult();
        }

        private static JObject Payload(string toArea, int toSlot, int count)
        {
            return new JObject
            {
                ["from"] = new JObject { ["area"] = "main", ["slot"] = MainSlot },
                ["to"] = new JObject { ["area"] = toArea, ["slot"] = toSlot },
                ["count"] = count,
            };
        }

        private static (MoveItemActionHandler handler, LocalPlayerInventoryController controller) CreateHandler()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var controller = new LocalPlayerInventoryController(new LocalPlayerInventory(), new LocalPlayerEquipment());
            return (new MoveItemActionHandler(controller), controller);
        }
    }
}
