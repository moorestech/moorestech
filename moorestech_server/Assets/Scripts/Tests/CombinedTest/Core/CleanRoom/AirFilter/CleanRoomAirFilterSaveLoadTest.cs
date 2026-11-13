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

        // 部屋の壁のうち、外すと部屋が成立しなくなる1枚
        // One room wall whose removal breaks the room
        private static readonly Vector3Int GapWallPosition = new(1, 1, 0);

        // 寿命240tickの手前239tickで保存すると、次のtickで1個消費する境界をまたぐ
        // Saving at 239 of the 240-tick life makes the next tick cross the consumption boundary
        private const int TicksBeforeSave = 239;

        // 保存tickの末尾（判定段の後）に起きる壁の変化
        // Wall change happening at the end of the save tick, after the clean-room pass
        public enum WallChangeBeforeSave
        {
            None,
            RemoveWall,
            CloseLastWall,
        }

        // 満電は毎tick確実に1tick進むので境界をまたぎ個数も比べられる。50%は乱数状態の保存復元も通す
        // Full power always advances a tick, so the boundary is crossed and counts compare; 50% also exercises the saved random state
        // 壁の変化は、稼働中の世界の在室が部屋の再検出より1tick遅れて変わる状態を保存する
        // Wall changes save a world whose live serving state lags the room re-detection by one tick
        [TestCase(100f, WallChangeBeforeSave.None)]
        [TestCase(50f, WallChangeBeforeSave.None)]
        [TestCase(100f, WallChangeBeforeSave.RemoveWall)]
        [TestCase(100f, WallChangeBeforeSave.CloseLastWall)]
        public void LoadedWorldWearsLikeLiveWorldTest(float power, WallChangeBeforeSave wallChange)
        {
            var (_, datastore1, assembleSaveJsonText1, _) = CleanRoomSaveLoadTest.CreateBlockTestModule();
            var filter1 = CleanRoomAirFilterTest.BuildBaseRoom();
            CleanRoomAirFilterTest.LoadFilterItems(filter1, 10);

            // 検出tickで在室にしてから通電して寿命を溜め、保存直前に壁を変える
            // Let the detection tick mark it serving, power it to accrue wear, then change a wall right before saving
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore1.TryGetCleanRoomAt(RoomCell, out _));
            AccrueWearAndChangeWall(filter1, power, wallChange);
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

            // ロードした世界はロードしなかった世界と寿命・在室・個数・中身が完全一致する
            // The loaded world matches the unloaded one exactly in wear, serving state, count, and item
            var actual = SaveState(filter2);
            Assert.AreEqual(expected.FilterWearSeconds, actual.FilterWearSeconds);
            Assert.AreEqual(expected.IsServingRoom, actual.IsServingRoom);
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

        // 保存時点の寿命を239tickに揃え、壁の変化を判定段の後（tick間）に起こす
        // Bring the wear to 239 ticks at save time and apply the wall change after the pass, between ticks
        private static void AccrueWearAndChangeWall(IBlock filter, float power, WallChangeBeforeSave wallChange)
        {
            switch (wallChange)
            {
                case WallChangeBeforeSave.None:
                    CleanRoomAirFilterTest.TickWithPower(filter, power, TicksBeforeSave);
                    break;
                case WallChangeBeforeSave.RemoveWall:
                    // 在室のまま部屋が崩れる。稼働中は次のtickも在室で寿命が進む
                    // The room breaks while still serving; the live world still wears on the next tick
                    CleanRoomAirFilterTest.TickWithPower(filter, power, TicksBeforeSave);
                    ServerContext.WorldBlockDatastore.RemoveBlock(GapWallPosition, BlockRemoveReason.ManualRemove);
                    break;
                case WallChangeBeforeSave.CloseLastWall:
                    // 壁を外して在室を解いた1tickも寿命が進むので、合計で239tickにする
                    // The tick that clears serving after the wall is removed still wears, so the total stays 239
                    CleanRoomAirFilterTest.TickWithPower(filter, power, TicksBeforeSave - 1);
                    ServerContext.WorldBlockDatastore.RemoveBlock(GapWallPosition, BlockRemoveReason.ManualRemove);
                    CleanRoomAirFilterTest.TickWithPower(filter, power, 1);
                    Assert.IsFalse(filter.GetComponent<ICleanRoomAirFilter>().IsServingRoom);
                    CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, GapWallPosition);
                    break;
            }
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
