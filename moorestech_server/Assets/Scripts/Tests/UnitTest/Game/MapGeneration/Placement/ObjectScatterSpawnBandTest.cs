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
        private const float NearRadius = ObjectScatterBandTestWorld.NearRadius;

        [Test]
        public void JSONのbandsが実行時ObjectEntryへ写る()
        {
            var generation = TestGenerationConfigFactory.CreateWithMapObjectGuid(TestGenerationConfigFactory.TestMapObjectGuid);
            var config = GenerationRuntimeConfigFactory.Build(generation);

            var scatter = (ObjectScatterParam)config.grassland.objectConfig.entries[0].placement;
            Assert.AreEqual(2, scatter.bands.Length);
            Assert.AreEqual(250f, scatter.bands[0].outerRadiusMeters);
            Assert.AreEqual(2f, scatter.bands[0].pointsPerHectare);
            Assert.AreEqual(-1f, scatter.bands[1].outerRadiusMeters);
            Assert.AreEqual(1f, scatter.bands[1].pointsPerHectare);
        }

        [Test]
        public void 近傍帯だけ密度を持つ散布はスポーンから近傍半径未満にのみ置かれる()
        {
            var output = ObjectScatterBandTestWorld.GenerateScatter(gridSide: 1, useClusterMode: false,
                (NearRadius, 30f),
                (-1f, 0f));

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                Assert.Less(ObjectScatterBandTestWorld.DistanceFromSpawnXz(mapObject.Position, output.SpawnPoint), NearRadius);
        }

        [Test]
        public void 最外周だけ密度を持つ散布はスポーンから近傍半径以上にのみ置かれる()
        {
            var output = ObjectScatterBandTestWorld.GenerateScatter(gridSide: 1, useClusterMode: false,
                (NearRadius, 0f),
                (-1f, 30f));

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                Assert.GreaterOrEqual(ObjectScatterBandTestWorld.DistanceFromSpawnXz(mapObject.Position, output.SpawnPoint), NearRadius);
        }

        [Test]
        public void クラスタモードは近傍帯のクラスタ中心だけをスポーン近傍に置く()
        {
            const float density = 30f;
            var output = ObjectScatterBandTestWorld.GenerateScatter(gridSide: 1, useClusterMode: true,
                (NearRadius, density),
                (-1f, 0f));

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.GreaterOrEqual(mapObject.ClusterId, 0);
                var center = new Vector3(mapObject.ClusterCenter.x, 0f, mapObject.ClusterCenter.y);
                Assert.Less(ObjectScatterBandTestWorld.DistanceFromSpawnXz(center, output.SpawnPoint), NearRadius);
            }

            // 中心数はリング面積×density由来のはず。clusterCount固定実装ではこの桁に収まらない。
            // The centre count should track ring area times density; a fixed clusterCount implementation would miss this band.
            ObjectScatterBandTestWorld.AssertClusterCenterCountMatchesDensity(output, density);
        }

        // 複数タイル（中心タイルにスポーン）でも、近傍帯の判定がタイルローカルではなく世界座標のスポーン距離で効くことを固定する。
        // WorldOffset加算が落ちると、各タイルが自タイルのローカル原点をスポーンとみなし、全タイルの同じ相対位置に湧いてしまう回帰を検知する。
        // Pins that the near-band test uses the world-space distance to spawn even across multiple tiles (spawn sits in the centre tile).
        // If the WorldOffset addition is dropped, every tile treats its own local origin as spawn and objects reappear at the same relative spot on all tiles.
        [Test]
        public void 複数タイルでも近傍帯はワールド座標のスポーン距離で判定される()
        {
            var output = ObjectScatterBandTestWorld.GenerateScatter(gridSide: 3, useClusterMode: false,
                (NearRadius, 30f),
                (-1f, 0f));

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                Assert.Less(ObjectScatterBandTestWorld.DistanceFromSpawnXz(mapObject.Position, output.SpawnPoint), NearRadius);
        }

        [Test]
        public void 複数タイルでもクラスタ中心はワールド座標のスポーン距離で判定される()
        {
            const float density = 30f;
            var output = ObjectScatterBandTestWorld.GenerateScatter(gridSide: 3, useClusterMode: true,
                (NearRadius, density),
                (-1f, 0f));

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.GreaterOrEqual(mapObject.ClusterId, 0);
                var center = new Vector3(mapObject.ClusterCenter.x, 0f, mapObject.ClusterCenter.y);
                Assert.Less(ObjectScatterBandTestWorld.DistanceFromSpawnXz(center, output.SpawnPoint), NearRadius);
            }

            // 近傍リングは中心タイルの内側にほぼ収まるため、単一タイルと同じ桁の中心数が出るはず。
            // The near ring sits almost entirely inside the centre tile, so the centre count should land in the same order as the single-tile case.
            ObjectScatterBandTestWorld.AssertClusterCenterCountMatchesDensity(output, density);
        }

        // 宣言順が外半径の降順でも、リングは帯の実体で対応する（添字前提の実装だと近傍帯と最外周帯が入れ替わる）。
        // Even when bands are declared in descending radius order, rings map to the band objects themselves; an index-based implementation would swap the near and outer bands.
        [Test]
        public void 降順に宣言しても近傍帯の密度が近傍リングへ効く()
        {
            var output = ObjectScatterBandTestWorld.GenerateScatter(gridSide: 1, useClusterMode: false,
                (-1f, 0f),
                (NearRadius, 30f));

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
                Assert.Less(ObjectScatterBandTestWorld.DistanceFromSpawnXz(mapObject.Position, output.SpawnPoint), NearRadius);
        }

        [Test]
        public void 降順に宣言してもクラスタモードの近傍帯が近傍リングへ効く()
        {
            const float density = 30f;
            var output = ObjectScatterBandTestWorld.GenerateScatter(gridSide: 1, useClusterMode: true,
                (-1f, 0f),
                (NearRadius, density));

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.GreaterOrEqual(mapObject.ClusterId, 0);
                var center = new Vector3(mapObject.ClusterCenter.x, 0f, mapObject.ClusterCenter.y);
                Assert.Less(ObjectScatterBandTestWorld.DistanceFromSpawnXz(center, output.SpawnPoint), NearRadius);
            }

            ObjectScatterBandTestWorld.AssertClusterCenterCountMatchesDensity(output, density);
        }

    }
}
