using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Tiling
{
    /// <summary>
    ///     タイル1枚ぶんのbiomeインデックスを組む唯一の場所。分類済みバッファから引く本番と、
    ///     窓から回して確かめる検証が同じ組み立てを通るための切り出し。
    ///     The single place assembling one tile's biome indices, extracted so that production reading
    ///     classified buffers and verification running the window itself both go through one assembly.
    /// </summary>
    public static class TileBiomeIndexBuilder
    {
        // 分類だけを回すバッファはsplatの重み列を使わないので1本で足りる（TileClassificationContextと同じ理由）
        // Buffers that only run the classification never touch the splat weight columns, so one suffices, as in TileClassificationContext
        private const int ClassificationOnlyLayerCount = 1;

        // splatが読む[z,x]の形で返す。分類結果を他にも使う呼び出し側が自前のバッファを渡す入口
        // Returns the [z,x] shape the splat reads; the entry for callers that have other uses for the classification and pass their own buffers
        public static byte[,] BuildTileGrid(JobBuffers classifiedBuffers, BiomeType[] biomeTypes, int resolution)
        {
            var indices = Build(classifiedBuffers, biomeTypes, resolution);
            var grid = new byte[resolution, resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                grid[z, x] = indices[z * resolution + x];

            return grid;
        }

        // パディング窓から回して1タイルぶんを組む。biomeParamsとnoiseOffsetsは借り物で、破棄は貸し主が持つ
        // Runs the padded window through to one tile's indices; biomeParams and noiseOffsets are borrowed and the lender disposes them
        public static byte[] BuildForTile(
            TerrainGenerationConfig tileConfig, BiomeType[] biomeTypes,
            NativeArray<BiomeParams> biomeParams, NativeArray<float2> noiseOffsets)
        {
            var resolution = tileConfig.Resolution;
            var buffers = JobDataConverter.AllocateBuffers(
                resolution, biomeTypes.Length, ClassificationOnlyLayerCount, Allocator.TempJob);
            buffers.biomeParams = biomeParams;
            buffers.noiseOffsets = noiseOffsets;
            try
            {
                // 窓を広げて分類し中央を切り出す。等倍で回すとタイル境界にシームが出る
                // Classify on a widened window and crop the center; a plain window leaves seams at the tile borders
                PaddedWindowStage.Run(tileConfig, biomeTypes, buffers);
                return Build(buffers, biomeTypes, resolution);
            }
            finally
            {
                // 借りた2本を切り離してから破棄する。付けたままだと貸し主の破棄と二重解放になる
                // Detach the two borrowed arrays before disposing, otherwise the lender's dispose double-frees them
                buffers.biomeParams = default;
                buffers.noiseOffsets = default;
                buffers.Dispose();
            }
        }

        // サーバーが転送していたbiome_x_z.binと同じ式。転送をやめてもSplatmapJobが読む勝者は1ビットも変わらない
        // The same formula that produced the transferred biome_x_z.bin; dropping the transfer changes no bit of the winner SplatmapJob reads
        private static byte[] Build(JobBuffers classifiedBuffers, BiomeType[] biomeTypes, int resolution)
        {
            return PlacementInputBuilder.BuildBiomeIndices(
                classifiedBuffers.winnerBiomeIndex, classifiedBuffers.landMask, classifiedBuffers.beachFactor,
                biomeTypes, resolution * resolution);
        }
    }
}
