using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // オブジェクト配置の共通計算（高さ・傾斜追従・傾斜フィルタ・法線・GUID 抽選）と
    // クラスター ID 採番カウンタ。
    // Shared object-placement math (height, slope alignment, slope filter, normal, GUID pick)
    // and the cluster-id counter.
    internal static class ObjectPlacementMath
    {
        // 元実装同様プロセス内で単調増加（リセットしない）。ClusterId は配置座標に影響しない。
        // Monotonic within the process like the original (never reset). ClusterId does not affect positions.
        public static int NextClusterId;

        public static float SampleHeight(float[,] heights, float localX, float localZ,
            float w, float l, int hRes)
        {
            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / w * (hRes - 1)), 0, hRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / l * (hRes - 1)), 0, hRes - 1);
            return heights[hz, hx];
        }

        public static Quaternion ApplySlopeAlignment(Quaternion baseRot, float[,] heights,
            float localX, float localZ, float w, float l, int hRes,
            float terrainHeight, float alignment)
        {
            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / w * (hRes - 1)), 0, hRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / l * (hRes - 1)), 0, hRes - 1);
            var normal = ComputeSurfaceNormal(heights, hx, hz, hRes, w, terrainHeight, l);
            var slopeRot = Quaternion.FromToRotation(Vector3.up, normal);
            return Quaternion.Slerp(baseRot, slopeRot * baseRot, alignment);
        }

        public static float ComputeSlopeAngle(float[,] heights, int x, int z, int res,
            float terrainWidth, float terrainHeight, float terrainLength)
        {
            var normal = ComputeSurfaceNormal(heights, x, z, res, terrainWidth, terrainHeight, terrainLength);
            return Mathf.Acos(Mathf.Clamp01(normal.y)) * Mathf.Rad2Deg;
        }

        public static float EvaluateSlopeFilter(float slope, float min, float max, float smoothness)
        {
            if (smoothness <= 0.001f)
                return (slope >= min && slope <= max) ? 1f : 0f;
            float low = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((slope - (min - smoothness)) / smoothness));
            float high = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(((max + smoothness) - slope) / smoothness));
            return low * high;
        }

        public static Vector3 ComputeSurfaceNormal(float[,] heights, int x, int z, int res,
            float terrainWidth, float terrainHeight, float terrainLength)
        {
            float h = heights[z, x];
            float hR = (x < res - 1) ? heights[z, x + 1] : h;
            float hU = (z < res - 1) ? heights[z + 1, x] : h;
            float cellX = terrainWidth / (res - 1);
            float cellZ = terrainLength / (res - 1);
            float dhdx = (hR - h) * terrainHeight / cellX;
            float dhdz = (hU - h) * terrainHeight / cellZ;
            return new Vector3(-dhdx, 1f, -dhdz).normalized;
        }

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
