using System;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Detail.Filter
{
    /// <summary>
    ///     スプラットマップ連動フィルタ。MapMaking DetailTextureFilter の移植で、特定レイヤー上へDetailを寄せる
    ///     Splatmap-linked filter ported from MapMaking's DetailTextureFilter, concentrating details on chosen layers
    /// </summary>
    public class DetailTextureFilter
    {
        private const float MinimumContributingSplatWeight = 0.01f;

        /// <summary>
        ///     1レイヤー分の配置重み。正で促進、負で抑制する
        ///     Placement weight for one layer; positive promotes and negative suppresses
        /// </summary>
        public class TextureFilterEntry
        {
            // どのレイヤーを指すかはこのアドレスだけが知っている。落とすとlayerIndexを解決する手がかりが消える
            // Only this address knows which layer is meant; dropping it removes every clue for resolving layerIndex
            public string layerAddressablePath;
            public float weight;

            // alphamapの列。SplatLayerTableが確定したあと呼び出し側が差し込む。-1は未解決
            // The alphamap column, injected by the caller once SplatLayerTable settles; -1 means unresolved
            public int layerIndex = -1;

            public void SetLayerIndex(int resolvedLayerIndex)
            {
                layerIndex = resolvedLayerIndex;
            }
        }

        public bool enabled;

        // entriesに載っていないレイヤー上での配置重み。0で配置しない、1で通常どおり
        // Placement weight on layers absent from entries: 0 places nothing, 1 places normally
        public float otherTextureWeight;

        public TextureFilterEntry[] entries;

        // Evaluateの早期脱出条件と揃える。無効フィルタのエントリは出力に一切影響しないため未解決でも落とさない
        // Mirrors Evaluate's early exit: a disabled filter's entries never affect the output, so an unresolved layer there is not fatal
        public void ThrowIfUnresolved()
        {
            if (!enabled || entries == null || entries.Length == 0) return;

            foreach (var entry in entries)
                if (entry.layerIndex < 0)
                    throw new InvalidOperationException(
                        $"Detail texture filter layer '{entry.layerAddressablePath}' has no alphamap column.");
        }

        public float Evaluate(float[,,] splatmap, int z, int x)
        {
            if (!enabled || entries == null || entries.Length == 0) return 1f;

            var layerCount = splatmap.GetLength(2);
            var result = 0f;

            for (var i = 0; i < layerCount; i++)
            {
                var splatWeight = splatmap[z, x, i];
                if (splatWeight < MinimumContributingSplatWeight) continue;

                // このレイヤーに対応するエントリを探し、無ければotherTextureWeightを使う
                // Look up the entry matching this layer, falling back to otherTextureWeight when absent
                var layerFactor = otherTextureWeight;
                for (var e = 0; e < entries.Length; e++)
                {
                    if (entries[e].layerIndex == i)
                    {
                        layerFactor = entries[e].weight;
                        break;
                    }
                }

                result += splatWeight * Mathf.Max(0f, layerFactor);
            }

            return Mathf.Clamp01(result);
        }
    }
}
