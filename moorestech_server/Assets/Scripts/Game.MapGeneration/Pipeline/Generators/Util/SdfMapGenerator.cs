using System.Threading.Tasks;

namespace Game.MapGeneration.Pipeline.Generators.Util
{
    // SpatialGrid から距離マップ（float[,]）を生成する。行単位 Parallel.For で並列化し
    // シングルスレッドと完全一致の結果を返す。Detail 用フィルタ算出は移植対象外。
    // Generates a distance map from a SpatialGrid; row-parallel and identical to single-thread.
    // The detail-filter radius helper is out of scope for the server port.
    public static class SdfMapGenerator
    {
        public static float[,] Generate(
            SpatialGrid grid, int resolution,
            float terrainWidth, float terrainLength, float maxSearchRadius)
        {
            if (grid == null || grid.Count == 0)
                return null;

            var map = new float[resolution, resolution];
            int res = resolution;
            float tw = terrainWidth;
            float tl = terrainLength;
            float maxR = maxSearchRadius;

            Parallel.For(0, res, z =>
            {
                for (int x = 0; x < res; x++)
                {
                    float worldX = (float)x / (res - 1) * tw;
                    float worldZ = (float)z / (res - 1) * tl;
                    map[z, x] = grid.FindMinDistance(worldX, worldZ, maxR);
                }
            });

            return map;
        }

        public static float[,] GenerateSingleThread(
            SpatialGrid grid, int resolution,
            float terrainWidth, float terrainLength, float maxSearchRadius)
        {
            if (grid == null || grid.Count == 0)
                return null;

            var map = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float worldX = (float)x / (resolution - 1) * terrainWidth;
                float worldZ = (float)z / (resolution - 1) * terrainLength;
                map[z, x] = grid.FindMinDistance(worldX, worldZ, maxSearchRadius);
            }
            return map;
        }
    }
}
