using System.Collections.Generic;
using Game.Block.Blocks.TrainRail.Naming;
using Game.Block.Interface;
using Game.Context;
using Game.Map;
using Game.Map.Interface.Json;
using NUnit.Framework;
using Tests.Util;
using UnityEngine;

namespace Tests.UnitTest.Game.TrainStation.Naming
{
    public class TrainStationNameFeatureScannerTest
    {
        [SetUp]
        public void Setup()
        {
            TrainTestHelper.CreateEnvironment();
        }

        [TestCase(0, 0, "Test1 Station")]
        [TestCase(4, 0, "Water Station")]
        [TestCase(95, 95, "Test2 Station")]
        [TestCase(60, 60, "Station")]
        public void RealMapUsesVeinThenMinedItemThenFallback(int x, int z, string expected)
        {
            var scanner = new TrainStationNameFeatureScanner(ServerContext.ItemMapVeinDatastore,
                ServerContext.FluidMapVeinDatastore, ServerContext.MapObjectDatastore, Names());
            Assert.AreEqual(expected, scanner.ScanBaseName(Station(new Vector3Int(x, 0, z))));
        }

        [Test]
        public void EnglishTranslationOverridesMasterName()
        {
            var names = new TrainStationFeatureEnglishNames(new Dictionary<string, string>
            {
                ["item.00000000-0000-0000-1234-000000000001.name"] = "Iron"
            });
            var scanner = new TrainStationNameFeatureScanner(ServerContext.ItemMapVeinDatastore,
                ServerContext.FluidMapVeinDatastore, ServerContext.MapObjectDatastore, names);
            Assert.AreEqual("Iron Station", scanner.ScanBaseName(Station(Vector3Int.zero)));
        }

        [TestCase(32, "Test1 Station")]
        [TestCase(33, "Station")]
        public void VeinRadiusIncludesBoundaryAndIgnoresHeight(int distance, string expected)
        {
            var map = EmptyMap();
            // 中心(1,1)から鉱脈セルの左端までの距離を固定する
            // Fix distance from center (1,1) to the vein cell's left edge
            map.MapVeins.Add(new MapVeinInfoJson
            {
                VeinGuidStr = "11111111-0000-0000-0000-000000000001",
                MinX = 1 + distance, MaxX = 1 + distance,
                MinY = 1000, MaxY = 1000, MinZ = 0, MaxZ = 0
            });
            Assert.AreEqual(expected, Scanner(map, Names()).ScanBaseName(SquareStation()));
        }

        [TestCase(32f, 0f, "Test2 Station")]
        [TestCase(32.01f, 0f, "Station")]
        [TestCase(24f, 24f, "Station")]
        public void MapObjectRadiusUsesCircleAndIgnoresHeight(float dx, float dz, string expected)
        {
            var map = EmptyMap();
            map.MapObjects.Add(Object(1, "00000000-0000-2222-0000-000000000001", new Vector3(1 + dx, 1000, 1 + dz)));
            Assert.AreEqual(expected, Scanner(map, Names()).ScanBaseName(SquareStation()));
        }

        [Test]
        public void DestroyedAndDecorationObjectsDoNotNameStation()
        {
            var map = EmptyMap();
            map.MapObjects.Add(Object(1, "00000000-0000-2222-0000-000000000001", Vector3.one));
            map.MapObjects.Add(Object(2, "00000000-0000-4444-0000-000000000001", Vector3.one));
            var objects = new MapObjectDatastore(new MapObjectFactory(), map);
            objects.Get(1).Destroy();
            var scanner = new TrainStationNameFeatureScanner(new ItemMapVeinDatastore(map), new FluidMapVeinDatastore(map), objects, Names());
            Assert.AreEqual("Station", scanner.ScanBaseName(SquareStation()));
        }

        [Test]
        public void MostFrequentMinedNameWinsAndTiesUseOrdinalName()
        {
            var map = EmptyMap();
            map.MapObjects.Add(Object(1, "00000000-0000-2222-0000-000000000001", Vector3.one));
            map.MapObjects.Add(Object(2, "8c0e1339-be75-4690-99cd-58b5385a17cd", Vector3.one));
            Assert.AreEqual("Test1 Station", Scanner(map, Names()).ScanBaseName(SquareStation()));
            // 同数の辞書順と、最多への切り替えを独立に確かめる
            // Verify ordinal ties separately from switching to a majority
            map.MapObjects.Add(Object(3, "00000000-0000-2222-0000-000000000001", Vector3.one));
            Assert.AreEqual("Test2 Station", Scanner(map, Names()).ScanBaseName(SquareStation()));
        }

        private static TrainStationFeatureEnglishNames Names()
        {
            return new TrainStationFeatureEnglishNames(new Dictionary<string, string>());
        }

        private static TrainStationNameFeatureScanner Scanner(MapInfoJson map, TrainStationFeatureEnglishNames names)
        {
            return new TrainStationNameFeatureScanner(new ItemMapVeinDatastore(map), new FluidMapVeinDatastore(map),
                new MapObjectDatastore(new MapObjectFactory(), map), names);
        }

        private static MapInfoJson EmptyMap()
        {
            return new MapInfoJson { MapObjects = new List<MapObjectInfoJson>(), MapVeins = new List<MapVeinInfoJson>() };
        }

        private static MapObjectInfoJson Object(int id, string guid, Vector3 position)
        {
            return new MapObjectInfoJson { InstanceId = id, MapObjectGuidStr = guid, X = position.x, Y = position.y, Z = position.z };
        }

        private static BlockPositionInfo Station(Vector3Int position)
        {
            return new BlockPositionInfo(position, BlockDirection.North, new Vector3Int(3, 1, 8));
        }

        private static BlockPositionInfo SquareStation()
        {
            return new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, new Vector3Int(2, 1, 2));
        }
    }
}
