using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // 多タイル生成が MapObjects へ載せるスケール・クラスタID・クラスタ重心を検証する。
    // Verifies the scale, cluster id, and cluster centroid that multi-tile generation puts onto MapObjects.
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
            var output = GenerateWithObjects(config);

            var tileOfClusterId = new Dictionary<int, Vector2Int>();
            var tilesWithCluster = new HashSet<Vector2Int>();
            foreach (var mapObject in output.MapObjects)
            {
                if (mapObject.ClusterId < 0) continue;
                var tile = MultiTileTestWorld.TileBucket(mapObject.Position.x, mapObject.Position.z, config);
                tilesWithCluster.Add(tile);
                if (!tileOfClusterId.TryGetValue(mapObject.ClusterId, out var owner))
                {
                    tileOfClusterId[mapObject.ClusterId] = tile;
                    continue;
                }

                Assert.AreEqual(owner, tile, $"ClusterId {mapObject.ClusterId} が別タイルと共有されている");
            }

            // 1タイルぶんしかクラスタが出ていないと、重複が起きえない設定を検証したことになる。
            // A single tile owning every cluster would mean the assertion ran on a setup where collisions cannot happen.
            Assert.Less(1, tilesWithCluster.Count, "クラスタを持つタイルが1枚しかない");
        }

        // 独立配置は ClusterId=-1 の空クラスタ情報を持つ。一意化のオフセットを掛けると実クラスタIDへ化ける。
        // An independent placement carries an empty ClusterId=-1; applying the uniquifying offset would morph it into a real cluster id.
        [Test]
        public void 独立散布の岩はタイルをまたいでもクラスタIDが_1のまま残る()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            var output = GenerateWithObjects(config);

            var tilesWithCluster = new HashSet<Vector2Int>();
            var tilesWithIndependent = new HashSet<Vector2Int>();
            foreach (var mapObject in output.MapObjects)
            {
                var tile = MultiTileTestWorld.TileBucket(mapObject.Position.x, mapObject.Position.z, config);
                if (0 <= mapObject.ClusterId) tilesWithCluster.Add(tile);
                if (mapObject.MapObjectGuid != MultiTileTestWorld.IndependentMapObjectGuid) continue;

                tilesWithIndependent.Add(tile);
                Assert.AreEqual(-1, mapObject.ClusterId, "独立散布の岩がクラスタIDを持っている");
                Assert.AreEqual(Vector2.zero, mapObject.ClusterCenter, "独立散布の岩が重心を持っている");
            }

            // 一意化のオフセットが積み上がった後のタイルにも独立散布が出ていないと、化けようがない設定を検証したことになる。
            // Unless independents also land on a tile written after the offset accumulated, the assertion ran where nothing could have morphed.
            Assert.Less(1, tilesWithIndependent.Count, "独立散布の岩が1タイルにしか出ていない");
            Assert.Less(1, tilesWithCluster.Count, "クラスタが1タイルにしか出ておらずオフセットが積み上がらない");
        }

        // 木はクラスタ情報そのものを持たない。null を素通しできていないと0番クラスタとして届く。
        // Trees own no cluster info at all; failing to pass the null through would deliver them as cluster zero.
        [Test]
        public void 木はクラスタIDが_1で届く()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            MultiTileTestWorld.EnableTrees(config);

            var output = new VanillaGenerator().Generate(config);

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.AreEqual(-1, mapObject.ClusterId);
                Assert.AreEqual(Vector2.zero, mapObject.ClusterCenter);
            }
        }

        // 重心がノイズ座標のまま残ると、位置だけがシーン座標という別フレームの組になり岩の周囲を外して塗る。
        // A centroid left in noise space pairs a scene-space position with another frame, painting beside the rock instead of around it.
        [Test]
        public void クラスタ重心は配置物と同じタイルのシーン座標に乗る()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            var output = GenerateWithObjects(config);

            var clustered = output.MapObjects.FindAll(mapObject => 0 <= mapObject.ClusterId);
            Assert.IsNotEmpty(clustered, "クラスタを持つ配置物が1件も無い");
            foreach (var mapObject in clustered)
            {
                MultiTileTestWorld.AssertInsideGrid(mapObject.ClusterCenter.x, mapObject.ClusterCenter.y, config);
                Assert.AreEqual(
                    MultiTileTestWorld.TileBucket(mapObject.Position.x, mapObject.Position.z, config),
                    MultiTileTestWorld.TileBucket(mapObject.ClusterCenter.x, mapObject.ClusterCenter.y, config),
                    "重心が配置物と別タイルにある");
            }
        }

        // スケールを載せ落とすと全配置物が0倍になり、岩の大きさに追随する周囲テクスチャが消える。
        // Dropping the scale zeroes every placement and erases the surround texture that follows a rock's size.
        [Test]
        public void 配置物のスケールが出力へ写る()
        {
            var output = GenerateWithObjects(MultiTileTestWorld.BuildConfig(GridSide, Seed));

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.Greater(mapObject.Scale.x, 0f);
                Assert.Greater(mapObject.Scale.y, 0f);
                Assert.Greater(mapObject.Scale.z, 0f);
            }
        }

        private static MapGenerationOutput GenerateWithObjects(TerrainGenerationConfig config)
        {
            MultiTileTestWorld.EnableObjects(config);
            return new VanillaGenerator().Generate(config);
        }
    }
}
