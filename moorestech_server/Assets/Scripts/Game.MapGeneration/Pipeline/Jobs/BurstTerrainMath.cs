using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// Burst対応の地形数学関数。TerrainMathの全機能をmath.*で再実装。
    /// NativeArray&lt;float&gt;（フラット1D）を直接扱うオーバーロードを提供。
    /// </summary>
    // ジョブ内からのみ呼ばれるためクラスレベルの[BurstCompile]は不要
    public static class BurstTerrainMath
    {
        /// <summary>
        /// 3次エルミート補間。edge0==edge1のときはステップ関数にフォールバック。
        /// </summary>
        public static float Smoothstep(float edge0, float edge1, float x)
        {
            // 縮退ケース: エッジ幅ゼロならステップ関数として扱う
            if (math.abs(edge1 - edge0) < 1e-6f) return x >= edge1 ? 1f : 0f;
            float t = math.saturate((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 値が[min, max]範囲内なら1、範囲外で滑らかに0へ減衰する台形フィルタ。
        /// </summary>
        public static float FilterRange(float value, float min, float max,
            float smoothnessMin, float smoothnessMax)
        {
            // 下端・上端それぞれのSmoothstepを掛け合わせて台形を作る
            float lower = Smoothstep(min - smoothnessMin, min, value);
            float upper = 1f - Smoothstep(max, max + smoothnessMax, value);
            return lower * upper;
        }

        /// <summary>
        /// フラットNativeArray版の傾斜計算。ジョブ内で使用。
        /// </summary>
        public static float ComputeSlope(in NativeArray<float> heights, int res,
            int x, int y, float terrainWidth, float terrainHeight, float terrainLength)
        {
            int idx = y * res + x;
            float h = heights[idx];
            // 端ピクセルでは隣接がないので自身の高さで代用
            float hR = (x < res - 1) ? heights[idx + 1] : h;
            float hU = (y < res - 1) ? heights[idx + res] : h;

            // グリッド間距離をワールド単位に変換
            float cellX = terrainWidth / (res - 1);
            float cellZ = terrainLength / (res - 1);

            // 高さ差分をワールドスケールにして勾配ベクトルを計算
            float dhdx = (hR - h) * terrainHeight / cellX;
            float dhdz = (hU - h) * terrainHeight / cellZ;

            // 勾配の大きさからatan→度に変換
            return math.atan(math.sqrt(dhdx * dhdx + dhdz * dhdz)) * math.TODEGREES;
        }

        /// <summary>
        /// 4近傍ラプラシアン曲率。境界ピクセルは0を返す。
        /// </summary>
        public static float ComputeCurvature(in NativeArray<float> heights, int res, int x, int y)
        {
            // 境界ピクセルは隣接データ不足で0を返す
            if (x <= 0 || x >= res - 1 || y <= 0 || y >= res - 1) return 0f;
            int idx = y * res + x;
            float center = heights[idx];
            // 4近傍ラプラシアン: 周囲の合計 - 4*中心
            float laplacian = heights[idx - res] + heights[idx + res]
                              + heights[idx - 1] + heights[idx + 1]
                              - 4f * center;
            return laplacian;
        }
    }
}
