using Core.Master;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomAirFilterTest
    {
        [Test]
        public void ItemComponent_CountsAndConsumesOnlyFilterItems()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var filterItemId = MasterHolder.ItemMaster.GetItemId(ForUnitTestModBlockId.CleanRoomFilterItemGuid);

            // スロット2: フィルター以外のアイテムはカウントも消費もされない。
            // 2 slots: non-filter items are neither counted nor consumed.
            var inventory = new CleanRoomAirFilterItemComponent(slotCount: 2, filterItemId, new BlockInstanceId(1));
            inventory.SetItem(0, itemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 2));
            inventory.SetItem(1, itemStackFactory.Create(new System.Guid("00000000-0000-0000-1234-000000000001"), 5)); // Test1（非フィルター）

            Assert.AreEqual(2, inventory.FilterCount, "filter items only");
            Assert.IsTrue(inventory.HasFilter);

            // 消費はフィルタースロットだけ減る。
            // Consumption only decrements the filter slot.
            Assert.IsTrue(inventory.TryConsumeOneFilter());
            Assert.IsTrue(inventory.TryConsumeOneFilter());
            Assert.IsFalse(inventory.TryConsumeOneFilter(), "no filters left");
            Assert.AreEqual(0, inventory.FilterCount);
            Assert.IsFalse(inventory.HasFilter);
            Assert.AreEqual(5, inventory.GetItem(1).Count, "non-filter item untouched");
        }
    }
}
