using Core.Master;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomHatchPollutionTest
    {
        [Test]
        public void HatchThroughputIncreasesPollutionTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();

            // 内寸3x1x3の部屋の壁1枚をハッチにし、搬送先チェストを室内に置く
            // Swap one wall of a 3x1x3 room for a hatch with a receiving chest inside
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 2, 4));
            var hatch = CleanRoomHatchTest.ReplaceBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(2, 1, 0));
            CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(2, 1, 1));

            GameUpdater.UpdateOneTick();
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room));

            // 無搬送40tickの汚染増分を基準として測る
            // Measure the pollution delta over 40 idle ticks as the baseline
            var baselineStart = room.ImpurityCount;
            for (var i = 0; i < 40; i++) GameUpdater.UpdateOneTick();
            var baselineDelta = room.ImpurityCount - baselineStart;

            // 毎tick1個をハッチ経由で搬送し kHatch×レート分の上乗せを検証する
            // Feed one item per tick through the hatch and verify the kHatch x rate surcharge
            var hatchInventory = hatch.GetComponent<IBlockInventory>();
            var transportStart = room.ImpurityCount;
            for (var i = 0; i < 40; i++)
            {
                hatchInventory.InsertItem(ServerContext.ItemStackFactory.Create(new ItemId(1), 1), InsertItemContext.Empty);
                GameUpdater.UpdateOneTick();
            }
            var transportDelta = room.ImpurityCount - transportStart;

            // 定常レート20個/秒 × kHatch0.3 = +6/秒。ランプ込みで+5以上の上乗せになる
            // Steady 20 items/sec x kHatch 0.3 = +6/sec; with ramp-up the surcharge exceeds +5
            var hatchThroughput = hatch.GetComponent<ICleanRoomItemHatch>();
            Assert.Greater(hatchThroughput.RecentThroughputPerSecond, 15);
            Assert.Greater(transportDelta, baselineDelta + 5.0);
        }
    }
}
