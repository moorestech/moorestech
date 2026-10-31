using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Chest;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.Usability
{
    public class CleanRoomAirFilterInsertionTest
    {
        [Test]
        public void BeltDeliversFiltersIntoAirFilterTest()
        {
            CleanRoomHatchTest.CreateServer();

            // 北向きにチェスト→ベルト→清浄機
            // Chest, belt and purifier in a northbound line
            var source = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1)).GetComponent<VanillaChestComponent>();
            CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0));
            var filter = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 1));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestCleanRoomFilter, 2));

            // 1マスのベルトは60tick周期で1個ずつ渡すので、2個は300tickで届き切る
            // A one-cell belt hands one item every 60 ticks, so two items arrive within 300 ticks
            GameUpdater.RunFrames(300);
            var slot = filter.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0);
            Assert.AreEqual(ForUnitTestItemId.TestCleanRoomFilter, slot.Id);
            Assert.AreEqual(2, slot.Count);
        }

        [Test]
        public void NonFilterItemStaysOnBeltTest()
        {
            CleanRoomHatchTest.CreateServer();
            var source = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1)).GetComponent<VanillaChestComponent>();
            CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0));
            var filter = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 1));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));

            // 清浄機はフィルター以外を受け取らないので、品はベルトの出口で止まりスロットは空のまま
            // The purifier refuses non-filters, so the item parks at the belt exit and the slot stays empty
            GameUpdater.RunFrames(300);
            Assert.AreEqual(ItemMaster.EmptyItemId, filter.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Id);
            var parked = BeltWorldTestUtil.ItemsOnSegmentAt(Vector3Int.zero);
            Assert.AreEqual(1, parked.Length);
            Assert.AreEqual(0, parked[0].DistanceToExit);
        }

        [Test]
        public void DirectInsertRejectsNonFilterTest()
        {
            CleanRoomHatchTest.CreateServer();
            var inventory = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0)).GetComponent<IOpenableBlockInventoryComponent>();
            var factory = ServerContext.ItemStackFactory;
            var nonFilter = factory.Create(ForUnitTestItemId.ItemId1, 3);
            var filterStack = factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 3);

            // 手動移動の判定（IsAllowedToPlace）とベルト前の判定（InsertionCheck）がどちらも非フィルターを拒む
            // Both the manual-move check (IsAllowedToPlace) and the pre-belt check (InsertionCheck) refuse non-filters
            Assert.IsFalse(inventory.IsAllowedToPlace(0, nonFilter));
            Assert.IsTrue(inventory.IsAllowedToPlace(0, filterStack));
            Assert.IsTrue(inventory.IsAllowedToPlace(0, factory.CreatEmpty()));
            Assert.IsFalse(((IBlockInventory)inventory).InsertionCheck(new List<IItemStack> { nonFilter }));
            Assert.IsTrue(((IBlockInventory)inventory).InsertionCheck(new List<IItemStack> { filterStack }));

            // 挿入APIは非フィルターを全量そのまま差し戻す
            // Every insert overload hands non-filters back untouched
            var bounced = inventory.InsertItem(nonFilter, InsertItemContext.Empty);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, bounced.Id);
            Assert.AreEqual(3, bounced.Count);
            Assert.AreEqual(2, inventory.InsertItem(ForUnitTestItemId.ItemId1, 2).Count);
            var bouncedList = inventory.InsertItem(new List<IItemStack> { factory.Create(ForUnitTestItemId.ItemId1, 1) });
            Assert.AreEqual(1, bouncedList.Count);
            Assert.AreEqual(ForUnitTestItemId.ItemId1, bouncedList[0].Id);
            Assert.AreEqual(ItemMaster.EmptyItemId, inventory.GetItem(0).Id);

            // フィルターは受け取る
            // Filters are accepted
            var accepted = inventory.InsertItem(filterStack, InsertItemContext.Empty);
            Assert.AreEqual(0, accepted.Count);
            Assert.AreEqual(3, inventory.GetItem(0).Count);
        }
    }
}
