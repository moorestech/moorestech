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
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomAirFilterTest
    {
        [Test]
        public void PoweredFilterConvergesToRowATest()
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

            // A_total = 0.1×74 + 0.05×109 + 0.5×2（ハッチ+ドア）= 13.85/秒、C_eq = 13.85/5 = 2.77
            // A_total = 0.1x74 + 0.05x109 + 0.5x2 (hatch + door) = 13.85/sec; C_eq = 13.85/5 = 2.77
            // 時定数 τ=V/q≈296tick なので2000tickで十分に収束する
            // With time constant V/q ~= 296 ticks, 2000 ticks converge well enough
            TickWithPower(block, 100f, 2000);
            Assert.AreEqual(2.77, room.ImpurityCount / room.Volume, 2.77 * 0.02);
            Assert.AreEqual(0, room.ThresholdIndex);
        }

        [Test]
        public void HalfPowerHalvesRemovalVolumeTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var block = BuildBaseRoom();
            LoadFilterItems(block, 5);

            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room));

            // 供給50/要求100で実効q=5×0.5=2.5になり、C_eq = 13.85/2.5 = 5.54 に収束する
            // Supplying 50 of 100 yields q=5x0.5=2.5, converging to C_eq = 13.85/2.5 = 5.54
            TickWithPower(block, 50f, 3500);
            Assert.AreEqual(2.5, block.GetComponent<ICleanRoomAirFilter>().RemovalVolumePerSecond, 0.001);
            Assert.AreEqual(5.54, room.ImpurityCount / room.Volume, 5.54 * 0.02);
        }

        [Test]
        public void NoFilterItemStopsRemovalTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var block = BuildBaseRoom();

            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room));

            // フィルター未装填では満電でも除去0でNが増え続け、行はOutのまま
            // Without a loaded filter, removal is zero even at full power and N keeps rising
            TickWithPower(block, 100f, 100);
            Assert.AreEqual(0, block.GetComponent<ICleanRoomAirFilter>().RemovalVolumePerSecond, 0.001);

            var beforeImpurity = room.ImpurityCount;
            TickWithPower(block, 100f, 100);
            Assert.Greater(room.ImpurityCount, beforeImpurity);
            Assert.AreEqual(MasterHolder.CleanRoomMaster.OutThresholdIndex, room.ThresholdIndex);
        }

        [Test]
        public void WearConsumesFilterItemPerCapacityTest()
        {
            CleanRoomHatchTest.CreateServer();
            var block = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0));
            LoadFilterItems(block, 2);

            var filter = block.GetComponent<ICleanRoomAirFilter>();
            // GetItemの多重定義曖昧さを避けるためIOpenableInventoryとして扱う
            // View as IOpenableInventory to avoid the GetItem overload ambiguity
            IOpenableInventory inventory = block.GetComponent<IOpenableBlockInventoryComponent>();

            // 累計3000は容量5000未満なので消費なし
            // Cumulative 3000 stays below capacity 5000, so nothing is consumed
            filter.ApplyRemovedImpurity(3000);
            Assert.AreEqual(2, inventory.GetItem(0).Count);

            // 累計5500で容量到達により1個消費し残量500に戻る
            // Reaching 5500 consumes one filter, carrying over the remaining 500
            filter.ApplyRemovedImpurity(2500);
            Assert.AreEqual(1, inventory.GetItem(0).Count);

            // 残量500+4600=5100で再度消費しスロットが空になり除去は0になる
            // 500+4600=5100 consumes again, emptying the slot and zeroing removal
            filter.ApplyRemovedImpurity(4600);
            Assert.AreEqual(0, inventory.GetItem(0).Count);
            TickWithPower(block, 100f, 2);
            Assert.AreEqual(0, filter.RemovalVolumePerSecond, 0.001);
        }

        [Test]
        public void SaveLoadRestoresWearAndFilterSlotTest()
        {
            CleanRoomHatchTest.CreateServer();
            var block = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(0, 0, 0));
            LoadFilterItems(block, 1);
            block.GetComponent<ICleanRoomAirFilter>().ApplyRemovedImpurity(4900);

            var saveComponent = block.GetComponent<IBlockSaveState>();
            var states = new Dictionary<string, string> { { saveComponent.SaveKey, saveComponent.GetSaveState() } };

            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.CleanRoomAirFilterId).BlockGuid;
            var positionInfo = new BlockPositionInfo(new Vector3Int(10, 0, 10), BlockDirection.North, Vector3Int.one);
            var loaded = ServerContext.BlockFactory.Load(blockGuid, new BlockInstanceId(int.MaxValue), states, positionInfo);

            // スロット内容と摩耗累計4900が新インスタンスへそのまま引き継がれる
            // Both the slot contents and the wear of 4900 carry over to the new instance
            IOpenableInventory loadedInventory = loaded.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.AreEqual(1, loadedInventory.GetItem(0).Count);
            Assert.AreEqual(ForUnitTestItemId.TestCleanRoomFilter, loadedInventory.GetItem(0).Id);

            var resaved = JsonConvert.DeserializeObject<CleanRoomAirFilterSaveJsonObject>(loaded.GetComponent<IBlockSaveState>().GetSaveState());
            Assert.AreEqual(4900, resaved.WearAccumulation, 0.001);
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
        private static IBlock BuildBaseRoom()
        {
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(6, 4, 6));
            CleanRoomHatchTest.ReplaceBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(3, 1, 0));
            CleanRoomHatchTest.ReplaceBlock(ForUnitTestModBlockId.CleanRoomDoorId, new Vector3Int(3, 2, 0));
            return CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(3, 1, 3));
        }

        private static void LoadFilterItems(IBlock block, int count)
        {
            block.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestCleanRoomFilter, count);
        }

        private static void TickWithPower(IBlock block, float power, int ticks)
        {
            // 既存の電力系テストと同様にConsumerへ毎tick直接供給する
            // Supply the consumer directly each tick, as existing electric tests do
            var consumer = block.GetComponent<CleanRoomAirFilterComponent>();
            for (var i = 0; i < ticks; i++)
            {
                consumer.SupplyExternalPower(power);
                GameUpdater.UpdateOneTick();
            }
        }

        #endregion
    }
}
