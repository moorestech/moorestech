using UnityEngine;
using Game.MapGeneration.Pipeline.Config;

namespace Game.MapGeneration.Pipeline.Spawn
{
    // スポーン探索の座標・範囲・マージン計算補助。
    // Coordinate/extent/margin helpers for spawn search.
    internal static class SpawnRegionGeometry
    {
        public static Vector2 CentroidWorld(ConnectedComponents.Component comp, CoarseBiomeGrid grid)
        {
            double sx = 0, sy = 0;
            foreach (int idx in comp.Cells)
            {
                int cx = idx % grid.Width;
                int cy = idx / grid.Width;
                sx += cx; sy += cy;
            }
            float ax = (float)(sx / comp.Cells.Count);
            float ay = (float)(sy / comp.Cells.Count);
            return new Vector2(grid.OriginX + ax * grid.CellSize, grid.OriginZ + ay * grid.CellSize);
        }

        public static float DefaultScanExtent(TerrainGenerationConfig config)
        {
            float w = config.gridSizeX * config.terrainWidth;
            float l = config.gridSizeZ * config.terrainLength;
            return Mathf.Max(w, l);
        }

        public static Vector2 GridCenterWorld(TerrainGenerationConfig config)
        {
            int halfX = config.gridSizeX / 2;
            int halfZ = config.gridSizeZ / 2;
            float minX = -halfX * config.terrainWidth;
            float maxX = (config.gridSizeX - halfX) * config.terrainWidth;
            float minZ = -halfZ * config.terrainLength;
            float maxZ = (config.gridSizeZ - halfZ) * config.terrainLength;
            return new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        }

        public static float WindowSizeFor(SpawnCandidate cand, SpawnSearchConfig ss)
        {
            float longSide = Mathf.Max(cand.BBoxW, cand.BBoxH);
            return longSide + 2f * ss.windowMargin;
        }

        public static float EdgeMargin(TerrainGenerationConfig config, SpawnSearchConfig ss)
        {
            float pX = config.terrainWidth / (config.Resolution - 1);
            int divisor = Mathf.Max(1, config.boundaryConfig != null ? config.boundaryConfig.blurRadiusDivisor : 2);
            float blendM = (config.biomeBlendRadius + config.biomeBlendRadius / (float)divisor) * pX;
            float beachR = 0f;
            var shore = config.shoreConfig;
            if (shore != null)
                beachR = Mathf.Max(Mathf.Max(shore.beachLandTextureRadius, shore.beachLandTerrainRadius),
                                   Mathf.Max(shore.beachSeaTextureRadius, shore.beachSeaTerrainRadius));
            return blendM + ss.waterClearanceMin + beachR * pX;
        }
    }
}
