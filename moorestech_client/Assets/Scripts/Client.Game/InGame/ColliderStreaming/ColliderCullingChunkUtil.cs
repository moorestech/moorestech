using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.ColliderStreaming
{
    /// <summary>
    /// チャンクグリッドの座標計算とプレイヤー半径判定を担う純粋関数群
    /// Pure helpers for chunk-grid coordinate math and player-radius tests
    /// </summary>
    public static class ColliderCullingChunkUtil
    {
        public static long PackChunk(int cx, int cz)
        {
            return ((long)cx << 32) ^ (uint)cz;
        }

        public static int ChunkX(long key)
        {
            return (int)(key >> 32);
        }

        public static int ChunkZ(long key)
        {
            return (int)(key & 0xffffffff);
        }

        // AABBが重なる全チャンクキーを列挙（チャンク境界をまたぐ大きなコライダー対応）
        // Enumerate every chunk key an AABB overlaps (covers colliders spanning chunk borders)
        public static void CollectOverlappingChunks(Bounds bounds, float chunkSize, List<long> result)
        {
            result.Clear();
            var minCx = Mathf.FloorToInt(bounds.min.x / chunkSize);
            var maxCx = Mathf.FloorToInt(bounds.max.x / chunkSize);
            var minCz = Mathf.FloorToInt(bounds.min.z / chunkSize);
            var maxCz = Mathf.FloorToInt(bounds.max.z / chunkSize);
            for (var cx = minCx; cx <= maxCx; cx++)
            for (var cz = minCz; cz <= maxCz; cz++)
                result.Add(PackChunk(cx, cz));
        }

        // プレイヤー中心の有効半径を包む正方内のチャンク座標を列挙（占有判定は呼び出し側）
        // Enumerate chunk coords inside the square that bounds the active radius (occupancy checked by caller)
        public static void CollectChunksInRadiusBox(Vector3 center, float radius, float chunkSize, List<long> result)
        {
            result.Clear();
            var minCx = Mathf.FloorToInt((center.x - radius) / chunkSize);
            var maxCx = Mathf.FloorToInt((center.x + radius) / chunkSize);
            var minCz = Mathf.FloorToInt((center.z - radius) / chunkSize);
            var maxCz = Mathf.FloorToInt((center.z + radius) / chunkSize);
            for (var cx = minCx; cx <= maxCx; cx++)
            for (var cz = minCz; cz <= maxCz; cz++)
                result.Add(PackChunk(cx, cz));
        }

        // チャンクの最近点がプレイヤーから半径内か（XZ平面・厳密）
        // Whether the chunk's nearest point is within the radius of the player (XZ plane, exact)
        public static bool ChunkWithinRadius(long chunkKey, Vector3 center, float radius, float chunkSize)
        {
            var minX = ChunkX(chunkKey) * chunkSize;
            var minZ = ChunkZ(chunkKey) * chunkSize;
            var nearestX = Mathf.Clamp(center.x, minX, minX + chunkSize);
            var nearestZ = Mathf.Clamp(center.z, minZ, minZ + chunkSize);
            var dx = nearestX - center.x;
            var dz = nearestZ - center.z;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
