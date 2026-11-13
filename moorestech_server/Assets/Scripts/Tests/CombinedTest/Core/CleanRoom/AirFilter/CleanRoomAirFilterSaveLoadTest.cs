using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.AirFilter
{
    public class CleanRoomAirFilterSaveLoadTest
    {
        private static readonly Vector3Int RoomCell = new(1, 1, 1);

        // 寿命240tickの手前239tickで保存すると、次のtickで1個消費する境界をまたぐ
        // Saving at 239 of the 240-tick life makes the next tick cross the consumption boundary
        private const int TicksBeforeSave = 239;

        // 満電は毎tick確実に1tick進むので境界をまたぎ個数も比べられる。50%は乱数状態の保存復元も通す
        // Full power always advances a tick, so the boundary is crossed and counts compare; 50% also exercises the saved random state
        [TestCase(100f)]
        [TestCase(50f)]
        public void LoadedWorldWearsLikeLiveWorldTest(float power)
        {
            var (_, datastore1, assembleSaveJsonText1, _) = CleanRoomSaveLoadTest.CreateBlockTestModule();
            var filter1 = CleanRoomAirFilterTest.BuildBaseRoom();
            CleanRoomAirFilterTest.LoadFilterItems(filter1, 10);

            // 検出tickで在室にしてから通電して寿命を溜める
            // Let the detection tick mark it serving, then power it to accrue wear
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore1.TryGetCleanRoomAt(RoomCell, out _));
            CleanRoomAirFilterTest.TickWithPower(filter1, power, TicksBeforeSave);
            Assert.Less(CleanRoomAirFilterConsumptionTest.RemainingRatio(filter1), 1d);

            // 保存した後、ロードしない側を1tick進めた値を期待値にする
            // After saving, advance the unloaded world one tick and take its values as expected
            var json = assembleSaveJsonText1.AssembleSaveJson();
            GameUpdater.UpdateOneTick();
            var expected = SaveState(filter1);
            var expectedCount = CleanRoomAirFilterConsumptionTest.FilterCount(filter1);

            // 新しい世界へロードし、保存されないテスト発電機だけ同じ出力で繋ぎ直して1tick進める
            // Load into a fresh world, re-attach only the unsaved test generator at the same output, and advance one tick
            var (_, _, _, loader2) = CleanRoomSaveLoadTest.CreateBlockTestModule();
            loader2.Load(json);
            var filter2 = ServerContext.WorldBlockDatastore.GetBlock(filter1.BlockPositionInfo.OriginalPos);
            CleanRoomAirFilterTest.TickWithPower(filter2, power, 1);

            // ロードした世界はロードしなかった世界と寿命・個数・中身が完全一致する
            // The loaded world matches the unloaded one exactly in wear, count, and item
            var actual = SaveState(filter2);
            Assert.AreEqual(expected.FilterWearSeconds, actual.FilterWearSeconds);
            Assert.AreEqual(expectedCount, CleanRoomAirFilterConsumptionTest.FilterCount(filter2));
            IOpenableInventory loadedInventory = filter2.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.AreEqual(ForUnitTestItemId.TestCleanRoomFilter, loadedInventory.GetItem(0).Id);
        }

        [Test]
        public void SavedWearBeyondLifeIsCappedAtLifeTest()
        {
            CleanRoomHatchTest.CreateServer();

            // 寿命12秒のマスタへ50秒使用済みのセーブを読むと、寿命で頭打ちになる
            // Loading 50 used seconds against a 12-second life caps the wear at the life
            var loaded = LoadPurifierWithWear(50d);
            Assert.AreEqual(GameUpdater.TicksToSeconds(GameUpdater.SecondsToTicks(12d)), SaveState(loaded).FilterWearSeconds);
        }

        [Test]
        public void SavedWearRoundTripsExactlyTest()
        {
            CleanRoomHatchTest.CreateServer();

            // 整数tickの往復は丸め誤差なく元の秒に戻る（3tick=0.15秒は浮動小数で誤差が出やすい値）
            // Whole ticks round-trip without error (3 ticks = 0.15 s is prone to floating-point error)
            var seconds = GameUpdater.TicksToSeconds(3);
            Assert.AreEqual(seconds, SaveState(LoadPurifierWithWear(seconds)).FilterWearSeconds);
        }

        #region TestHelper

        private static CleanRoomAirFilterSaveJsonObject SaveState(IBlock block)
        {
            return (CleanRoomAirFilterSaveJsonObject)block.GetComponent<IBlockSaveState>().GetSaveState();
        }

        private static IBlock LoadPurifierWithWear(double filterWearSeconds)
        {
            var saveKey = typeof(CleanRoomAirFilterComponent).FullName;
            var saveData = new CleanRoomAirFilterSaveJsonObject { FilterWearSeconds = filterWearSeconds, Items = new List<ItemStackSaveJsonObject>() };
            var states = SaveLoadJsonTestHelper.ThroughJson(saveKey, saveData);

            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.CleanRoomAirFilterId).BlockGuid;
            var positionInfo = new BlockPositionInfo(new Vector3Int(10, 0, 10), BlockDirection.North, Vector3Int.one);
            return ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(int.MaxValue), states, positionInfo);
        }

        #endregion
    }
}
