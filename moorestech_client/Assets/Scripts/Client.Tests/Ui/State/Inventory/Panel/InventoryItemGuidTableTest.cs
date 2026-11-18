using System;
using System.Collections.Generic;
using Client.Ui.State.Inventory.Panel;
using Client.Ui.State.Items;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Inventory.Panel
{
    public class InventoryItemGuidTableTest
    {
        private static readonly Guid WoodGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private ItemId _wood;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _wood = MasterHolder.ItemMaster.GetItemId(WoodGuid);
        }

        [Test]
        public void 同じアイテムの先頭スロットだけが小文字のguidを持つ()
        {
            var empty = new SlotItem(ItemMaster.EmptyItemId, 0);
            var slots = new List<SlotItem> { empty, new(_wood, 0), new(_wood, 3), new(_wood, 5) };

            var table = InventoryItemGuidTable.Build(slots);

            // 空・個数 0 は所持に数えず、実所持の先頭（2 番）だけが持つ
            // Empty and zero-count slots are not owned; only the first owned slot (index 2) holds it
            Assert.AreEqual(1, table.Count);
            Assert.AreEqual(WoodGuid.ToString(), table[2]);
            StringAssert.IsMatch("^[0-9a-f-]+$", table[2]);
        }
    }
}
