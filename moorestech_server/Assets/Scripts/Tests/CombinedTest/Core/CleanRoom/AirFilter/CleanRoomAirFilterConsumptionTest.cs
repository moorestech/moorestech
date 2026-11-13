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

namespace Tests.CombinedTest.Core.CleanRoom.AirFilter
{
    public class CleanRoomAirFilterConsumptionTest
    {
        // 寿命12秒（毎分5個）
        // The test mod's filterLifeSeconds is 12 seconds (five per minute)
        private const double FilterLifeSeconds = 12;

        private const ulong HalfPowerSeed = 20261113UL;

        [Test]
        public void FullPowerConsumesFiveFiltersPerMinuteTest()
        {
            var block = BuildServingPurifier(10);

            // 部屋の中で満電60秒ならちょうど5個減る
            // Sixty seconds at full power inside a room consume exactly five filters
            CleanRoomAirFilterTest.TickWithPower(block, 100f, (int)GameUpdater.SecondsToTicks(60));
            Assert.AreEqual(5, FilterCount(block));
        }

        [Test]
        public void HalfPowerHalvesConsumptionTest()
        {
            const int loadedFilterCount = 30;
            var block = BuildServingPurifier(loadedFilterCount);

            // 寿命は採掘機と同じ確率丸めで進むので、乱数の種を固定して結果を決定的にする
            // The life advances by the miner's probabilistic rounding, so fix the random seed to keep the result deterministic
            GameRandom.Reseed(HalfPowerSeed);
            CleanRoomAirFilterTest.TickWithPower(block, 50f, (int)GameUpdater.SecondsToTicks(600));

            // 使った寿命をフィルター個数で数える（消費済み＋装填中の経過分）
            // Count the used life in filters: consumed ones plus the loaded one's elapsed part
            var usedFilters = loadedFilterCount - FilterCount(block) + (1d - RemainingRatio(block));

            // 電力50%の600秒は期待値25個。経過tickは二項分布B(12000,0.5)で標準偏差√3000≈54.8tick、±4σ≈219tick＝0.913個を許容幅にする
            // 600 s at 50% power expect 25 filters; elapsed ticks follow B(12000,0.5) with sd sqrt(3000)~=54.8 ticks, so allow +/-4 sd ~= 219 ticks = 0.913 filters
            Assert.AreEqual(25d, usedFilters, 0.92);
        }

        [Test]
        public void UnpoweredFilterDoesNotWearTest()
        {
            var block = BuildServingPurifier(10);

            // 部屋の中でも無通電では寿命が進まない
            // Even inside a room, sixty seconds without power change neither the filter count nor the remaining life
            for (var i = 0; i < GameUpdater.SecondsToTicks(60); i++) GameUpdater.UpdateOneTick();
            Assert.AreEqual(10, FilterCount(block));
            Assert.AreEqual(1d, RemainingRatio(block), 1e-9);
        }

        [Test]
        public void EmptySlotDoesNotWearTest()
        {
            var block = BuildServingPurifier(0);

            // 空スロットのまま満電5秒回しても、後から入れた1個は未使用のまま
            // Five powered seconds with an empty slot leave a filter inserted afterwards unused
            CleanRoomAirFilterTest.TickWithPower(block, 100f, (int)GameUpdater.SecondsToTicks(5));
            CleanRoomAirFilterTest.LoadFilterItems(block, 1);
            Assert.AreEqual(1d, RemainingRatio(block), 1e-9);
        }

        [Test]
        public void RemainingRatioFollowsElapsedLifeTest()
        {
            var block = BuildServingPurifier(3);

            // 満電5秒で残り寿命は 1 - 5/12
            // Five seconds at full power leave 1 - 5/12 of the life
            CleanRoomAirFilterTest.TickWithPower(block, 100f, (int)GameUpdater.SecondsToTicks(5));
            Assert.AreEqual(3, FilterCount(block));
            Assert.AreEqual(1d - 5d / FilterLifeSeconds, RemainingRatio(block), 1e-9);
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
            var block = BuildServingPurifier(1);

            // 最後の1個が尽きる
            // One life consumes the last filter and drops the removal volume to zero
            CleanRoomAirFilterTest.TickWithPower(block, 100f, (int)GameUpdater.SecondsToTicks(FilterLifeSeconds));
            Assert.AreEqual(0, FilterCount(block));
            Assert.AreEqual(0d, block.GetComponent<ICleanRoomAirFilter>().RemovalVolumePerSecond, 1e-9);
        }

        #region TestHelper

        // 基準部屋に清浄機を置き、検出tickを1回回して在室にする。このtickは無通電なので寿命は進まない
        // Place the purifier in the base room and run one detection tick so it serves the room; unpowered, so no wear
        internal static IBlock BuildServingPurifier(int filterCount)
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var block = CleanRoomAirFilterTest.BuildBaseRoom();
            if (0 < filterCount) CleanRoomAirFilterTest.LoadFilterItems(block, filterCount);

            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out _));
            return block;
        }

        internal static double RemainingRatio(IBlock block)
        {
            return block.GetComponent<ICleanRoomAirFilter>().GetStatus().CurrentFilterRemainingRatio;
        }

        internal static int FilterCount(IBlock block)
        {
            // GetItemの多重定義曖昧さを避けるためIOpenableInventoryとして扱う
            // View as IOpenableInventory to avoid the GetItem overload ambiguity
            IOpenableInventory inventory = block.GetComponent<IOpenableBlockInventoryComponent>();
            return inventory.GetItem(0).Count;
        }

        #endregion
    }
}
