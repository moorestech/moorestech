using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.TrainStation.Naming
{
    public class TrainStationDefaultNameAllocatorTest
    {
        private TrainTestEnvironment _environment;

        [Test]
        public void SameFeatureGetsSmallestFreeNumber()
        {
            _environment = TrainTestHelper.CreateEnvironment();
            // +z方向に並べるとTest1鉱脈(0,5,1)が最寄りのまま（Water鉱脈より1近い）
            // Stacking along +z keeps the Test1 vein (0,5,1) nearest (1 closer than the Water vein)
            var first = Place(Vector3Int.zero);
            var second = Place(new Vector3Int(0, 0, 8));
            Assert.AreEqual("Test1 Station", first.StationName);
            Assert.AreEqual("Test1 Station 2", second.StationName);

            // 2→3へ改名後、次は2、その次は4
            // After renaming 2 to 3, allocate 2 next, then 4
            second.SetStationName("Test1 Station 3");
            Assert.AreEqual("Test1 Station 2", Place(new Vector3Int(0, 0, 16)).StationName);
            Assert.AreEqual("Test1 Station 4", Place(new Vector3Int(0, 0, 24)).StationName);
        }

        [Test]
        public void RemovedStationNumberIsReused()
        {
            _environment = TrainTestHelper.CreateEnvironment();
            Place(Vector3Int.zero);
            Place(new Vector3Int(0, 0, 8));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 0, 8), BlockRemoveReason.ManualRemove));
            Assert.AreEqual("Test1 Station 2", Place(new Vector3Int(0, 0, 8)).StationName);
        }

        [Test]
        public void FallbackNameWhenNothingNearby()
        {
            _environment = TrainTestHelper.CreateEnvironment();
            Assert.AreEqual("Station", Place(new Vector3Int(60, 0, 60)).StationName);
            Assert.AreEqual("Station 2", Place(new Vector3Int(64, 0, 60)).StationName);
        }

        [Test]
        public void RenamedDifferentFeatureStationReservesNameGlobally()
        {
            _environment = TrainTestHelper.CreateEnvironment();
            Place(new Vector3Int(60, 0, 60)).SetStationName("Test1 Station");
            Assert.AreEqual("Test1 Station 2", Place(Vector3Int.zero).StationName);
        }

        private TrainStationComponent Place(Vector3Int position)
        {
            return TrainTestHelper.PlaceBlock(_environment, ForUnitTestModBlockId.TestTrainStation, position, BlockDirection.North)
                .GetComponent<TrainStationComponent>();
        }
    }
}
