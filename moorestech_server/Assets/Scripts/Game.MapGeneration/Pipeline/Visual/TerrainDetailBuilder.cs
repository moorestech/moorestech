using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual
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

        // 密度は摂動前・傾斜は摂動後から採る移植元の使い分け（TerrainGenerator.cs:1147,1283）
        // Density reads the pre-tree heights and slopes the post-tree ones, as the source did (TerrainGenerator.cs:1147,1283)
        // 取り違えても例外は出ず、木の根元だけ草の生え方が変わる形で静かにずれる
        // Swapping them throws nothing and only shifts how grass grows around each tree
        public static List<int[,]> Build(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, BiomeVisualSections visualSections,
            float[,] preHeights, float[,] postHeights, bool[][,] winnerMasks, float[,,] alphamap,
            IReadOnlyList<LedgerPlacement> placements, Vector3 tileWorldPosition,
            int tileIndexX, int tileIndexZ)
        {
            var slopes = TerrainSlopeCalculator.Compute(postHeights, config);
            var dimensions = TerrainDimensions.From(
                config, config.shoreConfig.waterMargin, tileIndexX, tileIndexZ);

            // 距離場の点群はタイル境界の外まで要る。切り出しは全バイオームの最大探索半径で1回だけ行う
            // The distance fields need points from past the tile boundary; one slice at the largest search radius serves every biome
            BuildDistanceGrids(out var treeGrid, out var objectGrid);

            var maps = new List<int[,]>();

            for (var biomeIndex = 0; biomeIndex < biomeTypes.Length; biomeIndex++)
            {
                var detailConfig = visualSections.DetailConfigs[biomeIndex];
                if (detailConfig.entries.Length == 0) continue;

                // 勝者マスクは移植元と同じくローカル分類の重み由来。転送バイトと違いビーチ帯も勝者バイオーム側に残る
                // The winner mask derives from the locally classified weights as in the source; unlike the transferred bytes it keeps the beach band
                var mask = winnerMasks[biomeIndex];
                var detailRandom = new System.Random(config.seed + DetailSeedBase + biomeIndex * DetailSeedStridePerBiome);

                // 打ち切り半径はバイオームごとに違うので距離マップもバイオームごとに作る（移植元TerrainGenerator.cs:1274）
                // The cutoff radius differs per biome, so each biome gets its own distance map (source TerrainGenerator.cs:1274)
                maps.AddRange(DetailRuntimeGenerator.GenerateForBiome(
                    mask, preHeights, slopes, dimensions, detailConfig, detailRandom, alphamap,
                    GenerateDistanceMap(treeGrid, DetailDistanceRadius.ForTrees(detailConfig.entries)),
                    GenerateDistanceMap(objectGrid, DetailDistanceRadius.ForObjects(detailConfig.entries))));
            }

            return maps;

            #region Internal

            void BuildDistanceGrids(out SpatialGrid treeGrid, out SpatialGrid objectGrid)
            {
                treeGrid = null;
                objectGrid = null;

                var halo = DetailDistanceRadius.MaxOverConfigs(visualSections.DetailConfigs);

                // 距離フィルタが1つも無ければ距離場は誰も読まない。点群を組む意味がないので作らない
                // With no distance filter enabled nobody reads the fields, so the point sets are never built
                if (halo <= 0f) return;

                TilePlacementSlicer.SliceKindsWithHalo(
                    placements, tileWorldPosition, config.terrainWidth, config.terrainLength, halo,
                    out var trees, out var stones, out _);

                treeGrid = CreateGrid(trees);
                objectGrid = CreateGrid(stones);
            }

            // セルサイズは移植元と同じ。halo内の点はタイル外の座標を持つがSpatialGridが端セルへ寄せ、距離は真値で測られる
            // The cell size matches the source; halo points lie outside the tile and SpatialGrid folds them into the edge cells at true distance
            SpatialGrid CreateGrid(List<TileLocalPlacement> kindObjects)
            {
                var grid = new SpatialGrid(
                    config.terrainWidth, config.terrainLength, Mathf.Max(config.terrainWidth / 50f, 5f));
                foreach (var kindObject in kindObjects) grid.Add(kindObject.LocalPosition.x, kindObject.LocalPosition.z);

                return grid;
            }

            // 解像度はalphamapと同値。detail解像度と一致するのでDetailDensitySamplerがdetail座標のまま引ける
            // The resolution equals the alphamap's, which matches the detail resolution so DetailDensitySampler indexes it directly
            float[,] GenerateDistanceMap(SpatialGrid grid, float maxSearchRadius)
            {
                // 半径0はフィルタ自体が無効。誰も読まないのでnullを返す（移植元TerrainGenerator.cs:1276と同じ分岐）
                // A zero radius means the filter itself is off; nothing reads the map so null is returned (source TerrainGenerator.cs:1276)
                if (maxSearchRadius <= 0f) return null;

                // 点群が空でもSdfMapGeneratorのnullは素通ししない。距離フィルタが休んで木ゼロのタイルだけ草が生え放題になる
                // An empty point set must not pass SdfMapGenerator's null through; the filter would idle and flood tree-free tiles with grass
                if (grid.Count == 0) return CreateSaturatedDistanceMap(maxSearchRadius);

                return SdfMapGenerator.Generate(
                    grid, config.AlphamapResolution, config.terrainWidth, config.terrainLength, maxSearchRadius);
            }

            // 最寄りの点が探索半径の外にあるときSpatialGrid.FindMinDistanceが返す値で全画素を埋める。点群が空な状況の真値
            // Fills every pixel with what SpatialGrid.FindMinDistance returns when the nearest point lies past the search radius: the true value for an empty set
            float[,] CreateSaturatedDistanceMap(float maxSearchRadius)
            {
                var resolution = config.AlphamapResolution;
                var map = new float[resolution, resolution];
                for (var z = 0; z < resolution; z++)
                for (var x = 0; x < resolution; x++)
                    map[z, x] = maxSearchRadius;

                return map;
            }

            #endregion
        }
    }
}
