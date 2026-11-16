using System.Collections.Generic;
using Core.Item.Interface;
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
    public class CleanRoomAirFilterAutoInsertCapTest
    {
        [Test]
        public void BlockInsertStopsAtThreeTest()
        {
            CleanRoomHatchTest.CreateServer();
            var inventory = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0)).GetComponent<IOpenableBlockInventoryComponent>();
            var factory = ServerContext.ItemStackFactory;

            // 空へ5個入れると3個入り2個返る
            // Five into empty takes three, returns two
            var remainder = inventory.InsertItem(factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 5), InsertItemContext.Empty);
            Assert.AreEqual(2, remainder.Count);
            Assert.AreEqual(ForUnitTestItemId.TestCleanRoomFilter, remainder.Id);
            Assert.AreEqual(3, inventory.GetItem(0).Count);

            // 3個に達した後は全量返る
            // Once at three, everything comes back
            Assert.AreEqual(1, inventory.InsertItem(factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 1), InsertItemContext.Empty).Count);
            Assert.AreEqual(3, inventory.GetItem(0).Count);
        }

        [Test]
        public void BlockInsertionCheckUsesSameCapTest()
        {
            CleanRoomHatchTest.CreateServer();
            var inventory = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0)).GetComponent<IOpenableBlockInventoryComponent>();
            var factory = ServerContext.ItemStackFactory;
            var blockInventory = (IBlockInventory)inventory;

            // 空なら3個まで可、4個は不可
            // Empty: three fit, four do not
            Assert.IsTrue(blockInventory.InsertionCheck(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 3) }));
            Assert.IsFalse(blockInventory.InsertionCheck(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 4) }));

            // 2個入っていれば残り1個分だけ
            // With two loaded, only one more fits
            inventory.SetItem(0, ForUnitTestItemId.TestCleanRoomFilter, 2);
            Assert.IsTrue(blockInventory.InsertionCheck(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 1) }));
            Assert.IsFalse(blockInventory.InsertionCheck(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 2) }));
        }

        [Test]
        public void ManualInsertIgnoresCapTest()
        {
            CleanRoomHatchTest.CreateServer();
            var inventory = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0)).GetComponent<IOpenableBlockInventoryComponent>();
            var factory = ServerContext.ItemStackFactory;

            // プレイヤー操作の経路は3個を超えて入る
            // The player path goes past three
            Assert.AreEqual(0, inventory.InsertItem(factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 10)).Count);
            Assert.AreEqual(0, inventory.InsertItem(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 2) }).Count);
            Assert.AreEqual(12, inventory.GetItem(0).Count);
            Assert.IsTrue(((global::Core.Inventory.IOpenableInventory)inventory).InsertionCheck(new List<IItemStack> { factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 5) }));

            // 手で超過後は搬入を全量返す
            // After a manual overfill, block inflow comes back whole
            Assert.AreEqual(1, inventory.InsertItem(factory.Create(ForUnitTestItemId.TestCleanRoomFilter, 1), InsertItemContext.Empty).Count);
        }

        [Test]
        public void BeltStopsAtThreeTest()
        {
            CleanRoomHatchTest.CreateServer();

            // 北向きチェスト→ベルト→清浄機。5個流す
            // Chest, belt and purifier northbound; send five
            var source = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1)).GetComponent<VanillaChestComponent>();
            CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, 0));
            var filter = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 1));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestCleanRoomFilter, 5));

            // 1マスのベルトは60tick周期で1個渡すので、600tickあれば5個分は流れ切る
            // A one-cell belt hands one per 60 ticks, so 600 ticks covers five
            GameUpdater.RunFrames(600);
            Assert.AreEqual(3, filter.GetComponent<IOpenableBlockInventoryComponent>().GetItem(0).Count);

            // 残り2個はベルト上とチェストに留まる
            // The other two stay on the belt and in the chest
            var onBelt = BeltWorldTestUtil.ItemsOnSegmentAt(Vector3Int.zero).Length;
            Assert.AreEqual(2, onBelt + source.GetItem(0).Count);
        }
    }
}
