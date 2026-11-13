using Core.Master;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomPurityTest
    {
        [Test]
        public void StoppingPurifierDropsRoomToOutNextTickTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var block = CleanRoomAirFilterTest.BuildBaseRoom();
            CleanRoomAirFilterTest.LoadFilterItems(block, 5);

            // 換気が足りる部屋は通電でクラスが付く
            // A room with enough air change gets a class once powered
            CleanRoomAirFilterTest.TickWithPower(block, 100f, 2);
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room));
            Assert.AreEqual(0, room.ThresholdIndex);

            // フィルターを抜くと除去能力が0になり、次のtickで Out になる
            // Pulling the filter zeroes the removal volume, dropping the room to Out on the next tick
            block.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestCleanRoomFilter, 0);
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(MasterHolder.CleanRoomMaster.OutThresholdIndex, room.ThresholdIndex);
        }
    }
}
