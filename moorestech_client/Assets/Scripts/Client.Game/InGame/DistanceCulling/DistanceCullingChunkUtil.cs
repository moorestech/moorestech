using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.DistanceCulling
{
    /// <summary>
    /// チャンクグリッドの座標計算と中心半径判定を担う純粋関数群
    /// Pure helpers for chunk-grid coordinate math and center-radius tests
    /// </summary>
    public static class DistanceCullingChunkUtil
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

        // AABBが重なる全チャンクキーを列挙（チャンク境界をまたぐ大きな対象対応）
        // Enumerate every chunk key an AABB overlaps (covers targets spanning chunk borders)
        public static void CollectOverlappingChunks(Bounds bounds, float chunkSize, List<long> result)
        {
            FillChunkBox(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, chunkSize, result);
        }

        // 半径の外接正方を列挙。占有判定は呼出側
        // Enumerate the radius bounding square; the caller checks occupancy
        public static void CollectChunksInRadiusBox(Vector3 center, float radius, float chunkSize, List<long> result)
        {
            // 閉区間の接線チャンクを負側にも含める
            // Include negative-side tangent chunks for the closed radius test
            result.Clear();
            var minCx = Mathf.CeilToInt((center.x - radius) / chunkSize) - 1;
            var maxCx = Mathf.FloorToInt((center.x + radius) / chunkSize);
            var minCz = Mathf.CeilToInt((center.z - radius) / chunkSize) - 1;
            var maxCz = Mathf.FloorToInt((center.z + radius) / chunkSize);
            for (var cx = minCx; cx <= maxCx; cx++)
            for (var cz = minCz; cz <= maxCz; cz++)
                result.Add(PackChunk(cx, cz));
        }

        // 登録AABBの占有チャンクを列挙する
        // Enumerate occupied chunks for a registration AABB
        private static void FillChunkBox(float minX, float maxX, float minZ, float maxZ, float chunkSize, List<long> result)
        {
            result.Clear();
            var minCx = Mathf.FloorToInt(minX / chunkSize);
            var maxCx = Mathf.FloorToInt(maxX / chunkSize);
            var minCz = Mathf.FloorToInt(minZ / chunkSize);
            var maxCz = Mathf.FloorToInt(maxZ / chunkSize);
            for (var cx = minCx; cx <= maxCx; cx++)
            for (var cz = minCz; cz <= maxCz; cz++)
                result.Add(PackChunk(cx, cz));
        }

        // チャンクの最近点が中心から半径内か（XZ平面・厳密）
        // Whether the chunk's nearest point is within the radius of the center (XZ plane, exact)
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
