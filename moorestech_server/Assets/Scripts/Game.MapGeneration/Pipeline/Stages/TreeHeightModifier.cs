using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 配置済み樹木の周辺に生成ハイトマップをガウシアン摂動する（元 ApplyHeightModification）。
    // 出力 heights[] を直接書き換えるゲームプレイ対象処理で、全配置(木＋岩周辺木)確定後に1回適用する。
    // Perturbs the generated heightmap around placed trees (original ApplyHeightModification).
    // A gameplay-affecting pass that writes heights[] directly, applied once after all tree placement.
    internal static class TreeHeightModifier
    {
        // guid → (heightModAmount, heightModWidth)。有効バイオーム順・エントリ順・guid順で最初の出現が勝つ。
        // これは元の prefab 展開＋prefabToProtoIndex（最初のインデックス）による属性付けと一致する。
        // guid to (heightModAmount, heightModWidth); first occurrence wins in enabled-biome/entry/guid order,
        // matching the original prefab-expansion plus prefabToProtoIndex (first index) attribution.
        public static Dictionary<string, (float amount, float width)> BuildGuidModMap(
            BiomePlacementHelper helper, BiomeType[] biomeTypes)
        {
            var map = new Dictionary<string, (float, float)>();
            foreach (var biome in biomeTypes)
            {
                var tp = helper.GetTreePlacementConfig(biome);
                if (tp?.prototypes == null) continue;
                foreach (var entry in tp.prototypes)
                {
                    if (entry == null || entry.disabled || entry.mapObjectGuids == null) continue;
                    foreach (var guid in entry.mapObjectGuids)
                    {
                        if (string.IsNullOrEmpty(guid) || map.ContainsKey(guid)) continue;
                        map[guid] = (entry.heightModAmount, entry.heightModWidth);
                    }
                }
            }
            return map;
        }

        // 各配置木の guid から heightMod パラメータを引き、ガウシアンフォールオフで heights[] を加算する。
        // ガウシアン式は元実装と完全一致（sigma=radiusPixels/3, falloff=exp(-d^2/(2 sigma^2))）。
        // Look up heightMod params per placed tree by guid and add a Gaussian falloff to heights[].
        // The Gaussian math is verbatim from the original.
        public static void Apply(
            float[] heights, int res, TerrainGenerationConfig config,
            List<PlacementEntry> trees, Dictionary<string, (float amount, float width)> guidModMap)
        {
            if (trees == null || guidModMap.Count == 0) return;
            float terrainWidth = config.terrainWidth;
            float terrainLength = config.terrainLength;
            float terrainHeight = config.terrainHeight;

            foreach (var tree in trees)
            {
                if (string.IsNullOrEmpty(tree.MapObjectGuid)) continue;
                if (!guidModMap.TryGetValue(tree.MapObjectGuid, out var mod)) continue;

                float modAmount = mod.amount;
                float modWidth = mod.width;
                if (Mathf.Approximately(modAmount, 0f)) continue;

                float radiusPixels = modWidth / terrainWidth * (res - 1);
                int radiusInt = Mathf.CeilToInt(radiusPixels);
                float modNorm = modAmount / terrainHeight;

                // 元 ConvertToTreeInstances と同様に WorldPosition を寸法で正規化して格子座標へ写像する。
                // Map to grid coords by normalizing WorldPosition by size, as in the original ConvertToTreeInstances.
                int cx = Mathf.RoundToInt(tree.WorldPosition.x / terrainWidth * (res - 1));
                int cz = Mathf.RoundToInt(tree.WorldPosition.z / terrainLength * (res - 1));

                for (int dz = -radiusInt; dz <= radiusInt; dz++)
                for (int dx = -radiusInt; dx <= radiusInt; dx++)
                {
                    int px = cx + dx;
                    int pz = cz + dz;
                    if (px < 0 || res <= px || pz < 0 || res <= pz) continue;

                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (radiusPixels < dist) continue;

                    float sigma = radiusPixels / 3f;
                    float falloff = Mathf.Exp(-(dist * dist) / (2f * sigma * sigma));
                    heights[pz * res + px] += modNorm * falloff;
                }
            }
        }
    }
}
