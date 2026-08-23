using System;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Tiling;
using Unity.Collections;

namespace Game.MapGeneration.Pipeline.Visual
{
    /// <summary>
    ///     タイル1枚ぶんの分類をパディング窓で1回だけ回し、中央クロップ済みの結果を splat と Detail の双方へ配る。
    ///     公開するのはタイル解像度に切り出した後の値だけで、窓解像度は外へ出さない。
    ///     ctorはBuffers確保のみを行い、分類本体はInitializeで走らせる。usingで先に確保すれば
    ///     Initialize内の例外でも解放責任が保たれる
    ///     Runs one tile's classification once on a padded window and hands the center-cropped result to both splat and detail.
    ///     Only the tile-resolution values are exposed; the window resolution never leaves this class.
    ///     The ctor only allocates Buffers; Initialize runs the classification, so wrapping the ctor in
    ///     using keeps disposal safe even if Initialize throws
    /// </summary>
    public sealed class TileClassificationContext : IDisposable
    {
        // splatの重み配列はこのクラスの関心外。SplatmapRuntimeGeneratorが自前で確保するので最小の1本だけ持つ
        // The splat weight array is out of scope here; SplatmapRuntimeGenerator allocates its own, so one layer suffices
        private const int ClassificationOnlyLayerCount = 1;

        private readonly TerrainGenerationConfig _tileConfig;
        private readonly BiomeType[] _biomeTypes;

        // 分類段の全チャネル。中身はタイル解像度で、heightsとwinnerは転送値で上書きされる前提の下書き
        // Every classification channel at tile resolution; heights and winner are drafts the transferred values overwrite
        public JobBuffers Buffers { get; private set; }

        // Ocean/Beach列を先頭に持つ配置用の重み。勝者マスクはここから導く
        // Placement-shaped weights with the Ocean/Beach columns up front; the winner masks derive from these
        public float[,] Weights2D { get; private set; }

        // バイオームごとのwinner-takes-allマスク。ビーチ帯も勝者バイオーム側に残る（移植元BiomeMaskBuilderの意味）
        // Per-biome winner-takes-all masks; the beach band stays with its winning biome, as the source BiomeMaskBuilder meant
        public bool[][,] WinnerMasks { get; private set; }

        public TileClassificationContext(TerrainGenerationConfig tileConfig, BiomeType[] biomeTypes)
        {
            _tileConfig = tileConfig;
            _biomeTypes = biomeTypes;

            // 確保だけを行う。分類本体はInitializeが担い、その間に例外が出てもusingがBuffersを解放できる
            // Allocation only; Initialize runs the classification, so a mid-way exception still leaves Buffers disposable via using
            Buffers = JobDataConverter.AllocateBuffers(
                tileConfig.Resolution, biomeTypes.Length, ClassificationOnlyLayerCount, Allocator.TempJob);
        }

        public void Initialize()
        {
            var resolution = _tileConfig.Resolution;
            var biomeCount = _biomeTypes.Length;

            var buffers = Buffers;
            buffers.biomeParams = JobDataConverter.ConvertBiomeParams(_tileConfig, _biomeTypes, Allocator.TempJob);
            Buffers = buffers;
            buffers.noiseOffsets = JobDataConverter.GenerateNoiseOffsets(
                _tileConfig, buffers.biomeParams, _biomeTypes, Allocator.TempJob);
            Buffers = buffers;

            // 窓を広げて分類し中央を切り出す。等倍で回すとタイル境界にsplatとDetailのシームが出る
            // Classify on a widened window and crop the center; a plain window leaves splat and detail seams at tile borders
            PaddedWindowStage.Run(_tileConfig, _biomeTypes, Buffers);

            Weights2D = PlacementInputBuilder.BuildPlacementWeights(
                Buffers.biomeWeights, Buffers.shoreMask, Buffers.beachFactor, resolution, biomeCount, biomeCount + 2);
            WinnerMasks = BiomeMaskBuilder.BuildAllWinnerMasks(Weights2D, resolution, biomeCount);
        }

        public void Dispose()
        {
            Buffers.Dispose();
        }
    }
}
