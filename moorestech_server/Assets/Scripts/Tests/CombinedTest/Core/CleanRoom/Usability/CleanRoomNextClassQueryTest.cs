using System.Linq;
using Core.Master;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom.Info;
using Game.CleanRoom.Util;
using Game.Context;
using NUnit.Framework;
using Tests.CombinedTest.Core.CleanRoom.Machine;

namespace Tests.CombinedTest.Core.CleanRoom.Usability
{
    /// <summary>
    ///     部屋の要約の清浄機の台数・止まっている台数・次のクラスまでの台数を固定する
    ///     Pins the room summary's purifier count, stopped count and units to the next class
    /// </summary>
    public class CleanRoomNextClassQueryTest
    {
        [Test]
        public void StoppedPurifierCountsUnitsFromOutToTheLowestClassTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            GameUpdater.UpdateOneTick();

            // 現在の除去は0。追加した清浄機は定格で動き、その分だけ空気セルが減る
            // Current removal is zero; each added purifier runs at its rated removal and takes up air cells
            var airFilter = filter.GetComponent<ICleanRoomAirFilter>();
            Assert.AreEqual(0d, airFilter.RemovalVolumePerSecond);
            var room = AsEnclosed(new CleanRoomInfoQuery(datastore, ServerContext.WorldBlockDatastore).Query(filter)).Room;
            Assert.AreEqual(1, room.AirFilterCount);
            Assert.AreEqual(1, room.StoppedAirFilterCount);
            Assert.IsInstanceOf<CleanRoomOutStanding>(room.Class);
            var queried = AsQueriedFromAirFilter(room);
            Assert.IsInstanceOf<CleanRoomUnitsToNextClassOutlook>(queried.NextClass);
            var units = (CleanRoomUnitsToNextClassOutlook)queried.NextClass;

            // 返された台数なら昇格し、1台少ないと昇格しない
            // The returned count promotes the room, and one fewer does not
            var outIndex = MasterHolder.CleanRoomMaster.OutThresholdIndex;
            var targetIndex = outIndex - 1;
            Assert.IsTrue(datastore.TryGetCleanRoom(filter, out var detectedRoom));
            var unitCells = filter.BlockPositionInfo.EnumeratePositions().Count();
            Assert.AreEqual(MasterHolder.CleanRoomMaster.Thresholds[targetIndex].ClassName, units.NextClassName);
            Assert.Greater(units.Units, 0);
            Assert.LessOrEqual(ProjectedIndex(units.Units), targetIndex);
            Assert.Greater(ProjectedIndex(units.Units - 1), targetIndex);

            // 照会した清浄機自身の状態（電力0・フィルター満タン）は、清浄機から引いた照会の型が必ず持つ
            // The queried purifier's own state (zero power, fresh filter) is always carried by the from-a-purifier type
            Assert.AreEqual(0d, queried.Status.PowerRatio);
            Assert.AreEqual(1d, queried.Status.CurrentFilterRemainingRatio, 1e-9);

            #region Internal

            int ProjectedIndex(int count)
            {
                var projectedRate = count * airFilter.RatedRemovalVolumePerSecond / (detectedRoom.Volume - count * unitCells);
                return CleanRoomPurityLogic.DecideThresholdIndex(outIndex, projectedRate, datastore.RequiredAirChangeRates);
            }

            #endregion
        }

        [Test]
        public void MachineQueryDoesNotCountUnitsTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            var machine = CleanRoomProcessMachineTestHelpers.PlaceProcessMachine();
            GameUpdater.UpdateOneTick();

            // 機械の画面は部屋クラスの1行だけなので、機械から引いた照会は台数を数えない（台数のまとめは同じ部屋の値）
            // The machine screen shows only the class line, so a query from a machine does not count units (the purifier totals are the same room's)
            var room = AsEnclosed(new CleanRoomInfoQuery(datastore, ServerContext.WorldBlockDatastore).Query(machine)).Room;
            Assert.IsInstanceOf<CleanRoomQueriedFromNonAirFilter>(room.QueriedFrom);
            Assert.AreEqual(1, room.AirFilterCount);
        }

        [Test]
        public void BestClassHasNoNextClassTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            var query = new CleanRoomInfoQuery(datastore, ServerContext.WorldBlockDatastore);

            // 満電の清浄機でいちばん良いクラス（添字0）まで進める
            // Run the fully powered purifier up to the best class (index 0)
            CleanRoomMachineTestHelpers.EnsureFilterWiredPower(filter);
            GameUpdater.UpdateOneTick();
            var room = AsEnclosed(query.Query(filter)).Room;
            for (var i = 0; i < 3000 && !(room.Class is CleanRoomClassifiedStanding { ThresholdIndex: 0 }); i++)
            {
                GameUpdater.UpdateOneTick();
                room = AsEnclosed(query.Query(filter)).Room;
            }

            // 最高クラスには次のクラスが無く、満電の清浄機は止まっていない
            // The best class has no next class, and the fully powered purifier is not stopped
            Assert.IsInstanceOf<CleanRoomClassifiedStanding>(room.Class, "room never reached the best class within 3000 ticks");
            Assert.IsInstanceOf<CleanRoomAtBestClassOutlook>(AsQueriedFromAirFilter(room).NextClass);
            Assert.AreEqual(0, room.StoppedAirFilterCount);
        }

        private static CleanRoomEnclosedInfo AsEnclosed(CleanRoomInfo info)
        {
            Assert.IsInstanceOf<CleanRoomEnclosedInfo>(info, $"expected an enclosed room but got {info.GetType().Name}");
            return (CleanRoomEnclosedInfo)info;
        }

        private static CleanRoomQueriedFromAirFilter AsQueriedFromAirFilter(CleanRoomRoomSummary room)
        {
            Assert.IsInstanceOf<CleanRoomQueriedFromAirFilter>(room.QueriedFrom, $"expected a query from a purifier but got {room.QueriedFrom.GetType().Name}");
            return (CleanRoomQueriedFromAirFilter)room.QueriedFrom;
        }
    }
}
