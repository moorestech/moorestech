using System.Collections.Generic;
using Client.Game.InGame.ColliderStreaming;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    /// <summary>
    /// チャンク座標計算と半径判定の純粋関数テスト
    /// Pure-function tests for chunk coordinate math and radius tests
    /// </summary>
    public class ColliderCullingChunkUtilTest
    {
        [Test]
        public void PackChunk_RoundTrips_IncludingNegatives()
        {
            // 正負混在のチャンク座標がパック/アンパックで往復する
            // Mixed positive/negative chunk coords survive pack/unpack round-trip
            foreach (var cx in new[] { 0, 1, -1, 123, -123, int.MaxValue, int.MinValue })
            foreach (var cz in new[] { 0, 1, -1, 456, -456, int.MaxValue, int.MinValue })
            {
                var key = ColliderCullingChunkUtil.PackChunk(cx, cz);
                Assert.AreEqual(cx, ColliderCullingChunkUtil.ChunkX(key));
                Assert.AreEqual(cz, ColliderCullingChunkUtil.ChunkZ(key));
            }
        }

        [Test]
        public void CollectOverlappingChunks_SmallBounds_YieldsSingleChunk()
        {
            // チャンク内に収まる小さなAABBは1チャンクのみ
            // A small AABB inside one chunk yields a single chunk
            var result = new List<long>();
            var bounds = new Bounds(new Vector3(5f, 0f, 5f), new Vector3(1f, 1f, 1f));
            ColliderCullingChunkUtil.CollectOverlappingChunks(bounds, 10f, result);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(ColliderCullingChunkUtil.PackChunk(0, 0), result[0]);
        }

        [Test]
        public void CollectOverlappingChunks_SpanningBorder_YieldsEveryOverlappedChunk()
        {
            // チャンク境界をまたぐ大きなAABBは重なる全チャンクを列挙する
            // A large AABB spanning borders enumerates every overlapped chunk
            var result = new List<long>();
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(-5f, 0f, 8f), new Vector3(15f, 0f, 32f));
            ColliderCullingChunkUtil.CollectOverlappingChunks(bounds, 10f, result);

            // x:-5..15 -> chunk -1,0,1  /  z:8..32 -> chunk 0,1,2,3
            var expected = new HashSet<long>();
            for (var cx = -1; cx <= 1; cx++)
            for (var cz = 0; cz <= 3; cz++)
                expected.Add(ColliderCullingChunkUtil.PackChunk(cx, cz));

            Assert.AreEqual(expected.Count, result.Count);
            CollectionAssert.AreEquivalent(expected, result);
        }

        [Test]
        public void ChunkWithinRadius_InsideChunk_IsWithin()
        {
            // プレイヤーがチャンク内にいれば距離0で必ず範囲内
            // Player inside the chunk is within at distance zero
            var key = ColliderCullingChunkUtil.PackChunk(0, 0);
            Assert.IsTrue(ColliderCullingChunkUtil.ChunkWithinRadius(key, new Vector3(5f, 0f, 5f), 1f, 10f));
        }

        [Test]
        public void ChunkWithinRadius_FarChunk_IsOutside()
        {
            // 遠方チャンクは半径外
            // A far chunk is outside the radius
            var key = ColliderCullingChunkUtil.PackChunk(100, 100);
            Assert.IsFalse(ColliderCullingChunkUtil.ChunkWithinRadius(key, Vector3.zero, 15f, 10f));
        }

        [Test]
        public void ChunkWithinRadius_UsesNearestPoint_NotCenter()
        {
            // 中心は遠いが最近点は近いケースを最近点で判定する
            // Judge by the nearest point even when the chunk center is far
            var key = ColliderCullingChunkUtil.PackChunk(2, 0); // world x [20,30], z [0,10]
            // center (18,_,5): nearest point (20,_,5) -> distance 2 (<5); chunk center (25,5) would be 7 (>5)
            Assert.IsTrue(ColliderCullingChunkUtil.ChunkWithinRadius(key, new Vector3(18f, 0f, 5f), 5f, 10f));
        }

        [Test]
        public void ChunkWithinRadius_NegativeChunk_NearestPointCorrect()
        {
            // 負座標チャンクでも最近点計算が正しい
            // Nearest-point math is correct for negative-coordinate chunks
            var key = ColliderCullingChunkUtil.PackChunk(-1, -1); // world [-10,0]
            Assert.IsTrue(ColliderCullingChunkUtil.ChunkWithinRadius(key, new Vector3(2f, 0f, 2f), 3f, 10f));
            Assert.IsFalse(ColliderCullingChunkUtil.ChunkWithinRadius(key, new Vector3(2f, 0f, 2f), 1f, 10f));
        }

        [Test]
        public void CollectChunksInRadiusBox_CoversRadiusExtent()
        {
            // 半径正方は中心±半径を覆うチャンクを列挙する
            // The radius box enumerates chunks covering center +/- radius
            var result = new List<long>();
            ColliderCullingChunkUtil.CollectChunksInRadiusBox(Vector3.zero, 15f, 10f, result);

            // x in [-15,15] -> chunks -2..1, z same -> 4x4 = 16
            Assert.AreEqual(16, result.Count);
            Assert.Contains(ColliderCullingChunkUtil.PackChunk(-2, -2), result);
            Assert.Contains(ColliderCullingChunkUtil.PackChunk(1, 1), result);
        }
    }
}
