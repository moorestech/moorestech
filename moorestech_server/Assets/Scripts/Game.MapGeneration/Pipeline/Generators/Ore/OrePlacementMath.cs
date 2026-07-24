using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 鉱脈配置の高さサンプリングと傾斜フィルタの共通計算。
    // Shared height sampling and slope-filter math for ore placement.
    internal static class OrePlacementMath
    {
        public static float SampleHeight(float[,] heights, float localX, float localZ,
            float w, float l, int hRes)
        {
            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / w * (hRes - 1)), 0, hRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / l * (hRes - 1)), 0, hRes - 1);
            return heights[hz, hx];
        }

        public static float ComputeSlopeAngle(float[,] heights, int x, int z, int res,
            float terrainWidth, float terrainHeight, float terrainLength)
        {
            float h = heights[z, x];
            float hR = (x < res - 1) ? heights[z, x + 1] : h;
            float hU = (z < res - 1) ? heights[z + 1, x] : h;
            float cellX = terrainWidth / (res - 1);
            float cellZ = terrainLength / (res - 1);
            float dhdx = (hR - h) * terrainHeight / cellX;
            float dhdz = (hU - h) * terrainHeight / cellZ;
            var normal = new Vector3(-dhdx, 1f, -dhdz).normalized;
            return Mathf.Acos(Mathf.Clamp01(normal.y)) * Mathf.Rad2Deg;
        }

        // slopeMax 以下を通過、smoothness 幅で遷移するフィルタ。
        // Filter passing below slopeMax with a smoothness transition band.
        public static float EvaluateSlopeFilter(float slope, float max, float smoothness)
        {
            if (smoothness <= 0.001f)
                return slope <= max ? 1f : 0f;
            return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((slope - max) / smoothness));
        }
    }
}
