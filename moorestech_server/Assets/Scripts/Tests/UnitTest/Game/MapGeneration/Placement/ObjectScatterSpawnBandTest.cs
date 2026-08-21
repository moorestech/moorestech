using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Runtime;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Tiling;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Placement
{
    // 独立散布entriesのスポーン距離帯（bands）がJSONから実行時設定へ写り、配置がリング内に収まることを固定する。
    // Pins that object-scatter spawn-distance bands flow from JSON into runtime config and that placement stays inside the ring.
    public class ObjectScatterSpawnBandTest
    {
        private const int Seed = 11;
        private const float NearRadius = 60f;

        [Test]
        public void JSONのbandsが実行時ObjectEntryへ写る()
        {
            var generation = TestGenerationConfigFactory.CreateWithMapObjectGuid(TestGenerationConfigFactory.TestMapObjectGuid);
            var config = GenerationRuntimeConfigFactory.Build(generation);

            var entry = config.grassland.objectConfig.entries[0];
            Assert.AreEqual(1, entry.bands.Length);
            Assert.AreEqual(-1f, entry.bands[0].outerRadiusMeters);
            Assert.AreEqual(1f, entry.bands[0].density);
            Assert.AreEqual(8, entry.bands[0].clusterCount);
        }

        [Test]
        public void 近傍帯だけ密度を持つ散布はスポーンから近傍半径未満にのみ置かれる()
        {
            var output = GenerateScatter(useClusterMode: false,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = 30f, clusterCount = 0 },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 0f, clusterCount = 0 });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                Assert.Less(DistanceFromSpawnXz(mapObject.Position, output.SpawnPoint), NearRadius);
        }

        [Test]
        public void 最外周だけ密度を持つ散布はスポーンから近傍半径以上にのみ置かれる()
        {
            var output = GenerateScatter(useClusterMode: false,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = 0f, clusterCount = 0 },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 30f, clusterCount = 0 });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                Assert.GreaterOrEqual(DistanceFromSpawnXz(mapObject.Position, output.SpawnPoint), NearRadius);
        }

        [Test]
        public void クラスタモードは近傍帯のクラスタ中心だけをスポーン近傍に置く()
        {
            var output = GenerateScatter(useClusterMode: true,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = 0f, clusterCount = 400 },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 0f, clusterCount = 0 });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.GreaterOrEqual(mapObject.ClusterId, 0);
                var center = new Vector3(mapObject.ClusterCenter.x, 0f, mapObject.ClusterCenter.y);
                Assert.Less(DistanceFromSpawnXz(center, output.SpawnPoint), NearRadius);
            }
        }

        // 1タイル・Grassland/Forest 両方に同じ散布エントリを置き、木は出さずに生成する。
        // Generate one tile with the same scatter entry in Grassland and Forest, with no trees.
        private static MapGenerationOutput GenerateScatter(bool useClusterMode, params ObjectScatterBand[] bands)
        {
            var config = MultiTileTestWorld.BuildConfig(1, Seed);
            config.generateObject = true;
            config.grassland.objectConfig = BuildScatterConfig(useClusterMode, bands);
            config.forest.objectConfig = BuildScatterConfig(useClusterMode, bands);
            return new VanillaGenerator().Generate(config);
        }

        private static BiomeObjectConfig BuildScatterConfig(bool useClusterMode, ObjectScatterBand[] bands)
        {
            return new BiomeObjectConfig
            {
                entries = new[]
                {
                    new BiomeObjectConfig.ObjectEntry
                    {
                        mapObjectGuids = new[] { MultiTileTestWorld.IndependentMapObjectGuid },
                        bands = bands,
                        useClusterMode = useClusterMode,
                        scaleRange = new Vector2(1f, 1f),
                    },
                },
            };
        }

        private static float DistanceFromSpawnXz(Vector3 position, Vector3 spawn)
        {
            return Vector2.Distance(new Vector2(position.x, position.z), new Vector2(spawn.x, spawn.z));
        }
    }
}
