using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Runtime;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Tiling;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Placement
{
    // bandsのJSON→ランタイム反映とリング内配置を固定するテスト。
    // Pins that bands flow from JSON into runtime config and placement stays inside the ring.
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
        }

        [Test]
        public void 近傍帯だけ密度を持つ散布はスポーンから近傍半径未満にのみ置かれる()
        {
            var output = GenerateScatter(gridSide: 1, useClusterMode: false,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = 30f },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 0f });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                Assert.Less(DistanceFromSpawnXz(mapObject.Position, output.SpawnPoint), NearRadius);
        }

        [Test]
        public void 最外周だけ密度を持つ散布はスポーンから近傍半径以上にのみ置かれる()
        {
            var output = GenerateScatter(gridSide: 1, useClusterMode: false,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = 0f },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 30f });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                Assert.GreaterOrEqual(DistanceFromSpawnXz(mapObject.Position, output.SpawnPoint), NearRadius);
        }

        [Test]
        public void クラスタモードは近傍帯のクラスタ中心だけをスポーン近傍に置く()
        {
            var output = GenerateScatter(gridSide: 1, useClusterMode: true,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = 30f },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 0f });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.GreaterOrEqual(mapObject.ClusterId, 0);
                var center = new Vector3(mapObject.ClusterCenter.x, 0f, mapObject.ClusterCenter.y);
                Assert.Less(DistanceFromSpawnXz(center, output.SpawnPoint), NearRadius);
            }
        }

        // 複数タイル（中心タイルにスポーン）でも、近傍帯の判定がタイルローカルではなく世界座標のスポーン距離で効くことを固定する。
        // WorldOffset加算が落ちると、各タイルが自タイルのローカル原点をスポーンとみなし、全タイルの同じ相対位置に湧いてしまう回帰を検知する。
        // Pins that the near-band test uses the world-space distance to spawn even across multiple tiles (spawn sits in the centre tile).
        // If the WorldOffset addition is dropped, every tile treats its own local origin as spawn and objects reappear at the same relative spot on all tiles.
        [Test]
        public void 複数タイルでも近傍帯はワールド座標のスポーン距離で判定される()
        {
            var output = GenerateScatter(gridSide: 3, useClusterMode: false,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = 30f },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 0f });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                Assert.Less(DistanceFromSpawnXz(mapObject.Position, output.SpawnPoint), NearRadius);
        }

        [Test]
        public void 複数タイルでもクラスタ中心はワールド座標のスポーン距離で判定される()
        {
            var output = GenerateScatter(gridSide: 3, useClusterMode: true,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = 30f },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 0f });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.GreaterOrEqual(mapObject.ClusterId, 0);
                var center = new Vector3(mapObject.ClusterCenter.x, 0f, mapObject.ClusterCenter.y);
                Assert.Less(DistanceFromSpawnXz(center, output.SpawnPoint), NearRadius);
            }
        }

        // gridSide四方に散布entryを生成、木は出さない。
        // Generate a gridSide-by-gridSide grid with the scatter entry and no trees.
        private static MapGenerationOutput GenerateScatter(int gridSide, bool useClusterMode, params ObjectScatterBand[] bands)
        {
            var config = MultiTileTestWorld.BuildConfig(gridSide, Seed);
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
