using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    // マネージド Config→NativeArray の変換ブリッジ。BiomeType 配列のソート順・RNG 消費順を厳守し
    // seed 再現性を保証する。テクスチャ(TerrainLayer)経路はサーバー非対象のため移植しない（5b/見た目）。
    // Bridge from managed config to NativeArrays, preserving BiomeType sort order and RNG
    // consumption order for seed reproducibility. The texture (TerrainLayer) path is not ported
    // (server-irrelevant; handled by 5b/client visuals).
    public static class JobDataConverter
    {
        public static NativeArray<BiomeParams> ConvertBiomeParams(
            TerrainGenerationConfig config, BiomeType[] biomeTypes, Allocator allocator)
        {
            var helper = new BiomePlacementHelper(config);
            var result = new NativeArray<BiomeParams>(biomeTypes.Length, allocator);

            for (int i = 0; i < biomeTypes.Length; i++)
            {
                var bp = BiomeParamsBuilder.CreateBaseParams(biomeTypes[i], config, helper);
                BiomeParamsFiller.FillHeightParams(ref bp, config, biomeTypes[i]);
                BiomeParamsBuilder.FillClassificationRange(ref bp, config, biomeTypes[i]);
                result[i] = bp;
            }

            return result;
        }

        public static void GenerateClassificationOffsets(
            TerrainGenerationConfig config,
            Allocator allocator,
            out NativeArray<float2> continentalnessOffsets,
            out NativeArray<float2> erosionOffsets)
        {
            var rng = new System.Random(config.seed);
            continentalnessOffsets = AllocOffsets(rng, config.continentalnessOctaves, allocator);
            erosionOffsets = AllocOffsets(rng, config.erosionOctaves, allocator);
        }

        // ボロノイ四色配色用のバイオーム並び替えテーブル（Fisher-Yates・seed 依存の決定論的置換）。
        // Biome permutation table for voronoi coloring (Fisher-Yates, seed-deterministic).
        public static NativeArray<int> GenerateBiomePermutation(
            int seed, int biomeCount, Allocator allocator)
        {
            var perm = new NativeArray<int>(biomeCount, allocator);
            for (int i = 0; i < biomeCount; i++) perm[i] = i;

            var rng = new System.Random(seed + 54321);
            for (int i = biomeCount - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = perm[i];
                perm[i] = perm[j];
                perm[j] = tmp;
            }
            return perm;
        }

        static NativeArray<float2> AllocOffsets(System.Random rng, int count, Allocator allocator)
        {
            var arr = new NativeArray<float2>(count, allocator);
            for (int i = 0; i < count; i++)
            {
                arr[i] = new float2(
                    (float)rng.NextDouble() * 10000f,
                    (float)rng.NextDouble() * 10000f);
            }
            return arr;
        }

        // 既存パイプラインと同じ RNG 消費順で noiseOffsets を生成し flat NativeArray に詰める。
        // Generate noiseOffsets in the same RNG consumption order and pack into a flat NativeArray.
        public static NativeArray<float2> GenerateNoiseOffsets(
            TerrainGenerationConfig config,
            NativeArray<BiomeParams> biomeParams,
            BiomeType[] biomeTypes,
            Allocator allocator)
        {
            var rng = new System.Random(config.seed);
            ConsumeOffsets(rng, config.continentalnessOctaves);
            ConsumeOffsets(rng, config.erosionOctaves);
            ConsumeOffsets(rng, 4);
            ConsumeOffsets(rng, 4);
            ConsumeOffsets(rng, 4);

            int totalOffsets = 0;
            for (int i = 0; i < biomeTypes.Length; i++)
                totalOffsets += GetNoiseOffsetCount(config, biomeTypes[i]);

            var result = new NativeArray<float2>(totalOffsets, allocator);
            int cursor = 0;

            for (int i = 0; i < biomeTypes.Length; i++)
            {
                int count = GetNoiseOffsetCount(config, biomeTypes[i]);

                var bp = biomeParams[i];
                bp.noiseOffsetBase = cursor;
                bp.noiseOffsetCount = count;
                biomeParams[i] = bp;

                for (int j = 0; j < count; j++)
                {
                    result[cursor + j] = new float2(
                        (float)rng.NextDouble() * 10000f,
                        (float)rng.NextDouble() * 10000f
                    );
                }
                cursor += count;
            }

            return result;
        }

        // パイプラインで必要な全 NativeArray バッファを一括確保する。
        // Allocate all NativeArray buffers the pipeline needs.
        public static JobBuffers AllocateBuffers(
            int resolution, int biomeCount, int layerCount, Allocator allocator)
        {
            int pixelCount = resolution * resolution;
            int totalBiomeCols = biomeCount + 2;

            return new JobBuffers
            {
                shoreMask = new NativeArray<float>(pixelCount, allocator),
                landMask = new NativeArray<float>(pixelCount, allocator),
                beachFactor = new NativeArray<float>(pixelCount, allocator),
                coastalSmoothFactor = new NativeArray<float>(pixelCount, allocator),
                landTextureFactor = new NativeArray<float>(pixelCount, allocator),
                seaTextureFactor = new NativeArray<float>(pixelCount, allocator),
                rawBiomeIndex = new NativeArray<int>(pixelCount, allocator),
                rawBiomeWeights = new NativeArray<float>(pixelCount * totalBiomeCols, allocator),
                biomeWeights = new NativeArray<float>(pixelCount * totalBiomeCols, allocator),
                winnerBiomeIndex = new NativeArray<int>(pixelCount, allocator),
                heights = new NativeArray<float>(pixelCount, allocator),
                splatWeights = new NativeArray<float>(pixelCount * layerCount, allocator),
                blurTemp = new NativeArray<float>(pixelCount * totalBiomeCols, allocator),
                plateauMask = new NativeArray<float>(pixelCount, allocator),
                regionLabels = new NativeArray<int>(pixelCount, allocator),
                regionInfos = new NativeArray<PlateauRegionInfo>(64, allocator),
                regionCount = new NativeArray<int>(1, allocator),
            };
        }

        public static BiomeParams CreateSingleBiomeParams(
            TerrainGenerationConfig config, BiomeType type)
        {
            var bp = BiomeParamsBuilder.CreateDefaultParams((int)type);
            BiomeParamsFiller.FillHeightParams(ref bp, config, type);
            return bp;
        }

        // バイオームが必要とするノイズオフセット数を config から算出する。
        // Compute the noise-offset count each biome requires from config.
        public static int GetNoiseOffsetCount(TerrainGenerationConfig config, BiomeType type)
        {
            switch (type)
            {
                case BiomeType.Grassland:
                    return 2;
                case BiomeType.Forest:
                    var f = config.forest;
                    return f.warpIterations * 2 + f.baseOctaves + f.detailOctaves + f.ridgeOctaves;
                case BiomeType.Savanna:
                    return 7;
                case BiomeType.Desert:
                    return 3 + config.desert.canyonOctaves + config.desert.cliffOctaves;
                case BiomeType.Mesa:
                    var m = config.mesa;
                    return m.warpIterations * 2 + m.octaves * 2 + m.canyonOctaves + 5;
                case BiomeType.Alpine:
                    var a = config.alpine;
                    return a.warpIterations * 2 + (a.octaves + 1) + a.ridgeOctaves + 22;
                case BiomeType.Jungle:
                    return config.jungle.warpOctaves * 2 + 3;
                case BiomeType.Woods:
                    return 4;
                default:
                    return 0;
            }
        }

        static void ConsumeOffsets(System.Random rng, int count)
        {
            for (int i = 0; i < count; i++)
            {
                rng.NextDouble();
                rng.NextDouble();
            }
        }
    }
}
