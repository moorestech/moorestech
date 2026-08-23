using UnityEngine;
using static Game.MapGeneration.Cache.TerrainVisualCacheFormat;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     再構築したalphamapを、保存と適用が共有するRGBA8平面へ畳む。
    ///     裸地の塗りは合計が1を超える画素を作る（移植元から引き継いだ性質・SurroundBlendWriter参照）が、
    ///     平面は1画素1バイトなので、そのまま焼くと再読込時だけ頭打ちして比率が変わる。
    ///     合計が1を超える画素だけ比率を保ったまま合計1へ寄せ、全画素を1/255刻みへ量子化することで、
    ///     初回生成の平面とキャッシュから読み戻した平面を完全に一致させる。
    ///     Terrainへ渡る重みはUnityが正規化して8bitへ焼くため、この畳み込みで見た目は変わらない。
    ///     Folds a rebuilt alphamap into the RGBA8 planes that storage and application share.
    ///     Bare-ground painting produces pixels summing above one (a property inherited from the source; see
    ///     SurroundBlendWriter), but a plane holds one byte per pixel, so baking those as-is clips only on
    ///     reload and shifts the ratio. Pixels above one are scaled back to a sum of one with their ratio intact
    ///     and every pixel is quantized to 1/255 steps, making a freshly built plane and a cache-loaded one identical.
    ///     Unity normalizes the weights it receives and bakes them to 8 bits, so this fold leaves the look unchanged.
    /// </summary>
    public static class StoredAlphamapWeights
    {
        // 入力z-x-layer、出力RGBA8平面列
        // Input: z-x-layer; output: RGBA8 planes.
        public static byte[][] ToPlanes(float[,,] alphamap)
        {
            var resolutionZ = alphamap.GetLength(0);
            var resolutionX = alphamap.GetLength(1);
            var layerCount = alphamap.GetLength(2);
            var planeCount = AlphamapPlaneCount(layerCount);

            var planes = new byte[planeCount][];
            for (var planeIndex = 0; planeIndex < planeCount; planeIndex++)
                planes[planeIndex] = new byte[resolutionZ * resolutionX * AlphamapPlaneBytesPerPixel];

            for (var z = 0; z < resolutionZ; z++)
            for (var x = 0; x < resolutionX; x++)
            {
                // 非零画素は比率を保って合計1へ正規化し、丸め残差を最大重みへ戻す
                // Normalize each non-zero pixel to one while preserving ratios, then return rounding residue to the strongest layer
                var total = 0f;
                for (var layer = 0; layer < layerCount; layer++) total += alphamap[z, x, layer];
                if (total <= 0f) continue;

                var ratioScale = 1f / total;
                var quantizedTotal = 0;
                var strongestLayer = 0;
                var strongestWeight = float.MinValue;

                var pixelOffset = (z * resolutionX + x) * AlphamapPlaneBytesPerPixel;
                for (var layer = 0; layer < layerCount; layer++)
                {
                    var weight = Mathf.Clamp01(alphamap[z, x, layer] * ratioScale);
                    var quantizedWeight = Mathf.Clamp(Mathf.RoundToInt(weight * WeightQuantizeScale), 0, byte.MaxValue);
                    planes[layer / LayersPerAlphamapPlane][pixelOffset + layer % LayersPerAlphamapPlane] = (byte)quantizedWeight;
                    quantizedTotal += quantizedWeight;
                    if (strongestWeight < weight)
                    {
                        strongestWeight = weight;
                        strongestLayer = layer;
                    }
                }

                var strongestPlane = strongestLayer / LayersPerAlphamapPlane;
                var strongestOffset = pixelOffset + strongestLayer % LayersPerAlphamapPlane;
                planes[strongestPlane][strongestOffset] = (byte)(planes[strongestPlane][strongestOffset] + byte.MaxValue - quantizedTotal);
            }

            return planes;
        }
    }
}
