using System.Collections.Generic;
using System.Threading.Tasks;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Detail
{
    /// <summary>
    ///     バイオームのDetail密度マップを生成する。MapMaking DetailPlacementGenerator の移植で、
    ///     並びの正本はDetailPrototypeRuntimeConfigCollector
    ///     Builds a biome's detail density maps, ported from MapMaking's DetailPlacementGenerator;
    ///     the order's source of truth is DetailPrototypeRuntimeConfigCollector
    /// </summary>
    public static class DetailRuntimeGenerator
    {
        // ノイズオフセットの本数。移植元と同じ8本で、同じrngから同じ順に引くことで分布を一致させる
        // Eight noise offsets as in the source; drawing them from the same rng in the same order keeps distributions aligned
        private const int NoiseOffsetCount = 8;

        // 距離場・splatmapは呼び出し側が供給する。まだ無い段階ではnullを渡すとそのフィルタだけが休む
        // The caller supplies the distance fields and splatmap; passing null idles only the filters that need them
        public static List<int[,]> GenerateForBiome(
            bool[,] mask, float[,] heights, float[,] slopes, TerrainDimensions dimensions,
            BiomeDetailConfig detailConfig, System.Random rng,
            float[,,] splatmap,
            float[,] treeDistanceMap, float[,] objectDistanceMap)
        {
            var heightmapResolution = dimensions.Resolution;
            var detailResolution = dimensions.DetailResolution;

            // 密度はheightmapのセルを引いて決まるので、heightmapより細かいdetailは引く先が無い。添字外れではなく設定の誤りとして落とす
            // Density is sampled from heightmap cells, so a detail finer than the heightmap has nothing to sample; fail as a misconfiguration rather than an index error
            if (heightmapResolution - 1 < detailResolution)
                throw new System.InvalidOperationException(
                    $"[DetailRuntimeGenerator] Detail resolution {detailResolution} exceeds what heightmap resolution {heightmapResolution} can sample.");
            var maps = new List<int[,]>();

            // 曲率・方位角は使うフィルタが1つでもある時だけ計算する
            // Curvature and azimuth are computed only when at least one filter asks for them
            DetectPrecomputeRequirements(detailConfig, out var needsCurvature, out var needsAzimuth);
            var curvature = needsCurvature ? CurvatureComputer.ComputeCurvature(heights, heightmapResolution) : null;
            var azimuth = needsAzimuth ? CurvatureComputer.ComputeAzimuth(heights, heightmapResolution) : null;

            var context = new DetailSampleContext(
                mask, slopes, curvature, azimuth, treeDistanceMap, objectDistanceMap,
                splatmap, ManagedNoise.GenerateOffsets(rng, NoiseOffsetCount),
                heightmapResolution, detailResolution, splatmap != null ? splatmap.GetLength(0) : 0,
                dimensions.TerrainWidth, dimensions.TerrainLength,
                dimensions.WorldOffsetX, dimensions.WorldOffsetZ,
                detailConfig.filterRejectThreshold,
                BiomeMaskBuilder.MetersToPixels(detailConfig.borderMargin, dimensions.TerrainWidth, heightmapResolution));

            foreach (var entry in detailConfig.entries)
            {
                // 未解決のエントリを読み飛ばすとアドレス整備漏れが「草が生えない」形でしか現れない。ここで落とす
                // Skipping an unresolved entry would surface a missing address only as absent grass, so it fails here instead
                entry.textureFilter.ThrowIfUnresolved();

                maps.Add(BuildDensityMap(entry, context, maps));
            }

            return maps;
        }

        // 1エントリ分の密度マップを行単位で並列に埋める。各ピクセルは隣接を参照しないので完全に独立
        // Fills one entry's density map row-parallel; every pixel is independent since none reads its neighbours
        private static int[,] BuildDensityMap(DetailEntry entry, in DetailSampleContext context, List<int[,]> completedMaps)
        {
            var detailResolution = context.DetailResolution;
            var map = new int[detailResolution, detailResolution];

            // Parallel.Forのクロージャが捕捉できるよう、エントリ単位で固定の値をローカルへ写す
            // Copy the per-entry constants into locals so the Parallel.For closure can capture them
            var capturedEntry = entry;
            var capturedContext = context;
            var capturedMaps = completedMaps;
            var precedingMapCount = completedMaps.Count;

            Parallel.For(0, detailResolution, z =>
            {
                for (var x = 0; x < detailResolution; x++)
                {
                    if (!DetailDensitySampler.TryEvaluate(capturedEntry, capturedContext, x, z, out var density)) continue;

                    // occludedByOthers: 先行エントリのマップは確定済みなので読み取りは安全
                    // occludedByOthers: earlier entries' maps are already finalized, so reading them is safe
                    if (capturedEntry.occludedByOthers && IsOccluded(capturedMaps, precedingMapCount, x, z)) continue;

                    map[z, x] = Mathf.Clamp(
                        Mathf.RoundToInt(density * capturedEntry.maxDensity), 0, capturedEntry.maxDensity);
                }
            });

            return map;
        }

        private static bool IsOccluded(List<int[,]> completedMaps, int precedingMapCount, int x, int z)
        {
            for (var i = 0; i < precedingMapCount; i++)
                if (0 < completedMaps[i][z, x])
                    return true;

            return false;
        }

        // 曲率・方位角の事前計算が要るかを全エントリから判定する
        // Decide from every entry whether curvature and azimuth need precomputing
        private static void DetectPrecomputeRequirements(BiomeDetailConfig detailConfig, out bool needsCurvature, out bool needsAzimuth)
        {
            needsCurvature = false;
            needsAzimuth = false;
            foreach (var entry in detailConfig.entries)
            {
                if (entry.curvatureFilter.enabled) needsCurvature = true;
                if (entry.angleFilter.enabled) needsAzimuth = true;
                if (needsCurvature && needsAzimuth) return;
            }
        }
    }
}
