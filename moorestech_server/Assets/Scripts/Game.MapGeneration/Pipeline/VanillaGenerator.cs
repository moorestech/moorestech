using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Spawn;
using Game.MapGeneration.Pipeline.Stages;
using Unity.Collections;
using UnityEngine;

namespace Game.MapGeneration.Pipeline
{
    // VanillaGenerator アルゴリズムの本体。ステージ(分類→高さ→木→オブジェクト→鉱脈)を順に呼ぶ。
    // The VanillaGenerator algorithm body: runs stages in order (classify, height, tree, object, ore).
    public static class VanillaGenerator
    {
        public static MapGenerationOutput Generate(TerrainGenerationConfig config)
        {
            var helper = new BiomePlacementHelper(config);
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            int biomeCount = biomeTypes.Length;
            int res = config.Resolution;
            int pixelCount = res * res;

            // ジョブ用 NativeArray を確保する（テクスチャ層は使わないため layerCount=1）。
            // Allocate the job NativeArrays (layerCount=1 since no texture layers are used).
            var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            var noiseOffsets = JobDataConverter.GenerateNoiseOffsets(config, biomeParams, biomeTypes, Allocator.TempJob);
            JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob, out var cont, out var ero);
            var buffers = JobDataConverter.AllocateBuffers(res, biomeCount, 1, Allocator.TempJob);
            buffers.noiseOffsets = noiseOffsets;
            buffers.biomeParams = biomeParams;

            try
            {
                // ステージ1-2: 分類→高さ生成
                // Stage 1-2: classification then height generation
                ClassificationStage.Run(config, biomeCount, buffers, cont, ero, protectEdgeSea: false);
                HeightmapStage.Run(config, biomeCount, buffers);

                var heights = new float[pixelCount];
                buffers.heights.CopyTo(heights);

                var output = new MapGenerationOutput
                {
                    Heights = heights,
                    Resolution = res,
                    BiomeIndices = PlacementInputBuilder.BuildBiomeIndices(
                        buffers.winnerBiomeIndex, buffers.landMask, buffers.beachFactor, biomeTypes, pixelCount),
                };
                output.SpawnPoint = ComputeSpawn(config, biomeTypes, heights, res);

                RunPlacement(config, helper, biomeTypes, buffers, heights, res, biomeCount, output);
                return output;
            }
            finally
            {
                buffers.Dispose();
                if (cont.IsCreated) cont.Dispose();
                if (ero.IsCreated) ero.Dispose();
            }
        }

        // ステージ3-6: 木・オブジェクト・鉱脈を配置し MapObjects / ItemVeins を確定する。
        // Stage 3-6: place trees, objects, and veins; finalize MapObjects and ItemVeins.
        static void RunPlacement(
            TerrainGenerationConfig config, BiomePlacementHelper helper, BiomeType[] biomeTypes,
            JobBuffers buffers, float[] heights, int res, int biomeCount, MapGenerationOutput output)
        {
            int totalCols = biomeCount + 2;
            var weights2D = PlacementInputBuilder.BuildPlacementWeights(
                buffers.biomeWeights, buffers.shoreMask, buffers.beachFactor, res, biomeCount, totalCols);
            var masks = BiomeMaskBuilder.BuildAllWinnerMasks(weights2D, res, biomeCount);
            var heights2D = PlacementInputBuilder.ConvertHeights(heights, res);

            var treeEntries = new List<PlacementEntry>();
            TreePlacementStage.Generate(config, helper, biomeTypes, masks, heights, treeEntries);

            var objectEntries = new List<PlacementEntry>();
            List<ObjectPlacementResult> objectPlacements = null;
            if (config.generateObject)
                ObjectPlacementStage.Generate(config, helper, biomeTypes, masks, heights, heights2D,
                    treeEntries, out objectEntries, out objectPlacements);

            output.ItemVeins = OrePlacementStage.Generate(
                config, masks, biomeTypes, heights2D, treeEntries, objectPlacements);

            // 全配置確定後に木周辺の生成ハイトマップを摂動する（output.Heights と同一配列を書き換える）。
            // オブジェクト/鉱脈は摂動前の高さで配置済みのため、元パイプラインと同じく最終ハイトマップにのみ効く。
            // Perturb the generated heightmap around trees after all placement (mutates the same array as
            // output.Heights). Objects/veins were placed on pre-perturbation heights, matching the reference order.
            var heightModMap = TreeHeightModifier.BuildGuidModMap(helper, biomeTypes);
            TreeHeightModifier.Apply(heights, res, config, treeEntries, heightModMap);

            AppendMapObjects(output.MapObjects, treeEntries);
            AppendMapObjects(output.MapObjects, objectEntries);
        }

        static void AppendMapObjects(List<PlacedMapObject> target, List<PlacementEntry> entries)
        {
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.MapObjectGuid)) continue;
                target.Add(new PlacedMapObject { MapObjectGuid = e.MapObjectGuid, Position = e.WorldPosition });
            }
        }

        // スポーン地点を算出する。オフセット探索有効時は SpawnRegionFinder、無効時は config 指定値を使う。
        // Compute the spawn point via SpawnRegionFinder when offset search is on, else the config value.
        static Vector3 ComputeSpawn(TerrainGenerationConfig config, BiomeType[] biomeTypes, float[] heights, int res)
        {
            Vector2 spawn = config.spawnWorldPosition;
            if (config.useSpawnOffsetSearch)
            {
                var result = SpawnRegionFinder.Find(config, biomeTypes);
                if (result.Success) spawn = result.SpawnWorldPosition;
            }

            int px = Mathf.Clamp(Mathf.RoundToInt((spawn.x - config.worldOffsetX) / config.terrainWidth * (res - 1)), 0, res - 1);
            int pz = Mathf.Clamp(Mathf.RoundToInt((spawn.y - config.worldOffsetZ) / config.terrainLength * (res - 1)), 0, res - 1);
            float heightMeters = heights[pz * res + px] * config.terrainHeight;
            return new Vector3(spawn.x, heightMeters, spawn.y);
        }
    }
}
