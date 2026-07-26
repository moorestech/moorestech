using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Unity.Collections;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // プロトタイプ独立型の樹木配置。各 TreePrototypeEntry が独自 densityConfig で 4パス Poisson Disk を
    // 実行し共有 SpatialGrid で重なりを防ぐ。地形/テクスチャ変更(ApplyHeightModification/
    // ApplyTextureModification)は TreeInstance/TerrainData 依存のため 5b の TreePlacementStage へ委譲する。
    // Prototype-independent tree placement; each entry runs a 4-pass Poisson Disk with its own
    // densityConfig, sharing a SpatialGrid to avoid overlap. Height/texture modification passes are
    // TreeInstance/TerrainData-bound and deferred to 5b's TreePlacementStage.
    public static class TreePlacementGenerator
    {
        public static List<PlacementEntry> GenerateForBiome(
            bool[,] mask,
            float[] heights,
            TerrainDimensions dims,
            TreePlacementConfig treeConfig,
            System.Random rng)
        {
            var placements = new List<PlacementEntry>();
            if (treeConfig?.prototypes == null) return placements;
            int res = dims.Resolution;

            var sharedGrid = new SpatialGrid(dims.TerrainWidth, dims.TerrainLength, 3f);
            var curvatureMap = TreePlacementCommon.ComputeCurvatureMap(heights, res);
            var nativeHeights = new NativeArray<float>(heights, Allocator.Temp);

            // バイオーム全体の GUID 数・有効エントリ数を先に算出し、旧密度を等分で維持する。
            // Pre-count total guids and active entries to preserve the old per-entry density split.
            int biomeTotalPrefabs = 0;
            int activeEntryCount = 0;
            foreach (var e in treeConfig.prototypes)
            {
                if (e == null || e.disabled || e.mapObjectGuids == null) continue;
                bool valid = false;
                foreach (var g in e.mapObjectGuids) if (!string.IsNullOrEmpty(g)) { valid = true; biomeTotalPrefabs++; }
                if (valid) activeEntryCount++;
            }
            int biomeDesired = biomeTotalPrefabs * 1500;

            try
            {
                foreach (var entry in treeConfig.prototypes)
                {
                    if (entry == null || entry.disabled || entry.mapObjectGuids == null) continue;
                    bool hasValid = false;
                    foreach (var g in entry.mapObjectGuids) if (!string.IsNullOrEmpty(g)) { hasValid = true; break; }
                    if (!hasValid) continue;

                    var densityCfg = entry.densityConfig ?? new TreeDensityConfig();
                    var uCfg = entry.understoryConfig ?? new UnderstoryConfig();
                    float borderMarginPx = BiomeMaskBuilder.MetersToPixels(
                        entry.borderMargin, dims.TerrainWidth, res);

                    int totalDesired = activeEntryCount > 0 ? biomeDesired / activeEntryCount : 0;
                    if (totalDesired == 0) continue;

                    float area = dims.TerrainWidth * dims.TerrainLength;
                    var densityOffsets = ManagedNoise.GenerateOffsets(new System.Random(dims.Seed + 500), 4);
                    var detailOffsets = ManagedNoise.GenerateOffsets(new System.Random(dims.Seed + 600), 4);
                    var islandOffsets = ManagedNoise.GenerateOffsets(new System.Random(dims.Seed + 700), 4);
                    var noiseOffsets = ManagedNoise.GenerateOffsets(new System.Random(rng.Next()), 8);

                    float distScale = Mathf.Sqrt(activeEntryCount);
                    int beforeCount = placements.Count;

                    // ===== Pass 1 (Dense) =====
                    float denseMinDist = Mathf.Max(densityCfg.densePassMinDistance * distScale,
                        Mathf.Sqrt(area / (totalDesired * densityCfg.densePassMultiplier) * 0.8f));
                    foreach (var point in PoissonDiskSampler.Generate(
                        dims.TerrainWidth, dims.TerrainLength, denseMinDist, rng.Next()))
                    {
                        if (!TreePlacementCommon.CheckMask(mask, point, dims, res, borderMarginPx)) continue;
                        float dn = TreePlacementCommon.SampleDensityNoise(point.x, point.y,
                            densityOffsets, detailOffsets, islandOffsets, densityCfg);
                        if (dn < densityCfg.denseMinThreshold) continue;
                        TreePlacementEntry.TryPlaceEntry(entry, point, dims, heights, curvatureMap, nativeHeights,
                            noiseOffsets, densityCfg, sharedGrid, rng, placements);
                    }

                    // ===== Pass 2 (Transition) =====
                    float transMinDist = Mathf.Max(densityCfg.transitionPassMinDistance * distScale,
                        Mathf.Sqrt(area / (totalDesired * densityCfg.transitionPassMultiplier) * 0.8f));
                    foreach (var point in PoissonDiskSampler.Generate(
                        dims.TerrainWidth, dims.TerrainLength, transMinDist, rng.Next()))
                    {
                        if (!TreePlacementCommon.CheckMask(mask, point, dims, res, borderMarginPx)) continue;
                        float dn = TreePlacementCommon.SampleDensityNoise(point.x, point.y,
                            densityOffsets, detailOffsets, islandOffsets, densityCfg);
                        if (dn >= densityCfg.denseMinThreshold || dn < densityCfg.transitionMinThreshold)
                            continue;
                        float transRatio = (dn - densityCfg.transitionMinThreshold)
                                         / (densityCfg.denseMinThreshold - densityCfg.transitionMinThreshold);
                        float transProb = densityCfg.transitionBaseProb
                            + Mathf.Pow(transRatio, densityCfg.transitionProbPower) * densityCfg.transitionPeakProb;
                        if ((float)rng.NextDouble() > transProb) continue;
                        TreePlacementEntry.TryPlaceEntry(entry, point, dims, heights, curvatureMap, nativeHeights,
                            noiseOffsets, densityCfg, sharedGrid, rng, placements);
                    }

                    // ===== Pass 3 (Sparse) =====
                    float sparseMinDist = Mathf.Max(densityCfg.sparsePassMinDistance * distScale,
                        Mathf.Sqrt(area / (totalDesired * densityCfg.sparsePassMultiplier) * 0.8f));
                    foreach (var point in PoissonDiskSampler.Generate(
                        dims.TerrainWidth, dims.TerrainLength, sparseMinDist, rng.Next()))
                    {
                        if (!TreePlacementCommon.CheckMask(mask, point, dims, res, borderMarginPx)) continue;
                        float dn = TreePlacementCommon.SampleDensityNoise(point.x, point.y,
                            densityOffsets, detailOffsets, islandOffsets, densityCfg);
                        if (dn >= densityCfg.transitionMinThreshold) continue;
                        float openRatio = 1f - dn / densityCfg.transitionMinThreshold;
                        if ((float)rng.NextDouble() < openRatio * densityCfg.sparseOpenRejectFactor) continue;
                        TreePlacementEntry.TryPlaceEntry(entry, point, dims, heights, curvatureMap, nativeHeights,
                            noiseOffsets, densityCfg, sharedGrid, rng, placements);
                    }

                    // ===== Pass 4 (Scatter) =====
                    foreach (var point in PoissonDiskSampler.Generate(
                        dims.TerrainWidth, dims.TerrainLength,
                        densityCfg.scatterPassMinDistance * distScale, rng.Next()))
                    {
                        if (!TreePlacementCommon.CheckMask(mask, point, dims, res, borderMarginPx)) continue;
                        float dn = TreePlacementCommon.SampleDensityNoise(point.x, point.y,
                            densityOffsets, detailOffsets, islandOffsets, densityCfg);
                        if (dn >= densityCfg.transitionMinThreshold) continue;
                        float scatterProb = dn / densityCfg.transitionMinThreshold
                            * densityCfg.scatterDensityFactor + densityCfg.scatterBaseProb;
                        if ((float)rng.NextDouble() > scatterProb) continue;
                        TreePlacementEntry.TryPlaceEntry(entry, point, dims, heights, curvatureMap, nativeHeights,
                            noiseOffsets, densityCfg, sharedGrid, rng, placements);
                    }

                    // ===== Understory（自己クラスター方式）=====
                    // Understory (self-cluster method)
                    if (uCfg.understoryScaleThreshold > 0f)
                    {
                        TreeUnderstoryPlacer.AddSelfUnderstory(placements, beforeCount, mask, dims, heights,
                            nativeHeights, entry, densityOffsets, detailOffsets, islandOffsets,
                            rng, densityCfg, uCfg, borderMarginPx, sharedGrid);
                    }
                }
            }
            finally
            {
                nativeHeights.Dispose();
            }

            return placements;
        }
    }
}
