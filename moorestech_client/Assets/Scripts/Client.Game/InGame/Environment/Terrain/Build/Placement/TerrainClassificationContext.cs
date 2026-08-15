using System;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Tiling;
using Unity.Collections;

namespace Client.Game.InGame.Environment.Terrain.Build.Placement
{
    /// <summary>
    ///     タイル1枚ぶんの分類をパディング窓で1回だけ回し、中央クロップ済みの結果を splat と Detail の双方へ配る。
    ///     公開するのはタイル解像度に切り出した後の値だけで、窓解像度は外へ出さない
    ///     Runs one tile's classification once on a padded window and hands the center-cropped result to both splat and detail.
    ///     Only the tile-resolution values are exposed; the window resolution never leaves this class
    /// </summary>
    public sealed class TerrainClassificationContext : IDisposable
    {
        // splatの重み配列はこのクラスの関心外。SplatmapRuntimeGeneratorが自前で確保するので最小の1本だけ持つ
        // The splat weight array is out of scope here; SplatmapRuntimeGenerator allocates its own, so one layer suffices
        private const int ClassificationOnlyLayerCount = 1;

        // 分類段の全チャネル。中身はタイル解像度で、heightsとwinnerは転送値で上書きされる前提の下書き
        // Every classification channel at tile resolution; heights and winner are drafts the transferred values overwrite
        public JobBuffers Buffers { get; }

        // Ocean/Beach列を先頭に持つ配置用の重み。勝者マスクはここから導く
        // Placement-shaped weights with the Ocean/Beach columns up front; the winner masks derive from these
        public float[,] Weights2D { get; }

        // バイオームごとのwinner-takes-allマスク。ビーチ帯も勝者バイオーム側に残る（移植元BiomeMaskBuilderの意味）
        // Per-biome winner-takes-all masks; the beach band stays with its winning biome, as the source BiomeMaskBuilder meant
        public bool[][,] WinnerMasks { get; }

        public TerrainClassificationContext(TerrainGenerationConfig tileConfig, BiomeType[] biomeTypes)
        {
            var resolution = tileConfig.Resolution;
            var biomeCount = biomeTypes.Length;

            var buffers = JobDataConverter.AllocateBuffers(
                resolution, biomeCount, ClassificationOnlyLayerCount, Allocator.TempJob);
            buffers.biomeParams = JobDataConverter.ConvertBiomeParams(tileConfig, biomeTypes, Allocator.TempJob);
            buffers.noiseOffsets = JobDataConverter.GenerateNoiseOffsets(
                tileConfig, buffers.biomeParams, biomeTypes, Allocator.TempJob);

            // 窓を広げて分類し中央を切り出す。等倍で回すとタイル境界にsplatとDetailのシームが出る
            // Classify on a widened window and crop the center; a plain window leaves splat and detail seams at tile borders
            PaddedWindowStage.Run(tileConfig, biomeTypes, buffers);

            Buffers = buffers;
            Weights2D = PlacementInputBuilder.BuildPlacementWeights(
                buffers.biomeWeights, buffers.shoreMask, buffers.beachFactor, resolution, biomeCount, biomeCount + 2);
            WinnerMasks = BiomeMaskBuilder.BuildAllWinnerMasks(Weights2D, resolution, biomeCount);
        }

        public void Dispose()
        {
            Buffers.Dispose();
        }
    }
}
