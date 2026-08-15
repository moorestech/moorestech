using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Client.Game.InGame.Environment.Terrain.Visual;
using Client.Game.InGame.Environment.Terrain.Visual.Detail.Distance;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Server.Protocol.PacketResponse.MapData;
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
            float[,] preHeights, float[,] postHeights, bool[][,] winnerMasks, float[,,] alphamap,
            TerrainLayer[] terrainLayers,
            IReadOnlyList<MapObjectLayoutMessagePack> mapObjects, Vector3 tileWorldPosition)
        {
            var slopes = TerrainSlopeCalculator.Compute(postHeights, config);
            var dimensions = TerrainDimensions.From(config, config.shoreConfig.waterMargin);

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
                    mask, preHeights, slopes, dimensions, detailConfig, detailRandom, alphamap, terrainLayers,
                    GenerateDistanceMap(treeGrid, DetailDistanceRadius.ForTrees(detailConfig.entries)),
                    GenerateDistanceMap(objectGrid, DetailDistanceRadius.ForObjects(detailConfig.entries))));
            }

            return maps;

            #region Internal

            void BuildDistanceGrids(out SpatialGrid treeGrid, out SpatialGrid objectGrid)
            {
                treeGrid = null;
                objectGrid = null;

                var halo = 0f;
                foreach (var detailConfig in visualSections.DetailConfigs)
                    halo = Mathf.Max(halo, Mathf.Max(
                        DetailDistanceRadius.ForTrees(detailConfig.entries),
                        DetailDistanceRadius.ForObjects(detailConfig.entries)));

                // 距離フィルタが1つも無ければ距離場は誰も読まない。点群を組む意味がないので作らない
                // With no distance filter enabled nobody reads the fields, so the point sets are never built
                if (halo <= 0f) return;

                var haloObjects = TileMapObjectSlicer.SliceWithHalo(
                    mapObjects, tileWorldPosition, config.terrainWidth, config.terrainLength, halo);
                MapObjectPointSplitter.Split(haloObjects, out var treePoints, out var objectPoints);

                treeGrid = CreateGrid(treePoints);
                objectGrid = CreateGrid(objectPoints);
            }

            // セルサイズは移植元と同じ。halo内の点はタイル外の座標を持つがSpatialGridが端セルへ寄せ、距離は真値で測られる
            // The cell size matches the source; halo points lie outside the tile and SpatialGrid folds them into the edge cells at true distance
            SpatialGrid CreateGrid(List<Vector2> points)
            {
                var grid = new SpatialGrid(
                    config.terrainWidth, config.terrainLength, Mathf.Max(config.terrainWidth / 50f, 5f));
                foreach (var point in points) grid.Add(point.x, point.y);

                return grid;
            }

            // 解像度はalphamapと同値。detail解像度と一致するのでDetailDensitySamplerがdetail座標のまま引ける
            // The resolution equals the alphamap's, which matches the detail resolution so DetailDensitySampler indexes it directly
            float[,] GenerateDistanceMap(SpatialGrid grid, float maxSearchRadius)
            {
                if (maxSearchRadius <= 0f) return null;

                return SdfMapGenerator.Generate(
                    grid, config.AlphamapResolution, config.terrainWidth, config.terrainLength, maxSearchRadius);
            }

            #endregion
        }
    }
}
