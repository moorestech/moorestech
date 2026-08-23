using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // MapObjectsのスケールと台帳のクラスタ情報を検証
    // Verifies the scale on MapObjects and the cluster info on the ledger
    public class MultiTileMapObjectTransferTest
    {
        private const int GridSide = 3;
        private const int Seed = 7;

        // 採番はタイル1枚ごとに0へ戻るため、転送値を一意化していないと別タイルの岩が同じクラスタに見える。
        // The numbering restarts at 0 for every tile, so without uniquifying the transferred value other tiles' rocks look like one cluster.
        [Test]
        public void 岩クラスタのIDはタイルをまたいで重複しない()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            var run = GenerateWithObjects(config);

            var tileOfClusterId = new Dictionary<int, Vector2Int>();
            var tilesWithCluster = new HashSet<Vector2Int>();
            for (var i = 0; i < run.Output.MapObjects.Count; i++)
            {
                var placement = run.Ledger.Placements[i];
                if (!placement.Cluster.HasValue) continue;
                var clusterId = placement.Cluster.Value.Id;
                var tile = MultiTileTestWorld.TileBucket(run.Output.MapObjects[i].Position.x, run.Output.MapObjects[i].Position.z, config);
                tilesWithCluster.Add(tile);
                if (!tileOfClusterId.TryGetValue(clusterId, out var owner))
                {
                    tileOfClusterId[clusterId] = tile;
                    continue;
                }

                Assert.AreEqual(owner, tile, $"ClusterId {clusterId} が別タイルと共有されている");
            }

            // 1タイルぶんしかクラスタが出ていないと、重複が起きえない設定を検証したことになる。
            // A single tile owning every cluster would mean the assertion ran on a setup where collisions cannot happen.
            Assert.Less(1, tilesWithCluster.Count, "クラスタを持つタイルが1枚しかない");
        }

        // 独立配置はクラスタ情報を持たない(null)。一意化のオフセットを掛けると実クラスタIDへ化ける。
        // An independent placement carries no cluster info (null); applying the uniquifying offset would morph it into a real cluster id.
        [Test]
        public void 独立散布の岩はタイルをまたいでもクラスタ無しのまま残る()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            var run = GenerateWithObjects(config);

            var tilesWithCluster = new HashSet<Vector2Int>();
            var tilesWithIndependent = new HashSet<Vector2Int>();
            for (var i = 0; i < run.Output.MapObjects.Count; i++)
            {
                var mapObject = run.Output.MapObjects[i];
                var placement = run.Ledger.Placements[i];
                var tile = MultiTileTestWorld.TileBucket(mapObject.Position.x, mapObject.Position.z, config);
                if (placement.Cluster.HasValue) tilesWithCluster.Add(tile);
                if (mapObject.MapObjectGuid != MultiTileTestWorld.IndependentMapObjectGuid) continue;

                tilesWithIndependent.Add(tile);
                Assert.That(placement.Cluster, Is.Null, "独立散布の岩がクラスタ情報を持っている");
            }

            // 一意化のオフセットが積み上がった後のタイルにも独立散布が出ていないと、化けようがない設定を検証したことになる。
            // Unless independents also land on a tile written after the offset accumulated, the assertion ran where nothing could have morphed.
            Assert.Less(1, tilesWithIndependent.Count, "独立散布の岩が1タイルにしか出ていない");
            Assert.Less(1, tilesWithCluster.Count, "クラスタが1タイルにしか出ておらずオフセットが積み上がらない");
        }

        // 木はクラスタ情報そのものを持たない。null を素通しできていないと0番クラスタとして届く。
        // Trees own no cluster info at all; failing to pass the null through would deliver them as cluster zero.
        [Test]
        public void 木はクラスタ無しで届く()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            MultiTileTestWorld.EnableTrees(config);

            var run = new VanillaGenerator().Generate(config);

            Assert.IsNotEmpty(run.Output.MapObjects);
            foreach (var placement in run.Ledger.Placements)
                Assert.That(placement.Cluster, Is.Null);
        }

        // 重心がノイズ座標のまま残ると、位置だけがシーン座標という別フレームの組になり岩の周囲を外して塗る。
        // A centroid left in noise space pairs a scene-space position with another frame, painting beside the rock instead of around it.
        [Test]
        public void クラスタ重心は配置物と同じタイルのシーン座標に乗る()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            var run = GenerateWithObjects(config);

            var clustered = new List<LedgerPlacement>();
            for (var i = 0; i < run.Output.MapObjects.Count; i++)
                if (run.Ledger.Placements[i].Cluster.HasValue)
                    clustered.Add(run.Ledger.Placements[i]);

            Assert.IsNotEmpty(clustered, "クラスタを持つ配置物が1件も無い");
            foreach (var placement in clustered)
            {
                var clusterCenter = placement.Cluster.Value.Center;
                MultiTileTestWorld.AssertInsideGrid(clusterCenter.x, clusterCenter.y, config);
                Assert.AreEqual(
                    MultiTileTestWorld.TileBucket(placement.ScenePosition.x, placement.ScenePosition.z, config),
                    MultiTileTestWorld.TileBucket(clusterCenter.x, clusterCenter.y, config),
                    "重心が配置物と別タイルにある");
            }
        }

        // スケールを載せ落とすと全配置物が0倍になり、岩の大きさに追随する周囲テクスチャが消える。
        // Dropping the scale zeroes every placement and erases the surround texture that follows a rock's size.
        [Test]
        public void 配置物のスケールが出力へ写る()
        {
            var run = GenerateWithObjects(MultiTileTestWorld.BuildConfig(GridSide, Seed));

            Assert.IsNotEmpty(run.Output.MapObjects);
            foreach (var mapObject in run.Output.MapObjects)
            {
                Assert.Greater(mapObject.Scale.x, 0f);
                Assert.Greater(mapObject.Scale.y, 0f);
                Assert.Greater(mapObject.Scale.z, 0f);
            }
        }

        private static GenerationRun GenerateWithObjects(TerrainGenerationConfig config)
        {
            MultiTileTestWorld.EnableObjects(config);
            return new VanillaGenerator().Generate(config);
        }
    }
}
