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
    public class VanillaGenerator : IMapGenerator
    {
        public MapGenerationOutput Generate(TerrainGenerationConfig config)
        {
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);

            // G はノイズのサンプル座標に効くため、全ステージより前にスポーン探索を確定させる。
            // The spawn search must settle before every stage since G feeds the noise sample coordinates.
            Vector2 spawnOffset = ResolveSpawnOffset(config, biomeTypes);

            var helper = new BiomePlacementHelper(config);
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
                output.SpawnPoint = ComputeSpawn(config, heights, res, spawnOffset);

                RunPlacement(config, helper, biomeTypes, buffers, heights, res, biomeCount, output, spawnOffset);
                return output;
            }
            finally
            {
                buffers.Dispose();
                if (cont.IsCreated) cont.Dispose();
                if (ero.IsCreated) ero.Dispose();
            }
        }

        // ステージ3-6: 木・オブジェクト・鉱脈を配置し MapObjects / ItemVeins / FluidVeins を確定する。
        // Stage 3-6: place trees, objects, and veins; finalize MapObjects, ItemVeins, and FluidVeins.
        static void RunPlacement(
            TerrainGenerationConfig config, BiomePlacementHelper helper, BiomeType[] biomeTypes,
            JobBuffers buffers, float[] heights, int res, int biomeCount, MapGenerationOutput output,
            Vector2 spawnOffset)
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
            output.FluidVeins = FluidVeinPlacementStage.Generate(
                config, masks, biomeTypes, heights2D, treeEntries, objectPlacements);

            // 全配置確定後に木周辺の生成ハイトマップを摂動する（output.Heights と同一配列を書き換える）。
            // オブジェクト/鉱脈は摂動前の高さで配置済みのため、元パイプラインと同じく最終ハイトマップにのみ効く。
            // Perturb the generated heightmap around trees after all placement (mutates the same array as
            // output.Heights). Objects/veins were placed on pre-perturbation heights, matching the reference order.
            var heightModMap = TreeHeightModifier.BuildGuidModMap(helper, biomeTypes);
            TreeHeightModifier.Apply(heights, res, config, treeEntries, heightModMap);

            // オブジェクト/鉱脈はノイズ座標で算出されるため -G でシーン座標へ揃える（木は既にタイルローカル）。
            // Objects/veins are computed in noise space, so -G realigns them to scene space (trees already are).
            PlacementSceneOffset.ShiftEntries(objectEntries, spawnOffset);
            PlacementSceneOffset.ShiftVeins(output.ItemVeins, spawnOffset);
            PlacementSceneOffset.ShiftVeins(output.FluidVeins, spawnOffset);

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

        // スポーン探索を実行し、中央化オフセット G を config のノイズ座標へ反映して返す。
        // Run the spawn search, push the centering offset G into the config's noise coordinates, and return it.
        static Vector2 ResolveSpawnOffset(TerrainGenerationConfig config, BiomeType[] biomeTypes)
        {
            if (!config.useSpawnOffsetSearch) return Vector2.zero;

            var result = SpawnRegionFinder.Find(config, biomeTypes);

            // 成功/失敗いずれも offset と spawn を必ず同期させる（片方だけ残ると鉱脈帯とスポーンがズレる）。
            // Always sync offset and spawn for both outcomes; a stale pair skews the vein bands against the spawn.
            config.spawnWorldPosition = result.SpawnWorldPosition;
            config.worldOffsetX += result.WorldOffset.x;
            config.worldOffsetZ += result.WorldOffset.y;
            return result.WorldOffset;
        }

        // スポーン地点をシーン座標で返す。config.spawnWorldPosition はノイズ座標 S なので高さ採取後に -G する。
        // Return the spawn point in scene space; config.spawnWorldPosition is the noise-space S, so subtract G after sampling.
        static Vector3 ComputeSpawn(TerrainGenerationConfig config, float[] heights, int res, Vector2 spawnOffset)
        {
            Vector2 spawn = config.spawnWorldPosition;
            int px = Mathf.Clamp(Mathf.RoundToInt((spawn.x - config.worldOffsetX) / config.terrainWidth * (res - 1)), 0, res - 1);
            int pz = Mathf.Clamp(Mathf.RoundToInt((spawn.y - config.worldOffsetZ) / config.terrainLength * (res - 1)), 0, res - 1);
            float heightMeters = heights[pz * res + px] * config.terrainHeight;
            return new Vector3(spawn.x - spawnOffset.x, heightMeters, spawn.y - spawnOffset.y);
        }
    }
}
