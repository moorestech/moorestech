using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Visual;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Build
{
    /// <summary>
    ///     有効バイオームを順に回してDetailの密度マップを積み上げる。MapMaking TerrainGenerator の
    ///     Stage 5 の移植で、バイオームごとの入力整形だけを担い密度計算はDetailRuntimeGeneratorに委ねる
    ///     Walks the enabled biomes accumulating detail density maps; ported from MapMaking's
    ///     TerrainGenerator Stage 5, shaping per-biome inputs while DetailRuntimeGenerator owns the density math
    /// </summary>
    public static class TerrainDetailBuilder
    {
        // 移植元と同じseed導出。バイオームごとに100ずつずらして分布を独立させる
        // The source's seed derivation: 100 per biome so their distributions stay independent
        private const int DetailSeedBase = 6000;
        private const int DetailSeedStridePerBiome = 100;

        // 密度は摂動前・傾斜は摂動後から採る移植元の使い分け（TerrainGenerator.cs:1147,1283）をそのまま持ち込む。
        // 取り違えても例外は出ず、木の根元だけ草の生え方が変わる形で静かにずれる
        // Keeps the source's split of pre-tree heights for density and post-tree ones for slopes (TerrainGenerator.cs:1147,1283);
        // swapping them throws nothing and only shifts how grass grows around each tree
        public static List<int[,]> Build(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, BiomeVisualSections visualSections,
            float[,] preHeights, float[,] postHeights, byte[,] transferredBiomeIndices, float[,,] alphamap,
            TerrainLayer[] terrainLayers)
        {
            var slopes = TerrainSlopeCalculator.Compute(postHeights, config);
            var dimensions = TerrainDimensions.From(config, config.shoreConfig.waterMargin);

            var maps = new List<int[,]>();

            for (var biomeIndex = 0; biomeIndex < biomeTypes.Length; biomeIndex++)
            {
                var detailConfig = visualSections.DetailConfigs[biomeIndex];
                if (detailConfig.entries.Length == 0) continue;

                var mask = TransferredBiomeMaskBuilder.Build(transferredBiomeIndices, biomeTypes[biomeIndex], config.Resolution);
                var detailRandom = new System.Random(config.seed + DetailSeedBase + biomeIndex * DetailSeedStridePerBiome);

                // 木・オブジェクトの距離場はクライアントに配置情報が無いため渡さない。距離フィルタだけが休む
                // The tree and object distance fields are absent client-side, so only the distance filters idle
                maps.AddRange(DetailRuntimeGenerator.GenerateForBiome(
                    mask, preHeights, slopes, dimensions, detailConfig, detailRandom,
                    alphamap, terrainLayers, null, null));
            }

            return maps;
        }
    }
}
