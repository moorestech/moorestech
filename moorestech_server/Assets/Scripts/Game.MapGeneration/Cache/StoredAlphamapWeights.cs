using UnityEngine;
using static Game.MapGeneration.Cache.TerrainVisualCacheFormat;

namespace Game.MapGeneration.Cache
{
    /// <summary>
    ///     保存と適用へ回すalphamapを、キャッシュ往復で1画素も動かない値へ畳む。
    ///     裸地の塗りは合計が1を超える画素を作る（移植元から引き継いだ性質・SurroundBlendWriter参照）が、
    ///     キャッシュは1画素1バイトなので、そのまま焼くと再読込時だけ頭打ちして比率が変わる。
    ///     合計が1を超える画素だけ比率を保ったまま合計1へ寄せ、全画素を1/255刻みへ量子化することで、
    ///     初回生成の配列とキャッシュから読み戻した配列を完全に一致させる。
    ///     Terrainへ渡る重みはUnityが正規化して8bitへ焼くため、この畳み込みで見た目は変わらない。
    ///     Folds the alphamap bound for storage and application into values a cache round trip cannot move.
    ///     Bare-ground painting produces pixels summing above one (a property inherited from the source; see
    ///     SurroundBlendWriter), but the cache holds one byte per pixel, so baking those as-is clips only on
    ///     reload and shifts the ratio. Pixels above one are scaled back to a sum of one with their ratio intact
    ///     and every pixel is quantized to 1/255 steps, making a freshly built array and a cache-loaded one identical.
    ///     Unity normalizes the weights it receives and bakes them to 8 bits, so this fold leaves the look unchanged.
    /// </summary>
    public static class StoredAlphamapWeights
    {
        public static void Fold(float[,,] alphamap)
        {
            var resolutionZ = alphamap.GetLength(0);
            var resolutionX = alphamap.GetLength(1);
            var layerCount = alphamap.GetLength(2);

            for (var z = 0; z < resolutionZ; z++)
            for (var x = 0; x < resolutionX; x++)
            {
                // 合計1以下の画素はバイト表現に収まる。触らずに量子化だけ通し、移植元の重みをそのまま残す
                // A pixel summing to one or less already fits a byte; it is only quantized, keeping the source weights
                var total = 0f;
                for (var layer = 0; layer < layerCount; layer++) total += alphamap[z, x, layer];
                var ratioScale = 1f < total ? 1f / total : 1f;

                for (var layer = 0; layer < layerCount; layer++)
                {
                    var weight = Mathf.Clamp01(alphamap[z, x, layer] * ratioScale);
                    alphamap[z, x, layer] = Mathf.RoundToInt(weight * WeightQuantizeScale) / WeightQuantizeScale;
                }
            }
        }
    }
}
