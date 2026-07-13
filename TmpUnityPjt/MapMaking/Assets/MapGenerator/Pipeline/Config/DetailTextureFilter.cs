using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// スプラットマップ連動フィルタ。特定テレインレイヤー上にのみ Detail を配置する。
    /// MicroVerse の FilterSet.TextureFilter に相当。
    /// </summary>
    [System.Serializable]
    public class DetailTextureFilter
    {
        public bool enabled;

        // textureFilters に含まれないレイヤーでの配置重み（0=配置しない、1=通常通り）
        [Range(0f, 1f)] public float otherTextureWeight = 1f;

        public TextureFilterEntry[] entries = new TextureFilterEntry[0];

        [System.Serializable]
        public class TextureFilterEntry
        {
            public TerrainLayer layer;
            // 正で促進、負で抑制。草レイヤー上に草Detailを集中させる等
            [Range(-1f, 1f)] public float weight = 1f;
        }

        /// <summary>
        /// 指定座標での配置重みを返す。splatmap の各レイヤー重みを参照して計算。
        /// </summary>
        public float Evaluate(float[,,] splatmap, int z, int x, TerrainLayer[] terrainLayers)
        {
            if (!enabled || entries == null || entries.Length == 0) return 1f;

            int layerCount = splatmap.GetLength(2);
            float result = 0f;

            for (int i = 0; i < layerCount; i++)
            {
                float splatWeight = splatmap[z, x, i];
                if (splatWeight < 0.01f) continue;

                // このレイヤーに対応するフィルタエントリを探す
                float layerFactor = otherTextureWeight;
                for (int e = 0; e < entries.Length; e++)
                {
                    if (entries[e].layer != null && i < terrainLayers.Length
                        && ReferenceEquals(entries[e].layer, terrainLayers[i]))
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
