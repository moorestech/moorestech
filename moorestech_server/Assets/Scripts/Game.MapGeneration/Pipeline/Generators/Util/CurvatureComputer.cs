using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators.Util
{
    /// <summary>
    /// ハイトマップから曲率・方位角を計算するユーティリティ。
    /// DetailPlacementGenerator の曲率フィルタ・角度フィルタで使用。
    /// </summary>
    public static class CurvatureComputer
    {
        /// <summary>
        /// Laplacian 曲率を計算。0-1 正規化（0.5=平坦、>0.5=凸、&lt;0.5=凹）。
        /// heights は [z, x] の 2D 配列。
        /// </summary>
        public static float[,] ComputeCurvature(float[,] heights, int resolution)
        {
            var curvature = new float[resolution, resolution];
            // Laplacianの生値を一旦蓄積してからmin/maxで正規化
            var raw = new float[resolution, resolution];
            float minVal = float.MaxValue, maxVal = float.MinValue;

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int xm = Mathf.Max(0, x - 1);
                    int xp = Mathf.Min(resolution - 1, x + 1);
                    int zm = Mathf.Max(0, z - 1);
                    int zp = Mathf.Min(resolution - 1, z + 1);

                    // 離散 Laplacian: 隣接4セルの平均との差
                    float laplacian = heights[zm, x] + heights[zp, x]
                                    + heights[z, xm] + heights[z, xp]
                                    - 4f * heights[z, x];

                    raw[z, x] = laplacian;
                    if (laplacian < minVal) minVal = laplacian;
                    if (laplacian > maxVal) maxVal = laplacian;
                }
            }

            // 0-1 に正規化。範囲がゼロ（完全フラット）なら全て0.5
            float range = maxVal - minVal;
            if (range < 1e-8f)
            {
                for (int z = 0; z < resolution; z++)
                    for (int x = 0; x < resolution; x++)
                        curvature[z, x] = 0.5f;
            }
            else
            {
                float invRange = 1f / range;
                for (int z = 0; z < resolution; z++)
                    for (int x = 0; x < resolution; x++)
                        curvature[z, x] = (raw[z, x] - minVal) * invRange;
            }

            return curvature;
        }

        /// <summary>
        /// 法線の方位角（コンパス方向）を計算。0-360度で返す。
        /// 北=0°、東=90°、南=180°、西=270°。
        /// </summary>
        public static float[,] ComputeAzimuth(float[,] heights, int resolution)
        {
            var azimuth = new float[resolution, resolution];

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int xm = Mathf.Max(0, x - 1);
                    int xp = Mathf.Min(resolution - 1, x + 1);
                    int zm = Mathf.Max(0, z - 1);
                    int zp = Mathf.Min(resolution - 1, z + 1);

                    // 勾配ベクトル（∂h/∂x, ∂h/∂z）
                    float dx = heights[z, xp] - heights[z, xm];
                    float dz = heights[zp, x] - heights[zm, x];

                    // atan2 で方位角を計算（度数法、0-360）
                    float angle = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg;
                    if (angle < 0f) angle += 360f;
                    azimuth[z, x] = angle;
                }
            }

            return azimuth;
        }
    }
}
