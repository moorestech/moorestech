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
    /// 検証つき移動の拒否理由が列挙になっても、inventory.move_item の Web の失敗コードの綴りは変わらない
    /// The Web error codes of inventory.move_item keep their spelling after the validated move's deny reason became an enum
    /// </summary>
    public class MoveItemDenyWireCodeTest
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

        [TestCase(LocalItemMoveDenyReason.SourceEmpty, "empty_slot")]
        [TestCase(LocalItemMoveDenyReason.SourceCountShort, "insufficient_count")]
        public void 拒否理由の全値がWebの失敗コードへ写る(LocalItemMoveDenyReason reason, string wireCode)
        {
            Assert.AreEqual(wireCode, LocalItemMoveDenyWireCode.From(reason));
            Assert.AreEqual(Enum.GetValues(typeof(LocalItemMoveDenyReason)).Length, 2, "値を足したらこの表にも足す");
        }

        private static ActionResult Execute(MoveItemActionHandler handler, int count)
        {
            var payload = new JObject
            {
                ["from"] = new JObject { ["area"] = "main", ["slot"] = MainSlot },
                ["to"] = new JObject { ["area"] = "grab", ["slot"] = 0 },
                ["count"] = count,
            };
            return handler.ExecuteAsync(payload).GetAwaiter().GetResult();
        }

        private static (MoveItemActionHandler handler, LocalPlayerInventoryController controller) CreateHandler()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var controller = new LocalPlayerInventoryController(new LocalPlayerInventory(), new LocalPlayerEquipment());
            return (new MoveItemActionHandler(controller), controller);
        }
    }
}
