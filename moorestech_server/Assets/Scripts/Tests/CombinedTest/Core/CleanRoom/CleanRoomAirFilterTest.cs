using Tests.Util.EnergySystem;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Blocks.CleanRoom;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using Newtonsoft.Json;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomAirFilterTest
    {
        [Test]
        public void PoweredFilterReachesRowATest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var block = BuildBaseRoom();
            LoadFilterItems(block, 5);

            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room));

            // 清浄機の占有セルがVから抜け（75→74）、床接地面ぶんSも1減る（110→109）
            // The purifier's cell leaves V (75 -> 74) and its floor-touching face leaves S (110 -> 109)
            Assert.AreEqual(74, room.Volume);
            Assert.AreEqual(109, room.SurfaceArea);

            // ACH = 5/74 ≈ 0.068 は行Aの昇格条件 0.017×1.25 を満たすので、通電したtickで行Aになる
            // ACH = 5/74 ~= 0.068 meets row A's promotion bar 0.017x1.25, so the room reaches row A once powered
            TickWithPower(block, 100f, 2);
            Assert.AreEqual(0, room.ThresholdIndex);
        }

        [Test]
        public void HalfPowerHalvesRemovalVolumeTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var block = BuildBaseRoom();
            LoadFilterItems(block, 5);

            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out _));

            // 実効q=5×0.5=2.5
            // Supplying 50 of 100 yields an effective q=5x0.5=2.5
            TickWithPower(block, 50f, 20);
            Assert.AreEqual(2.5, block.GetComponent<ICleanRoomAirFilter>().RemovalVolumePerSecond, 0.001);
        }

        [Test]
        public void NoFilterItemStopsRemovalTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var block = BuildBaseRoom();

            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room));

            // 未装填は除去0でOut
            // Without a loaded filter, removal is zero even at full power and the row stays Out
            TickWithPower(block, 100f, 100);
            Assert.AreEqual(0, block.GetComponent<ICleanRoomAirFilter>().RemovalVolumePerSecond, 0.001);
            Assert.AreEqual(MasterHolder.CleanRoomMaster.OutThresholdIndex, room.ThresholdIndex);
        }

        [Test]
        public void SaveLoadRestoresWearAndFilterSlotTest()
        {
            CleanRoomHatchTest.CreateServer();
            var block = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0));
            LoadFilterItems(block, 1);

            // 満電5秒で経過を溜める
            // Run five seconds at full power so the loaded filter accrues five seconds of use
            TickWithPower(block, 100f, (int)GameUpdater.SecondsToTicks(5));

            var saveComponent = block.GetComponent<IBlockSaveState>();
            var states = SaveLoadJsonTestHelper.ThroughJson(saveComponent.SaveKey, saveComponent.GetSaveState());

            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.CleanRoomAirFilterId).BlockGuid;
            var positionInfo = new BlockPositionInfo(new Vector3Int(10, 0, 10), BlockDirection.North, Vector3Int.one);
            var loaded = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(int.MaxValue), states, positionInfo);

            // スロットと経過が引き継がれる
            // Both the slot contents and the five elapsed seconds carry over to the new instance
            IOpenableInventory loadedInventory = loaded.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.AreEqual(1, loadedInventory.GetItem(0).Count);
            Assert.AreEqual(ForUnitTestItemId.TestCleanRoomFilter, loadedInventory.GetItem(0).Id);

            var resaved = (CleanRoomAirFilterSaveJsonObject)loaded.GetComponent<IBlockSaveState>().GetSaveState();
            Assert.AreEqual(5d, resaved.FilterWearSeconds, 0.001);
        }

        [Test]
        public void LargeRoomLacksAirChangeForRowATest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();

            // 内寸10×10×5（V=500-1=499）ではACH=5/499≈0.010<0.017で行Aに届かない
            // A 10x10x5 interior (V=500-1=499) yields ACH=5/499~=0.010<0.017, missing row A
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(11, 6, 11));
            var block = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(5, 1, 5));
            LoadFilterItems(block, 5);

            // 大部屋は差分再検出のシード処理が複数tickに跨るため数tick回す
            // Large rooms need a few ticks for the incremental detection to finish its seeds
            for (var i = 0; i < 10; i++) GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room));
            Assert.AreEqual(499, room.Volume);

            // 満電・フィルター装填でも換気不足のため行Aには一度も到達しない
            // Even fully powered and loaded, the room never reaches row A due to low ACH
            TickWithPower(block, 100f, 600);
            Assert.AreEqual(5.0, block.GetComponent<ICleanRoomAirFilter>().RemovalVolumePerSecond, 0.001);
            Assert.AreNotEqual(0, room.ThresholdIndex);
        }

        #region TestHelper

        // 内寸5×5×3の部屋（壁+ハッチ1+ドア1）に清浄機を1台設置して返す
        // Build a 5x5x3-interior room (walls + one hatch + one door) with one purifier inside
        internal static IBlock BuildBaseRoom()
        {
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(6, 4, 6));
            CleanRoomHatchTest.ReplaceBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(3, 1, 0));
            CleanRoomHatchTest.ReplaceBlock(ForUnitTestModBlockId.CleanRoomDoorId, new Vector3Int(3, 2, 0));
            return CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(3, 1, 3));
        }

        internal static void LoadFilterItems(IBlock block, int count)
        {
            block.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestCleanRoomFilter, count);
        }

        // 部屋の外に置く給電用電柱の位置
        // Position of the powering pole placed outside every room
        internal static readonly Vector3Int PowerPolePosition = new(30, 0, 30);

        internal static void TickWithPower(IBlock block, float power, int ticks)
        {
            // 電線経由の実経路で給電する。発電機が既に居るセグメントには追加しない
            // Power through the real wire path; skip registration when the segment already has a generator
            var datastore = ServerContext.GetService<IElectricWireNetworkLookup>();
            if (!datastore.TryGetEnergySegment(block.BlockInstanceId, out var segment) || ElectricNetworkReflectionTestUtil.GetGenerators(segment).Count == 0)
                ElectricWireTestUtil.WirePower(block.BlockPositionInfo.OriginalPos, PowerPolePosition, power);

            for (var i = 0; i < ticks; i++) GameUpdater.UpdateOneTick();
        }

        #endregion
    }
}
