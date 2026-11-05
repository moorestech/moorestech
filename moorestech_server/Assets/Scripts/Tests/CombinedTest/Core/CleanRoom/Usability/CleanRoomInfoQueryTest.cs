using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.CleanRoom.Info;
using Game.Context;
using NUnit.Framework;
using Tests.CombinedTest.Core.CleanRoom.Machine;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.Usability
{
    public class CleanRoomInfoQueryTest
    {
        [Test]
        public void EnclosedRoomSummaryFromFilterTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            GameUpdater.UpdateOneTick();
            var query = new CleanRoomInfoQuery(datastore, ServerContext.WorldBlockDatastore);

            // 室内の清浄機から引くと、清浄機1台（座標・フィルター5個・残量満タン）を持つ内寸27-1=26の部屋が返る
            // Querying the purifier inside returns the 27-1=26 room holding one purifier (position, 5 filters, full life)
            var room = AsEnclosed(query.Query(filter)).Room;
            Assert.AreEqual(26, room.Volume);
            Assert.AreEqual(1, room.AirFilters.Count);
            Assert.AreEqual(new Vector3Int(1, 1, 1), room.AirFilters[0].Position);
            Assert.AreEqual(5, room.AirFilters[0].Status.FilterItemCount);
            Assert.AreEqual(1d, room.AirFilters[0].Status.CurrentFilterRemainingRatio, 1e-9);

            // 検出直後の部屋は Out から始まるので、クラスは Out の型になる
            // A freshly detected room starts at Out, so its class is the Out type
            Assert.IsInstanceOf<CleanRoomOutStanding>(room.Class);
        }

        [Test]
        public void ClassifiedRoomCarriesClassNameAndIndexTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            var query = new CleanRoomInfoQuery(datastore, ServerContext.WorldBlockDatastore);

            // 満電の清浄機でクラス到達まで進める
            // Run the fully powered purifier until a class is reached
            CleanRoomMachineTestHelpers.EnsureFilterWiredPower(filter);
            CleanRoomClassStanding standing = CleanRoomOutStanding.Instance;
            for (var i = 0; i < 3000 && standing is CleanRoomOutStanding; i++)
            {
                GameUpdater.UpdateOneTick();
                standing = AsEnclosed(query.Query(filter)).Room.Class;
            }

            // 分類済みの型は、閾値行の添字とその行のクラス名を持つ
            // The classified type carries the threshold row index and that row's class name
            Assert.IsInstanceOf<CleanRoomClassifiedStanding>(standing, "room never reached a class within 3000 ticks");
            var classified = (CleanRoomClassifiedStanding)standing;
            Assert.AreEqual(MasterHolder.CleanRoomMaster.Thresholds[classified.ThresholdIndex].ClassName, classified.ClassName);
        }

        [Test]
        public void BreachedRoomReportsOpeningsFromInsideBlockTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            GameUpdater.UpdateOneTick();
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();

            // 部屋が崩れた後、室内の清浄機から引くと未密閉の型になり、壊した壁1か所が漏れとして数えられる
            // After the room collapses, querying the purifier inside yields the not-enclosed type counting the one broken wall cell
            var info = new CleanRoomInfoQuery(datastore, ServerContext.WorldBlockDatastore).Query(filter);
            Assert.IsInstanceOf<CleanRoomNotEnclosedInfo>(info);
            var leak = ((CleanRoomNotEnclosedInfo)info).Leak;
            Assert.IsInstanceOf<CleanRoomOpeningsLeak>(leak);
            Assert.AreEqual(1, ((CleanRoomOpeningsLeak)leak).TotalOpeningCount);
        }

        #region TestHelper

        private static CleanRoomEnclosedInfo AsEnclosed(CleanRoomInfo info)
        {
            Assert.IsInstanceOf<CleanRoomEnclosedInfo>(info, $"expected an enclosed room but got {info.GetType().Name}");
            return (CleanRoomEnclosedInfo)info;
        }

        #endregion
    }
}
