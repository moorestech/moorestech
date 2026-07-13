using UnityEngine;

namespace MapGenerator.Pipeline.Generators.Util
{
    /// <summary>
    /// バイオーム重み配列からwinner-takes-all方式のboolマスクを構築する。
    /// オーケストレーター（TerrainGenerator）がper-biomeループ前に全マスクを一括生成し、
    /// 各ジェネレーターに渡す。ジェネレーター自身はバイオームの概念を持たない。
    /// </summary>
    public static class BiomeMaskBuilder
    {
        /// <summary>
        /// 指定バイオームのwinnerマスクを構築する。
        /// biomeWeights[pixelIndex, 2+biomeIdx] が最大のピクセルのみtrue。
        /// 同率の場合はbiomeIndex小が勝つ（決定論的）。
        /// </summary>
        /// <param name="biomeWeights">全ピクセルのバイオーム重み [pixelCount, 2+biomeCount]</param>
        /// <param name="res">ハイトマップ解像度（辺のピクセル数）</param>
        /// <param name="biomeIdx">対象バイオームのインデックス（0-based）</param>
        /// <param name="biomeCount">有効バイオーム総数</param>
        /// <returns>res×resのboolマスク。mask[z,x]=trueの領域にのみ配置可能</returns>
        public static bool[,] BuildWinnerMask(
            float[,] biomeWeights, int res, int biomeIdx, int biomeCount)
        {
            var mask = new bool[res, res];
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int idx = z * res + x;
                    float myWeight = biomeWeights[idx, 2 + biomeIdx];
                    if (myWeight <= 0f) continue;

                    bool isWinner = true;
                    for (int b = 0; b < biomeCount; b++)
                    {
                        if (b == biomeIdx) continue;
                        float otherWeight = biomeWeights[idx, 2 + b];
                        // 同率はbiomeIndex小が勝つ
                        if (otherWeight > myWeight || (otherWeight == myWeight && b < biomeIdx))
                        {
                            isWinner = false;
                            break;
                        }
                    }
                    mask[z, x] = isWinner;
                }
            }
            return mask;
        }

        /// <summary>
        /// 全バイオームのwinnerマスクを一括構築する。
        /// </summary>
        public static bool[][,] BuildAllWinnerMasks(
            float[,] biomeWeights, int res, int biomeCount)
        {
            var masks = new bool[biomeCount][,];
            for (int b = 0; b < biomeCount; b++)
                masks[b] = BuildWinnerMask(biomeWeights, res, b, biomeCount);
            return masks;
        }

        /// <summary>
        /// 指定ピクセルがmask境界からmarginPixels以内にあるかを判定する。
        /// trueの場合、borderMargin設定に基づいて配置をスキップすべき。
        /// </summary>
        /// <param name="mask">バイオームマスク</param>
        /// <param name="x">ピクセルX座標</param>
        /// <param name="z">ピクセルZ座標</param>
        /// <param name="res">解像度</param>
        /// <param name="marginPixels">マージン幅（ピクセル単位、小数切り上げ）</param>
        /// <returns>境界から近い場合true</returns>
        public static bool IsNearMaskEdge(bool[,] mask, int x, int z, int res, float marginPixels)
        {
            if (marginPixels <= 0f) return false;
            int margin = Mathf.CeilToInt(marginPixels);
            int zMin = Mathf.Max(z - margin, 0);
            int zMax = Mathf.Min(z + margin, res - 1);
            int xMin = Mathf.Max(x - margin, 0);
            int xMax = Mathf.Min(x + margin, res - 1);

            // テレインの端はmask外とみなす
            if (z - margin < 0 || z + margin >= res || x - margin < 0 || x + margin >= res)
                return true;

            for (int dz = zMin; dz <= zMax; dz++)
                for (int dx = xMin; dx <= xMax; dx++)
                    if (!mask[dz, dx])
                        return true;
            return false;
        }

        /// <summary>
        /// borderMargin（メートル）をピクセル数に変換する。
        /// </summary>
        public static float MetersToPixels(float marginMeters, float terrainWidth, int resolution)
        {
            if (marginMeters <= 0f) return 0f;
            float pixelSize = terrainWidth / (resolution - 1);
            return marginMeters / pixelSize;
        }
    }
}
