using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Stages
{
    // 配置済み樹木の周辺に生成ハイトマップをガウシアン摂動する（元 ApplyHeightModification）。
    // 摂動前の高さが転送の正本なのでサーバーは適用せず、クライアントが表示用の高さへ順適用する（R12）。
    // Perturbs the generated heightmap around placed trees (original ApplyHeightModification).
    // Pre-perturbation heights are the transferred source of truth, so the server never applies this; the client does, for display (R12).
    public static class TreeHeightModifier
    {
        // guid → (heightModAmount, heightModWidth)。どの entry が勝つかは BiomePlacementHelper が唯一決める。
        // Projects (heightModAmount, heightModWidth) out of the entries BiomePlacementHelper alone picks per guid.
        public static Dictionary<string, (float amount, float width)> BuildGuidModMap(
            BiomePlacementHelper helper, BiomeType[] biomeTypes)
        {
            var map = new Dictionary<string, (float, float)>();
            foreach (var pair in helper.BuildFirstTreePrototypeByGuid(biomeTypes))
                map[pair.Key] = (pair.Value.heightModAmount, pair.Value.heightModWidth);

            return map;
        }

        // 摂動が格子へ届く最大距離。隣タイルの木もここまで効くので、切り出しhaloがこれを下回ると境界の片側だけが持ち上がる。
        // Apply の丸めと同値の上界: 中心は RoundToInt で半画素まで格子側へ寄り、そこから radiusPixels 画素ぶん届く。
        // The farthest the perturbation reaches onto the lattice; a neighbouring tile's trees reach this far too, so a
        // smaller slice halo lifts only one side of the seam. It is Apply's own rounding bound: RoundToInt pulls the
        // centre up to half a pixel towards the lattice and the falloff then spans radiusPixels pixels from there.
        public static float MaxReach(
            TerrainGenerationConfig config, Dictionary<string, (float amount, float width)> guidModMap)
        {
            // 幅の最大は amount が効く樹種だけから採る。足切りを Apply と揃えないと、塗らない樹種のせいでhaloが広がる。
            // The widest width comes only from species whose amount lands, sharing Apply's cutoff so a no-op species cannot widen the halo.
            var maxModWidth = 0f;
            foreach (var mod in guidModMap.Values)
            {
                if (Mathf.Approximately(mod.amount, 0f)) continue;
                maxModWidth = Mathf.Max(maxModWidth, mod.width);
            }

            if (maxModWidth <= 0f) return 0f;

            // radiusPixels は Apply と同じく terrainWidth だけで割る。届く実距離は縦横の広いほうの画素間隔で決まる。
            // radiusPixels divides by terrainWidth exactly as Apply does; the reach in metres follows the coarser of the two pixel pitches.
            var radiusPixels = maxModWidth / config.terrainWidth * (config.Resolution - 1);
            var pixelPitch = Mathf.Max(
                config.terrainWidth / (config.Resolution - 1), config.terrainLength / (config.Resolution - 1));

            return (radiusPixels + 0.5f) * pixelPitch;
        }

        // 各配置木の guid から heightMod パラメータを引き、ガウシアンフォールオフで heights[] を加算する。
        // ガウシアン式は元実装と完全一致（sigma=radiusPixels/3, falloff=exp(-d^2/(2 sigma^2))）。
        // Look up heightMod params per placed tree by guid and add a Gaussian falloff to heights[].
        // The Gaussian math is verbatim from the original.
        // 解像度は config からだけ読む。res を引数で受けると MaxReach 側の config.Resolution と食い違い、
        // 別解像度で呼んだときに halo だけがずれる形になる
        // The resolution is read from config alone; taking it as an argument lets it diverge from MaxReach's
        // config.Resolution and skews the halo by itself when called at another resolution
        public static void Apply(
            float[] heights, TerrainGenerationConfig config,
            List<PlacementEntry> trees, Dictionary<string, (float amount, float width)> guidModMap)
        {
            if (trees == null || guidModMap.Count == 0) return;
            int res = config.Resolution;
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
