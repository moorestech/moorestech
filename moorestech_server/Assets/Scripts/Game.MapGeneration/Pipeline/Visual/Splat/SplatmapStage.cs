using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Visual;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Splat
{
    /// <summary>
    ///     転送済みの地形から splatmap を実行時生成する。分類済みの補助バッファは呼び出し側の context から借り、
    ///     サーバーと同じ SplatmapJob をそのまま流用する（このクラスは合成式を1行も持たない）
    ///     Builds the splatmap at runtime from the transferred terrain, borrowing the classified auxiliary buffers from the
    ///     caller's context and reusing the server's SplatmapJob verbatim
    /// </summary>
    public static class SplatmapStage
    {
        private const int JobBatchSize = 64;

        public static float[,,] Generate(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, TileClassificationContext classification,
            SplatLayerTable layerTable, BiomeVisualSections visualSections,
            TreeSurroundSpeciesTable treeSurroundSpecies,
            float[,] transferredHeights, byte[,] transferredBiomeIndices, int alphamapResolution,
            IReadOnlyList<LedgerPlacement> placements, Vector3 tileWorldPosition)
        {
            var biomeTextureConfigs = visualSections.TextureConfigs;
            var biomeMainLayerAddresses = visualSections.MainLayerAddresses;
            var resolution = config.Resolution;
            var pixelCount = resolution * resolution;
            var biomeCount = biomeTypes.Length;
            var buffers = classification.Buffers;

            // contextが別configで作られていると全画素が1列ずつ流れる。窓解像度が漏れた場合もここで止まる
            // A context built from another config would shift every pixel by a column; a leaked window resolution stops here too
            if (buffers.heights.Length != pixelCount)
                throw new InvalidOperationException(
                    $"[SplatmapStage] The classification context holds {buffers.heights.Length} pixels for a {pixelCount} pixel tile.");

            // レイヤー0本だとNativeArrayが空になりSplatmapJobが落ちる（移植元と同じく最低1本を確保する）
            // A zero-length layer array would crash SplatmapJob, so at least one is reserved as in the source
            var layerCount = Math.Max(layerTable.OrderedLayerAddresses.Count, 1);

            // splat専用の2本だけをここで確保する。分類チャネルはcontextの持ち物なので触らない
            // Only the two splat-specific arrays are allocated here; the classification channels belong to the context
            var splatWeights = new NativeArray<float>(pixelCount * layerCount, Allocator.TempJob);
            var textureEntries = default(NativeArray<TextureEntryParams>);

            try
            {
                OverwriteSplatmapLayerIndices();
                textureEntries = TextureEntryParamsBuilder.Build(
                    config.seed, biomeTextureConfigs, layerTable.LayerIndexByAddress, buffers.biomeParams, Allocator.TempJob);

                OverwriteWithTransferredTerrain();
                RunSplatmapJob();
                RunPlateauDebugOverlayJob();

                var alphamap = SplatWeightConverter.ToAlphamap(splatWeights, resolution, alphamapResolution, layerCount);

                // 移植元の順序は岩の裸地→木の根元。逆にすると根元の塗りの上から岩の裸地が乗り、木の下だけ色が変わる
                // The source paints the rocks' bare ground before the tree roots; reversing it lays bare ground over the roots and recolours only what sits under a tree
                PaintRockSurroundTexture(alphamap);
                PaintTreeSurroundTexture(alphamap);
                return alphamap;
            }
            finally
            {
                splatWeights.Dispose();
                if (textureEntries.IsCreated) textureEntries.Dispose();
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

            // 高さとwinnerは転送データが権威。重みは触らないのでcontextのWinnerMasksはこの上書きの影響を受けない
            // Heights and winner come from the authoritative transferred data; the weights stay untouched, so the context's WinnerMasks are unaffected
            void OverwriteWithTransferredTerrain()
            {
                for (var z = 0; z < resolution; z++)
                for (var x = 0; x < resolution; x++)
                    buffers.heights[z * resolution + x] = transferredHeights[z, x];

                WinnerBiomeIndexWriter.Overwrite(buffers.winnerBiomeIndex, transferredBiomeIndices, biomeTypes, resolution);
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
                    textureEntries = textureEntries,
                    splatWeights = splatWeights,
                }.Schedule(pixelCount, JobBatchSize).Complete();
            }

            // 受理された台地をデバッグ色で塗り、棄却候補をAlpineのベース色で塗る。移植元と同じくalphamap化の前に走る
            // Paints the accepted plateaus in their debug colours and the rejected candidates in Alpine's base colour, before the alphamap as in the source
            void RunPlateauDebugOverlayJob()
            {
                if (!PlateauDebugOverlayGate.IsEnabled(config)) return;

                // 列0本ではPlateauDebugOverlayJobが棄却側へ落ち、台地の全画素を全消ししてAlpineのベース色でベタ塗りする
                // With zero columns PlateauDebugOverlayJob falls into its rejected branch, wiping every plateau pixel onto Alpine's base colour
                if (layerTable.DebugLayerCount <= 0) return;

                // Alpineが無効な構成でもここへ来る。ベース色が引けなければ移植元と同じく0番を使う
                // A configuration without Alpine reaches here too; with no base colour to find it falls back to layer 0 as the source does
                var alpineBaseLayerIndex = 0;
                for (var biome = 0; biome < biomeCount; biome++)
                {
                    if (buffers.biomeParams[biome].biomeType != (int)BiomeType.Alpine) continue;
                    alpineBaseLayerIndex = buffers.biomeParams[biome].splatmapLayerIndex;
                    break;
                }

                new PlateauDebugOverlayJob
                {
                    resolution = resolution,
                    totalLayers = layerCount,
                    baseLayerIndex = alpineBaseLayerIndex,
                    debugLayerStart = layerTable.DebugLayerStart,
                    debugLayerCount = layerTable.DebugLayerCount,
                    fadeRadius = Mathf.Max(config.alpine.smoothRadius / 2, 3),
                    plateauMask = buffers.plateauMask,
                    regionLabels = buffers.regionLabels,
                    splatWeights = splatWeights,
                }.Schedule(pixelCount, JobBatchSize).Complete();
            }

            // 岩周辺の裸地はSplatmapJobの外。合成後のalphamap上で上書きするのが移植元の順序
            // Bare ground around rocks sits outside SplatmapJob and overwrites the composed alphamap, as the source ordered it
            void PaintRockSurroundTexture(float[,,] alphamap)
            {
                ObjectSurroundTexturePainter.Apply(
                    alphamap, config, layerTable, visualSections.SurroundTextureConfigs,
                    classification.Weights2D, biomeCount, transferredHeights, placements, tileWorldPosition);
            }

            // 根元の塗りも隣タイルの木から伸びる。届く距離は樹種のsurroundLayerWidthで決まり、岩の到達距離とは別物
            // A root patch reaches in from a neighbouring tile's trees too, as far as that species' surroundLayerWidth rather than the rocks' reach
            void PaintTreeSurroundTexture(float[,,] alphamap)
            {
                TreeSurroundTexturePainter.Apply(
                    alphamap, config, layerTable, treeSurroundSpecies, placements, tileWorldPosition);
            }

            #endregion
        }
    }
}
