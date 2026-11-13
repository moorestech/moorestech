using System;
using Core.Inventory;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.EnergySystem;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomAirFilterConsumptionTest
    {
        // 寿命12秒（毎分5個）
        // The test mod's filterLifeSeconds is 12 seconds (five per minute)
        private const double FilterLifeSeconds = 12;

        [Test]
        public void FullPowerConsumesFiveFiltersPerMinuteTest()
        {
            CleanRoomHatchTest.CreateServer();
            var block = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0));
            CleanRoomAirFilterTest.LoadFilterItems(block, 10);

            // 満電60秒でちょうど5個減る
            // Even outside any room, sixty seconds at full power consume exactly five filters
            CleanRoomAirFilterTest.TickWithPower(block, 100f, (int)GameUpdater.SecondsToTicks(60));
            Assert.AreEqual(5, FilterCount(block));
        }

        [Test]
        public void UnpoweredFilterDoesNotWearTest()
        {
            CleanRoomHatchTest.CreateServer();
            var block = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0));
            CleanRoomAirFilterTest.LoadFilterItems(block, 10);

            // 無通電では寿命が進まない
            // Sixty seconds without power change neither the filter count nor the remaining life
            for (var i = 0; i < GameUpdater.SecondsToTicks(60); i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(10, FilterCount(block));
            Assert.AreEqual(1d, block.GetComponent<ICleanRoomAirFilter>().GetStatus().CurrentFilterRemainingRatio, 1e-9);
        }

        [Test]
        public void TwoPurifiersInOneRoomEachConsumeAtFullRateTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var first = CleanRoomAirFilterTest.BuildBaseRoom();
            var second = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(2, 1, 3));
            CleanRoomAirFilterTest.LoadFilterItems(first, 10);
            CleanRoomAirFilterTest.LoadFilterItems(second, 10);

            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out _));

            // 2台を同じ給電セグメントへ入れ、両方を満電にする
            // Put both purifiers in one powered segment so each runs at full power
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, CleanRoomAirFilterTest.PowerPolePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(second.BlockPositionInfo.OriginalPos, CleanRoomAirFilterTest.PowerPolePosition);
            ElectricWireTestUtil.WirePower(first.BlockPositionInfo.OriginalPos, CleanRoomAirFilterTest.PowerPolePosition, 200f);

            // 台数に依らず各5個減る
            // Consumption is not diluted by the count; each loses five (ten in total) in sixty seconds
            for (var i = 0; i < GameUpdater.SecondsToTicks(60); i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(5, FilterCount(first));
            Assert.AreEqual(5, FilterCount(second));
        }

        [Test]
        public void LastFilterRunsOutAfterOneLifeTest()
        {
            CleanRoomHatchTest.CreateServer();
            var block = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0));
            CleanRoomAirFilterTest.LoadFilterItems(block, 1);

            // 最後の1個が尽きる
            // One life consumes the last filter and drops the removal volume to zero
            CleanRoomAirFilterTest.TickWithPower(block, 100f, (int)GameUpdater.SecondsToTicks(FilterLifeSeconds));
            Assert.AreEqual(0, FilterCount(block));
            Assert.AreEqual(0d, block.GetComponent<ICleanRoomAirFilter>().RemovalVolumePerSecond, 1e-9);
        }

        #region TestHelper

        private static int FilterCount(IBlock block)
        {
            // GetItemの多重定義曖昧さを避けるためIOpenableInventoryとして扱う
            // View as IOpenableInventory to avoid the GetItem overload ambiguity
            IOpenableInventory inventory = block.GetComponent<IOpenableBlockInventoryComponent>();
            return inventory.GetItem(0).Count;
        }

        #endregion
    }
}
