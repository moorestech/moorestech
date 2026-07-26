using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 樹木配置の共有ヘルパー（マスク判定・密度ノイズ・曲率・ノイズサンプリング・GUID 抽選）。
    // Shared tree-placement helpers (mask test, density noise, curvature, noise sampling, GUID pick).
    internal static class TreePlacementCommon
    {
        // ワールド座標のポイントが mask 内かつ borderMargin 外か。
        // Whether a world-space point lies inside the mask and outside borderMargin.
        public static bool CheckMask(bool[,] mask, Vector2 point, TerrainDimensions dims, int res, float borderMarginPx)
        {
            int hx = Mathf.Clamp(Mathf.RoundToInt(point.x / dims.TerrainWidth * (res - 1)), 0, res - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(point.y / dims.TerrainLength * (res - 1)), 0, res - 1);
            if (!mask[hz, hx]) return false;
            if (borderMarginPx > 0f && BiomeMaskBuilder.IsNearMaskEdge(mask, hx, hz, res, borderMarginPx))
                return false;
            return true;
        }

        // 3スケール密度ノイズ + 島変調を合成する。
        // Combine 3-scale density noise with island modulation.
        public static float SampleDensityNoise(float worldX, float worldZ,
            Vector2[] densityOffsets, Vector2[] detailOffsets, Vector2[] islandOffsets,
            TreeDensityConfig cfg)
        {
            float largeN = ManagedNoise.SampleFBm(worldX, worldZ,
                cfg.densityLargeFrequency, densityOffsets, 0.5f, 2f, 4);
            float midN = ManagedNoise.SampleFBm(worldX, worldZ,
                cfg.densityMidFrequency, detailOffsets, 0.5f, 2f, 3);
            float smallN = ManagedNoise.SampleFBm(worldX, worldZ,
                cfg.densitySmallFrequency, densityOffsets, 2, 0.5f, 2f, 3);
            float baseDensity = largeN * cfg.densityLargeWeight + midN * cfg.densityMidWeight + smallN * cfg.densitySmallWeight;

            float islandN = ManagedNoise.SampleFBm(worldX, worldZ,
                cfg.islandModulationFrequency, islandOffsets, 0.5f, 2f, 3);
            float islandMod = Mathf.Lerp(cfg.islandModulationMin, cfg.islandModulationMax, islandN);
            return Mathf.Max(baseDensity * islandMod, cfg.densityFloor);
        }

        public static float[] ComputeCurvatureMap(float[] heights, int res)
        {
            var curvature = new float[heights.Length];
            for (int z = 1; z < res - 1; z++)
            for (int x = 1; x < res - 1; x++)
            {
                int idx = z * res + x;
                float center = heights[idx];
                float laplacian = heights[idx - 1] + heights[idx + 1]
                                + heights[idx - res] + heights[idx + res]
                                - 4f * center;
                curvature[idx] = laplacian;
            }
            return curvature;
        }

        public static float SampleFilterNoise(PlacementNoise noise, float worldX, float worldZ,
            Vector2[] offsets, float terrainWidth, float terrainLength)
        {
            if (noise.noiseType == MapNoiseType.None) return 0f;
            return ManagedNoise.SamplePlacementNoise(noise, worldX, worldZ, offsets,
                terrainWidth, terrainLength);
        }

        // mapObjectGuid 群から等確率で1件を選ぶ（空エントリは除外）。
        // Pick one mapObjectGuid with equal probability (skipping empty entries).
        public static string PickRandomGuid(string[] guids, System.Random rng)
        {
            if (guids.Length == 1) return guids[0];
            int validCount = 0;
            foreach (var g in guids) if (!string.IsNullOrEmpty(g)) validCount++;
            if (validCount <= 1)
            {
                foreach (var g in guids) if (!string.IsNullOrEmpty(g)) return g;
                return null;
            }
            int pick = rng.Next(validCount);
            int seen = 0;
            foreach (var g in guids)
            {
                if (string.IsNullOrEmpty(g)) continue;
                if (seen == pick) return g;
                seen++;
            }
            return guids[0];
        }
    }
}
