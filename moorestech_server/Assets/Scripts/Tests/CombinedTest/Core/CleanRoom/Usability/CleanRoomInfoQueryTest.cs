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
        public void EnclosedRoomSummaryFromWallCornerAndFilterTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            GameUpdater.UpdateOneTick();
            var world = ServerContext.WorldBlockDatastore;
            var query = new CleanRoomInfoQuery(datastore, world);

            // 壁から引くと、清浄機1台（座標・フィルター5個・残量満タン）を持つ内寸27-1=26の部屋が返る
            // Querying a wall returns the 27-1=26 room holding one purifier (position, 5 filters, full life)
            var fromWall = AsEnclosed(query.Query(world.GetBlock(new Vector3Int(0, 2, 2))));
            Assert.AreEqual(1, fromWall.Rooms.Count);
            var room = fromWall.Rooms[0];
            Assert.AreEqual(26, room.Volume);
            Assert.AreEqual(1, room.AirFilters.Count);
            Assert.AreEqual(new Vector3Int(1, 1, 1), room.AirFilters[0].Position);
            Assert.AreEqual(5, room.AirFilters[0].Status.FilterItemCount);
            Assert.AreEqual(1d, room.AirFilters[0].Status.CurrentFilterRemainingRatio, 1e-9);

            // 検出直後の部屋は Out から始まるので、クラスは Out の型になる
            // A freshly detected room starts at Out, so its class is the Out type
            Assert.IsInstanceOf<CleanRoomOutStanding>(room.Class);

            // 角の壁は6近傍に部屋セルを持たないが、26近傍で同じ部屋を見つける
            // A corner wall has no room cell among its six neighbors but finds the room via its 26 neighbors
            AsEnclosed(query.Query(world.GetBlock(new Vector3Int(0, 0, 0))));

            // 室内の清浄機自身からも同じ部屋を引ける
            // The purifier inside the room resolves the same room
            Assert.AreEqual(26, AsEnclosed(query.Query(filter)).Rooms[0].Volume);
        }

        [Test]
        public void ClassifiedRoomCarriesClassNameAndIndexTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            var query = new CleanRoomInfoQuery(datastore, ServerContext.WorldBlockDatastore);

            // 清浄機を満電で回し、部屋がいずれかのクラスに入るまで進める
            // Run the purifier at full power until the room enters some class
            CleanRoomMachineTestHelpers.EnsureFilterWiredPower(filter);
            CleanRoomClassStanding standing = CleanRoomOutStanding.Instance;
            for (var i = 0; i < 3000 && standing is CleanRoomOutStanding; i++)
            {
                GameUpdater.UpdateOneTick();
                standing = AsEnclosed(query.Query(filter)).Rooms[0].Class;
            }

            // 分類済みの型は、閾値行の添字とその行のクラス名を持つ
            // The classified type carries the threshold row index and that row's class name
            Assert.IsInstanceOf<CleanRoomClassifiedStanding>(standing, "room never reached a class within 3000 ticks");
            var classified = (CleanRoomClassifiedStanding)standing;
            Assert.AreEqual(MasterHolder.CleanRoomMaster.Thresholds[classified.ThresholdIndex].ClassName, classified.ClassName);
        }

        [Test]
        public void SharedWallReportsBothRoomsTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            CleanRoomDetectionTest.BuildBox(new Vector3Int(4, 0, 0), new Vector3Int(8, 4, 4));

            // 検出はtickごとの予算で分割されるので、2部屋が揃うまで数tick進める
            // Detection is split by a per-tick budget, so advance a few ticks until both rooms exist
            for (var i = 0; i < 5; i++) GameUpdater.UpdateOneTick();

            // 2部屋が共有する壁は両方の部屋を返す
            // A wall shared by two rooms returns both
            var info = new CleanRoomInfoQuery(datastore, ServerContext.WorldBlockDatastore).Query(ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(4, 2, 2)));
            Assert.AreEqual(2, AsEnclosed(info).Rooms.Count);
        }

        [Test]
        public void BreachedRoomReportsOpeningsFromInsideBlockTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = CleanRoomMachineTestHelpers.BuildSmallCleanRoomWithFilter();
            GameUpdater.UpdateOneTick();
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            GameUpdater.UpdateOneTick();

            // 部屋が崩れた後、室内の清浄機から引くと未密閉の型になり、壊した壁の位置が漏れとして返る
            // After the room collapses, querying the purifier inside yields the not-enclosed type with the broken wall cell as the leak
            var info = new CleanRoomInfoQuery(datastore, ServerContext.WorldBlockDatastore).Query(filter);
            Assert.IsInstanceOf<CleanRoomNotEnclosedInfo>(info);
            var leak = ((CleanRoomNotEnclosedInfo)info).Leak;
            Assert.IsInstanceOf<CleanRoomOpeningsLeak>(leak);
            CollectionAssert.Contains(((CleanRoomOpeningsLeak)leak).OpeningCells, new Vector3Int(2, 2, 0));
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
