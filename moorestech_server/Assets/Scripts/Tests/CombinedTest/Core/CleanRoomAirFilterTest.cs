using Core.Master;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomAirFilterTest
    {
        private const double FilterCapacity = 5000;

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

        [Test]
        public void Component_RemovalScalesWithPowerRatioAndFilterPresence()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var filterItemId = MasterHolder.ItemMaster.GetItemId(ForUnitTestModBlockId.CleanRoomFilterItemGuid);

            var inventory = new CleanRoomAirFilterItemComponent(slotCount: 1, filterItemId, new BlockInstanceId(1));
            inventory.InsertItem(itemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 1));

            // q=5, requiredPower=100, filterCapacity=5000。
            // q=5, requiredPower=100, filterCapacity=5000.
            var component = new CleanRoomAirFilterComponent(new BlockInstanceId(1), removalVolumePerSecond: 5.0, requiredPower: 100f, filterCapacity: FilterCapacity, inventory);

            // 給電前は0。
            // No power yet => 0.
            Assert.AreEqual(0.0, component.RemovalVolumePerSecond, 1e-9);

            // 満電で q=5.0、半電で 2.5、過給電は1にクランプ。
            // Full power => 5.0, half => 2.5, over-supply clamps to 1.
            component.SupplyEnergy(new ElectricPower(100f));
            Assert.AreEqual(5.0, component.RemovalVolumePerSecond, 1e-9);
            component.SupplyEnergy(new ElectricPower(50f));
            Assert.AreEqual(2.5, component.RemovalVolumePerSecond, 1e-9);
            component.SupplyEnergy(new ElectricPower(1000f));
            Assert.AreEqual(5.0, component.RemovalVolumePerSecond, 1e-9);

            // 給電が来ないまま2回目のUpdateで電力decay→0（常時消費のため毎tick電力を使う）。
            // Second Update without fresh supply decays power to 0 (always-on consumer).
            component.SupplyEnergy(new ElectricPower(100f));
            component.Update();
            Assert.AreEqual(5.0, component.RemovalVolumePerSecond, 1e-9);
            component.Update();
            Assert.AreEqual(0.0, component.RemovalVolumePerSecond, 1e-9);
        }

        [Test]
        public void Component_NoFilter_RemovalIsZero()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var filterItemId = MasterHolder.ItemMaster.GetItemId(ForUnitTestModBlockId.CleanRoomFilterItemGuid);

            // フィルター無し → 満電でも除去0。
            // No filter loaded => removal is 0 even at full power.
            var inventory = new CleanRoomAirFilterItemComponent(slotCount: 1, filterItemId, new BlockInstanceId(1));
            var component = new CleanRoomAirFilterComponent(new BlockInstanceId(1), 5.0, 100f, FilterCapacity, inventory);
            component.SupplyEnergy(new ElectricPower(100f));
            Assert.AreEqual(0.0, component.RemovalVolumePerSecond, 1e-9);
        }

        [Test]
        public void Component_WearCrossingCapacityConsumesFilter_AndDepletionStopsRemoval()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var filterItemId = MasterHolder.ItemMaster.GetItemId(ForUnitTestModBlockId.CleanRoomFilterItemGuid);

            var inventory = new CleanRoomAirFilterItemComponent(slotCount: 1, filterItemId, new BlockInstanceId(1));
            inventory.InsertItem(itemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 2));
            var component = new CleanRoomAirFilterComponent(new BlockInstanceId(1), 5.0, 100f, FilterCapacity, inventory);
            component.SupplyEnergy(new ElectricPower(100f));

            // capacity 未満では消費しない。
            // No consumption below one capacity of wear.
            component.ApplyRemovedImpurity(FilterCapacity - 1);
            Assert.AreEqual(2, inventory.FilterCount);

            // capacity を跨いだら1個消費。
            // Crossing capacity consumes exactly one filter.
            component.ApplyRemovedImpurity(2);
            Assert.AreEqual(1, inventory.FilterCount);

            // 残り1個も使い切ると除去0（フィルター切れ）。
            // Wearing out the last filter stops removal.
            component.ApplyRemovedImpurity(FilterCapacity);
            Assert.AreEqual(0, inventory.FilterCount);
            Assert.AreEqual(0.0, component.RemovalVolumePerSecond, 1e-9);
        }

        [Test]
        public void Component_SaveState_RoundTripsWearProgressAndSlots()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var filterItemId = MasterHolder.ItemMaster.GetItemId(ForUnitTestModBlockId.CleanRoomFilterItemGuid);

            var inventory = new CleanRoomAirFilterItemComponent(slotCount: 1, filterItemId, new BlockInstanceId(1));
            inventory.InsertItem(itemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 3));
            var component = new CleanRoomAirFilterComponent(new BlockInstanceId(1), 5.0, 100f, FilterCapacity, inventory);
            component.SupplyEnergy(new ElectricPower(100f));
            component.ApplyRemovedImpurity(1234); // 進捗を残す（消費は跨がない）

            // 2コンポーネントの保存stateを componentStates 辞書に集めてロード経路を再現。
            // Collect both components' states into a componentStates dict to mimic the load path.
            var componentStates = new System.Collections.Generic.Dictionary<string, string>
            {
                { inventory.SaveKey, inventory.GetSaveState() },
                { component.SaveKey, component.GetSaveState() },
            };

            var restoredInventory = new CleanRoomAirFilterItemComponent(componentStates, slotCount: 1, filterItemId, new BlockInstanceId(2));
            var restoredComponent = new CleanRoomAirFilterComponent(componentStates, new BlockInstanceId(2), 5.0, 100f, FilterCapacity, restoredInventory);

            Assert.AreEqual(3, restoredInventory.FilterCount);
            Assert.AreEqual(1234, restoredComponent.WearProgress, 1e-6);
        }
    }
}
