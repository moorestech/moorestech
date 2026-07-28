using System;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Client.Game.InGame.Environment.Terrain.Visual.Splat
{
    /// <summary>
    ///     転送済みの地形から splatmap を実行時生成する。分類段を再実行して SplatmapJob の補助バッファを揃え、
    ///     サーバーと同じ SplatmapJob をそのまま流用する（このクラスは合成式を1行も持たない）
    ///     Builds the splatmap at runtime from the transferred terrain by re-running the classification stage to
    ///     supply SplatmapJob's auxiliary buffers, then reusing the server's SplatmapJob verbatim
    /// </summary>
    public static class SplatmapRuntimeGenerator
    {
        // 本番の生成経路と同じ値。窓・粗グリッド探索専用の true とは別物
        // Matches the production generation path; true is reserved for the window and coarse-grid searches
        private const bool ProtectEdgeSea = false;

        private const int JobBatchSize = 64;

        public static float[,,] Generate(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, SplatLayerTable layerTable,
            BiomeTextureConfig[] biomeTextureConfigs, string[] biomeMainLayerAddresses,
            float[,] transferredHeights, byte[,] transferredBiomeIndices, int alphamapResolution)
        {
            var resolution = config.Resolution;
            var pixelCount = resolution * resolution;
            var biomeCount = biomeTypes.Length;

            // レイヤー0本だとNativeArrayが空になりSplatmapJobが落ちる（移植元と同じく最低1本を確保する）
            // A zero-length layer array would crash SplatmapJob, so at least one is reserved as in the source
            var layerCount = Math.Max(layerTable.OrderedLayerAddresses.Count, 1);

            // 確保した端からbuffersへ預ける。整備漏れの例外が途中で出ても、常にbuffers.Disposeが全部を解放できる状態を保つ
            // Each allocation is handed to buffers immediately so a mid-way data-gap exception still leaves everything for buffers.Dispose
            var buffers = JobDataConverter.AllocateBuffers(resolution, biomeCount, layerCount, Allocator.TempJob);
            var continentalnessOffsets = default(NativeArray<float2>);
            var erosionOffsets = default(NativeArray<float2>);

            try
            {
                buffers.biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
                OverwriteSplatmapLayerIndices();

                buffers.textureEntries = TextureEntryParamsBuilder.Build(
                    config.seed, biomeTextureConfigs, layerTable.LayerIndexByAddress, buffers.biomeParams, Allocator.TempJob);
                buffers.noiseOffsets = JobDataConverter.GenerateNoiseOffsets(config, buffers.biomeParams, biomeTypes, Allocator.TempJob);
                JobDataConverter.GenerateClassificationOffsets(config, Allocator.TempJob, out continentalnessOffsets, out erosionOffsets);

                // 分類段を再実行して海陸マスク・ビーチ遷移・バイオーム重みを揃える。転送されていないのはこの6本だけ
                // Re-run classification to supply the land/sea masks, beach transition, and biome weights: the only six buffers never transferred
                ClassificationStage.Run(config, biomeCount, buffers, continentalnessOffsets, erosionOffsets, ProtectEdgeSea);

                OverwriteWithTransferredTerrain();
                RunSplatmapJob();

                return SplatWeightConverter.ToAlphamap(buffers.splatWeights, resolution, alphamapResolution, layerCount);
            }
            finally
            {
                buffers.Dispose();
                if (continentalnessOffsets.IsCreated) continentalnessOffsets.Dispose();
                if (erosionOffsets.IsCreated) erosionOffsets.Dispose();
            }

            #region Internal

            // BiomePlacementHelper.GetSplatmapLayerIndex のハードコード値(1-8)は有効バイオーム数で実配列とずれる
            // BiomePlacementHelper.GetSplatmapLayerIndex's hardcoded 1-8 drifts from the real array as the enabled biome count changes
            void OverwriteSplatmapLayerIndices()
            {
                for (var biome = 0; biome < biomeTypes.Length; biome++)
                {
                    var parameters = buffers.biomeParams[biome];
                    parameters.splatmapLayerIndex = layerTable.LayerIndexByAddress[biomeMainLayerAddresses[biome]];
                    buffers.biomeParams[biome] = parameters;
                }
            }

            // 高さとwinnerは転送データが権威。再計算した分類結果は補助floatバッファとしてのみ使う
            // Heights and winner come from the authoritative transferred data; the recomputed classification only feeds the auxiliary float buffers
            void OverwriteWithTransferredTerrain()
            {
                for (var z = 0; z < resolution; z++)
                for (var x = 0; x < resolution; x++)
                    buffers.heights[z * resolution + x] = transferredHeights[z, x];

                TransferredWinnerBiomeWriter.Overwrite(buffers.winnerBiomeIndex, transferredBiomeIndices, biomeTypes, resolution);
            }

            void RunSplatmapJob()
            {
                new SplatmapJob
                {
                    resolution = resolution,
                    biomeCount = biomeCount,
                    totalLayers = layerCount,
                    terrainWidth = config.terrainWidth,
                    terrainHeight = config.terrainHeight,
                    terrainLength = config.terrainLength,
                    worldOffsetX = config.worldOffsetX,
                    worldOffsetZ = config.worldOffsetZ,
                    textureBlendStrength = config.boundaryConfig.textureBlendStrength,
                    heights = buffers.heights,
                    shoreMask = buffers.shoreMask,
                    landMask = buffers.landMask,
                    beachFactor = buffers.beachFactor,
                    landTextureFactor = buffers.landTextureFactor,
                    seaTextureFactor = buffers.seaTextureFactor,
                    biomeWeights = buffers.biomeWeights,
                    winnerBiomeIndex = buffers.winnerBiomeIndex,
                    biomeParams = buffers.biomeParams,
                    noiseOffsets = buffers.noiseOffsets,
                    textureEntries = buffers.textureEntries,
                    splatWeights = buffers.splatWeights,
                }.Schedule(pixelCount, JobBatchSize).Complete();
            }

            #endregion
        }
    }
}
