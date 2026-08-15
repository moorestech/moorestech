using System;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Client.Game.InGame.Environment.Terrain.Visual.Source;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Server.Protocol.PacketResponse.MapData;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Splat
{
    /// <summary>
    ///     転送済みの地形から splatmap を実行時生成する。分類済みの補助バッファは呼び出し側の context から借り、
    ///     サーバーと同じ SplatmapJob をそのまま流用する（このクラスは合成式を1行も持たない）
    ///     Builds the splatmap at runtime from the transferred terrain, borrowing the classified auxiliary buffers from the
    ///     caller's context and reusing the server's SplatmapJob verbatim
    /// </summary>
    public static class SplatmapRuntimeGenerator
    {
        private const int JobBatchSize = 64;

        public static float[,,] Generate(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, TerrainClassificationContext classification,
            SplatLayerTable layerTable, BiomeVisualSections visualSections,
            IReadOnlyDictionary<string, (string layerAddress, float weight, float width)> treeSurroundParamsByGuid,
            float[,] transferredHeights, byte[,] transferredBiomeIndices, int alphamapResolution,
            IReadOnlyList<MapObjectLayoutMessagePack> mapObjects, Vector3 tileWorldPosition)
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
                    $"[SplatmapRuntimeGenerator] The classification context holds {buffers.heights.Length} pixels for a {pixelCount} pixel tile.");

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
                    textureEntries = textureEntries,
                    splatWeights = splatWeights,
                }.Schedule(pixelCount, JobBatchSize).Complete();
            }

            // 岩周辺の裸地はSplatmapJobの外。合成後のalphamap上で上書きするのが移植元の順序
            // Bare ground around rocks sits outside SplatmapJob and overwrites the composed alphamap, as the source ordered it
            void PaintRockSurroundTexture(float[,,] alphamap)
            {
                // 遷移帯は隣タイルの岩からも伸びる。等倍で切り出すと裸地がタイル境界で直線に切れる
                // The transition band reaches in from neighbouring tiles' rocks; a plain slice would break it in a straight line at the seam
                var halo = ObjectSurroundTexturePainter.MaxReach(visualSections.SurroundTextureConfigs, mapObjects);
                var haloObjects = TileMapObjectSlicer.SliceWithHalo(
                    mapObjects, tileWorldPosition, config.terrainWidth, config.terrainLength, halo);
                MapObjectKindSplitter.Split(haloObjects, out _, out var stones);

                ObjectSurroundTexturePainter.Apply(
                    alphamap, config, layerTable, visualSections.SurroundTextureConfigs,
                    classification.Weights2D, biomeCount, transferredHeights, stones);
            }

            // 根元の塗りも隣タイルの木から伸びる。届く距離は樹種のsurroundLayerWidthで決まり、岩のMaxReachとは別物
            // A root patch reaches in from a neighbouring tile's trees too, as far as that species' surroundLayerWidth rather than the rocks' MaxReach
            void PaintTreeSurroundTexture(float[,,] alphamap)
            {
                var halo = TreeSurroundTexturePainter.MaxReach(treeSurroundParamsByGuid);
                var haloObjects = TileMapObjectSlicer.SliceWithHalo(
                    mapObjects, tileWorldPosition, config.terrainWidth, config.terrainLength, halo);
                MapObjectKindSplitter.Split(haloObjects, out var trees, out _);

                TreeSurroundTexturePainter.Apply(alphamap, config, layerTable, treeSurroundParamsByGuid, trees);
            }

            #endregion
        }
    }
}
