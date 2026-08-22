using System.Collections.Generic;
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
            const float density = 30f;
            var output = GenerateScatter(gridSide: 1, useClusterMode: true,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = density },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 0f });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.GreaterOrEqual(mapObject.ClusterId, 0);
                var center = new Vector3(mapObject.ClusterCenter.x, 0f, mapObject.ClusterCenter.y);
                Assert.Less(DistanceFromSpawnXz(center, output.SpawnPoint), NearRadius);
            }

            // 中心数はリング面積×density由来のはず。clusterCount固定実装ではこの桁に収まらない。
            // The centre count should track ring area times density; a fixed clusterCount implementation would miss this band.
            AssertClusterCenterCountMatchesDensity(output, density);
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
            const float density = 30f;
            var output = GenerateScatter(gridSide: 3, useClusterMode: true,
                new ObjectScatterBand { outerRadiusMeters = NearRadius, density = density },
                new ObjectScatterBand { outerRadiusMeters = -1f, density = 0f });

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.GreaterOrEqual(mapObject.ClusterId, 0);
                var center = new Vector3(mapObject.ClusterCenter.x, 0f, mapObject.ClusterCenter.y);
                Assert.Less(DistanceFromSpawnXz(center, output.SpawnPoint), NearRadius);
            }

            // 近傍リングは中心タイルの内側にほぼ収まるため、単一タイルと同じ桁の中心数が出るはず。
            // The near ring sits almost entirely inside the centre tile, so the centre count should land in the same order as the single-tile case.
            AssertClusterCenterCountMatchesDensity(output, density);
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

        // 近傍帯クラスタ中心数が「リング面積×density/1万」の桁に収まることを固定する。
        // Poisson散布・マスク・境界除外で下振れはするが、旧clusterCount固定実装が生む極端な過不足は検知する幅を取る。
        // Pins that the near-band centre count lands in the order of ring-area times density over 1e4.
        // Poisson sampling, masking, and edge exclusion can undershoot, but the range still catches the gross over/under-count a fixed clusterCount implementation would produce.
        private static void AssertClusterCenterCountMatchesDensity(MapGenerationOutput output, float density)
        {
            var clusterIds = new HashSet<int>();
            foreach (var mapObject in output.MapObjects)
                clusterIds.Add(mapObject.ClusterId);

            float ringArea = Mathf.PI * NearRadius * NearRadius;
            int expectedCenters = Mathf.RoundToInt(density * ringArea / 10000f);

            Assert.That(clusterIds.Count, Is.InRange(Mathf.RoundToInt(expectedCenters * 0.3f), Mathf.RoundToInt(expectedCenters * 1.7f)),
                $"cluster centre count {clusterIds.Count} is outside the expected order for density {density} (expected around {expectedCenters})");
        }
    }
}
