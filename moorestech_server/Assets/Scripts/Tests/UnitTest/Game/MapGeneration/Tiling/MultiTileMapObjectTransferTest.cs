using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
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
            MultiTileTestWorld.EnableClusteredObjects(config);

            var output = new VanillaGenerator().Generate(config);

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

        // -1 は「クラスタに属さない」の表明。オフセットを掛けて別の値へ化けるとクライアントが独立岩を束ねる。
        // -1 declares "belongs to no cluster"; letting an offset turn it into another value would make clients bundle independent rocks.
        [Test]
        public void クラスタに属さない配置物のIDは_1のまま残る()
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            MultiTileTestWorld.EnableTrees(config);

            var output = new VanillaGenerator().Generate(config);

            var nonCluster = output.MapObjects.FindAll(mapObject => mapObject.ClusterId < 0);
            Assert.IsNotEmpty(nonCluster, "クラスタに属さない配置物が1件も無い");
            foreach (var mapObject in nonCluster)
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
            MultiTileTestWorld.EnableClusteredObjects(config);

            var output = new VanillaGenerator().Generate(config);

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
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            MultiTileTestWorld.EnableClusteredObjects(config);

            var output = new VanillaGenerator().Generate(config);

            Assert.IsNotEmpty(output.MapObjects);
            foreach (var mapObject in output.MapObjects)
            {
                Assert.Greater(mapObject.Scale.x, 0f);
                Assert.Greater(mapObject.Scale.y, 0f);
                Assert.Greater(mapObject.Scale.z, 0f);
            }
        }
    }
}
