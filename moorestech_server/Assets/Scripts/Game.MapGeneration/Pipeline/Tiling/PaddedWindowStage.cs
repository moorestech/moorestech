using System;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using Unity.Collections;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Tiling
{
    /// <summary>
    ///     タイル1枚を四方へ広げたパディング窓で分類＋高さ生成し、中央を切り出して destination へ書き戻す。
    ///     窓端でカーネルを打ち切った値を捨てることが目的で、これがタイル境界のシーム解消の中核になる。
    ///     destination に書かれるのは heights・shoreMask・landMask・beachFactor・landTextureFactor・
    ///     seaTextureFactor・biomeWeights・winnerBiomeIndex・plateauMask・regionLabels の10本で、
    ///     残りはステージ内部の作業領域として扱う。
    ///     Generates one tile's classification and heights on a window padded on all four sides, then crops the
    ///     center back into destination. Discarding the window-truncated values is what removes the tile seams.
    ///     The ten channels written into destination are heights, shoreMask, landMask, beachFactor,
    ///     landTextureFactor, seaTextureFactor, biomeWeights, winnerBiomeIndex, plateauMask, and regionLabels;
    ///     the rest stay stage scratch.
    /// </summary>
    public static class PaddedWindowStage
    {
        // destination は呼び出し側が確保・破棄する。biomeParams と noiseOffsets も充填済みであること
        // The caller allocates and disposes destination, with biomeParams and noiseOffsets already filled
        public static void Run(TerrainGenerationConfig tileConfig, BiomeType[] biomeTypes, JobBuffers destination)
        {
            var baseResolution = tileConfig.Resolution;
            var biomeCount = biomeTypes.Length;
            if (destination.heights.Length != baseResolution * baseResolution)
                throw new ArgumentException(
                    $"destination must be allocated at the tile resolution {baseResolution}.", nameof(destination));

            // biomeParams が空だと ResolvePadding が高さ側の到達を0と読み、padding だけ無言で痩せてシームが戻る
            // An empty biomeParams makes ResolvePadding read the height reach as zero, silently shrinking the padding and bringing the seam back
            if (destination.biomeParams.Length == 0)
                throw new ArgumentException(
                    "destination.biomeParams must be filled before Run; the padding derivation reads it.", nameof(destination));

            var padding = ResolvePadding(tileConfig, destination.biomeParams);
            if (padding <= 0)
            {
                RunWindow(tileConfig, biomeCount, destination);
                return;
            }

            var windowConfig = BuildWindowConfig(tileConfig, baseResolution, padding);
            var window = JobDataConverter.AllocateBuffers(windowConfig.Resolution, biomeCount, 1, Allocator.TempJob);

            // biomeParams と noiseOffsets は解像度非依存なので窓と共有する。破棄責任は呼び出し側に残る
            // biomeParams and noiseOffsets are resolution-independent, so the window shares them; disposal stays with the caller
            window.biomeParams = destination.biomeParams;
            window.noiseOffsets = destination.noiseOffsets;
            try
            {
                RunWindow(windowConfig, biomeCount, window);
                CropWindow(window, destination, baseResolution, padding, biomeCount);
            }
            finally
            {
                // 共有した2本を切り離してから破棄する。付けたままだと呼び出し側の破棄と二重解放になる
                // Detach the two shared arrays before disposing, otherwise the caller's dispose double-frees them
                window.biomeParams = default;
                window.noiseOffsets = default;
                window.Dispose();
            }
        }

        // 窓の外を読むカーネルの実効到達半径を、半径を持っている型それぞれから採って足し合わせる。
        // 移植元 InfiniteTerrainManager:46 の blendRadius/2 はブラーしか勘定しておらず coastalSmoothFactor の
        // 到達（v8で120px）に届かないため、タイル境界に14mの崖が立っていた。式ごと差し替えてある。
        // chunkPadding はマスタが明示する追加余白として下限に残す。
        // Sums the true reach of every kernel that reads outside the window, taken from whichever type owns each radius.
        // The source's blendRadius/2 (InfiniteTerrainManager:46) counted only the blur and fell short of coastalSmoothFactor's
        // reach (120px on v8), which is what stood a 14m cliff on the tile seam; the whole formula is replaced.
        // chunkPadding stays as the floor, the extra margin the master states explicitly.
        public static int ResolvePadding(TerrainGenerationConfig config, NativeArray<BiomeParams> biomeParams)
        {
            var requiredPadding = ClassificationWindowReach.Pixels(config)
                                  + HeightmapStage.MaxReachPixels(config, biomeParams);
            return Mathf.Max(config.chunkPadding, requiredPadding);
        }

        // ピクセル間隔を保ったまま窓を四方へ padding ピクセル広げる（移植元 GenerateWithPadding と同式）。
        // Widens the window by padding pixels on all sides while preserving the pixel pitch (same as the source GenerateWithPadding).
        static TerrainGenerationConfig BuildWindowConfig(
            TerrainGenerationConfig tileConfig, int baseResolution, int padding)
        {
            var pixelSizeX = tileConfig.terrainWidth / (baseResolution - 1);
            var pixelSizeZ = tileConfig.terrainLength / (baseResolution - 1);
            var windowResolution = baseResolution + 2 * padding;

            // 移植元は config を一時書換して finally で戻すが、ShallowCopy なら汚染自体が起きない
            // The source mutates config and restores it in finally; ShallowCopy avoids the mutation entirely
            var windowConfig = tileConfig.ShallowCopy();
            windowConfig.worldOffsetX -= padding * pixelSizeX;
            windowConfig.worldOffsetZ -= padding * pixelSizeZ;
            windowConfig.overrideResolution = windowResolution;
            windowConfig.terrainWidth = pixelSizeX * (windowResolution - 1);
            windowConfig.terrainLength = pixelSizeZ * (windowResolution - 1);
            return windowConfig;
        }

        // 窓1枚分の分類→高さを走らせる。protectEdgeSea は本番経路と同じ false（true はスポーン探索窓専用）。
        // Runs classification then heights for one window; protectEdgeSea stays false as in production (true is spawn-search only).
        static void RunWindow(TerrainGenerationConfig config, int biomeCount, JobBuffers buffers)
        {
            JobDataConverter.GenerateClassificationOffsets(
                config, Allocator.TempJob, out var continentalnessOffsets, out var erosionOffsets);
            try
            {
                ClassificationStage.Run(
                    config, biomeCount, buffers, continentalnessOffsets, erosionOffsets, protectEdgeSea: false);
                HeightmapStage.Run(config, biomeCount, buffers);
            }
            finally
            {
                continentalnessOffsets.Dispose();
                erosionOffsets.Dispose();
            }
        }

        // 下流が読む10チャネルを同一の添字で切り出す。高さだけ切ると分類側に窓端のシームが残る
        // Crops the ten downstream-read channels with one shared index; cropping heights alone leaves the classification seam
        static void CropWindow(
            JobBuffers window, JobBuffers destination, int baseResolution, int padding, int biomeCount)
        {
            Crop(window.heights, destination.heights, baseResolution, padding, 1);
            Crop(window.shoreMask, destination.shoreMask, baseResolution, padding, 1);
            Crop(window.landMask, destination.landMask, baseResolution, padding, 1);
            Crop(window.beachFactor, destination.beachFactor, baseResolution, padding, 1);
            Crop(window.landTextureFactor, destination.landTextureFactor, baseResolution, padding, 1);
            Crop(window.seaTextureFactor, destination.seaTextureFactor, baseResolution, padding, 1);

            // 重みだけ 1 ピクセル biomeCount 要素。ブラー系ジョブが使う stride に合わせる
            // Weights alone hold biomeCount elements per pixel, matching the stride the blur jobs use
            Crop(window.biomeWeights, destination.biomeWeights, baseResolution, padding, biomeCount);
            Crop(window.winnerBiomeIndex, destination.winnerBiomeIndex, baseResolution, padding, 1);

            // 台地の候補と受理領域も窓の判定が正本。等倍で作り直すと境界を跨ぐ台地が割れて隣タイルと食い違う
            // The plateau candidates and accepted regions are authoritative from the window; a redo at tile size would split a plateau across the seam
            Crop(window.plateauMask, destination.plateauMask, baseResolution, padding, 1);
            Crop(window.regionLabels, destination.regionLabels, baseResolution, padding, 1);
        }

        // 窓の中央 baseResolution 角を行単位で複製する（channels は1ピクセルあたりの要素数）。
        // Copies the window's central baseResolution square row by row (channels is the element count per pixel).
        static void Crop<T>(
            NativeArray<T> source, NativeArray<T> destination, int baseResolution, int padding, int channels)
            where T : struct
        {
            var windowResolution = baseResolution + 2 * padding;
            var rowLength = baseResolution * channels;
            for (var y = 0; y < baseResolution; y++)
            {
                var sourceStart = ((y + padding) * windowResolution + padding) * channels;
                NativeArray<T>.Copy(source, sourceStart, destination, y * rowLength, rowLength);
            }
        }
    }
}
