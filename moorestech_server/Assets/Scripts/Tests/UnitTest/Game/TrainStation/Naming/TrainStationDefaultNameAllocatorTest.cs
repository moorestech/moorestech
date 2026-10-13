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

            // 2番の駅を3番の名前へ改名すると、次は空いた2番、その次は4番になる
            // Renaming station 2 to number 3 frees 2 for the next station and pushes the one after to 4
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
            // 撤去で空いた番号は同じ特徴の別位置でも再利用される
            // A number freed by removal is reused at another location with the same feature
            Assert.AreEqual("Test1 Station 2", Place(new Vector3Int(0, 0, 16)).StationName);
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
