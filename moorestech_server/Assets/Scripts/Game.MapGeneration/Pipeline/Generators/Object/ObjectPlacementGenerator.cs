using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 階層的オブジェクト配置: Primary(大岩) + 任意数の従属グループ(Ring/Saddle) と独立散布を生成する。
    // prefab 参照はスキーマ化で mapObjectGuid（文字列）へ置換した。メッシュ半径推定など見た目専用の
    // 旧 GeneratePrimaryClusters は移植しない。
    // Hierarchical object placement: Primary rocks plus subordinate groups (Ring/Saddle) and scatter.
    // prefab references replaced by mapObjectGuid strings; the view-only legacy GeneratePrimaryClusters
    // (mesh-radius estimation) is not ported.
    public static class ObjectPlacementGenerator
    {
        public static List<PlacementEntry> GenerateForBiome(
            bool[,] mask,
            float[,] heights,
            TerrainDimensions dims,
            BiomeObjectConfig objConfig,
            System.Random rng,
            int noiseSeed,
            SpatialGrid treeSpatialGrid,
            ref int nextClusterId)
        {
            var placements = new List<PlacementEntry>();
            int hRes = dims.Resolution;

            bool hasEntries = objConfig.entries != null && objConfig.entries.Length > 0;
            bool hasClusters = objConfig.clusterEntries != null && objConfig.clusterEntries.Length > 0;
            if (!hasEntries && !hasClusters) return placements;

            var objAlgCfg = objConfig.algorithmConfig ?? new ObjectAlgorithmConfig();
            float borderMarginPx = BiomeMaskBuilder.MetersToPixels(objConfig.borderMargin, dims.TerrainWidth, hRes);
            // rng にはタイルが混ざっているため、ここから引くとノイズ場がタイルごとに別物になり境目に直線が立つ。
            // rng carries the tile term, so drawing from it would give each tile a different noise field and stand a straight line on the seam.
            var noiseOffsets = ManagedNoise.GenerateOffsets(new System.Random(noiseSeed), 4);

            // ===== Phase A: clusterEntries =====
            var clusterInfos = new List<RockClusterInfo>();
            if (hasClusters)
            {
                foreach (var cluster in objConfig.clusterEntries)
                {
                    if (cluster.primary == null || cluster.primary.Length == 0) continue;
                    ObjectClusterPlacer.GeneratePrimaryClusters(cluster, dims, heights, hRes,
                        mask, borderMarginPx, rng, noiseOffsets, placements, clusterInfos,
                        treeSpatialGrid, objAlgCfg, ref nextClusterId);
                    if (cluster.secondaries != null)
                    {
                        foreach (var sec in cluster.secondaries)
                        {
                            if (sec?.mapObjectGuids == null || sec.mapObjectGuids.Length == 0) continue;
                            switch (sec.mode)
                            {
                                case SecondaryPlacementMode.Ring:
                                    ObjectSecondaryPlacer.GenerateRingPlacement(sec, dims, heights, hRes,
                                        mask, borderMarginPx, rng, placements, clusterInfos,
                                        treeSpatialGrid);
                                    break;
                                case SecondaryPlacementMode.Saddle:
                                    ObjectSecondaryPlacer.GenerateSaddlePlacement(sec, dims, heights, hRes,
                                        mask, borderMarginPx, rng, placements, clusterInfos,
                                        treeSpatialGrid, objAlgCfg);
                                    break;
                            }
                        }
                    }
                }
            }

            // ===== Phase B: 独立散布エントリ =====
            // Phase B: independent scatter entries
            if (hasEntries)
            {
                foreach (var entry in objConfig.entries)
                {
                    if (entry.mapObjectGuids == null || entry.mapObjectGuids.Length == 0) continue;

                    // バンド未設定は警告してスキップ（鉱脈 OreEntryPlacer と同じ扱い）。
                    // Warn and skip entries without bands (same treatment as OreEntryPlacer).
                    if (entry.bands == null || entry.bands.Length == 0)
                    {
                        Debug.LogWarning($"[ObjectPlacement] scatter entry '{entry.mapObjectGuids[0]}' has no spawn-distance bands; skipping.");
                        continue;
                    }

                    // 不正な外半径を警告する（鉱脈 OreEntryPlacer と同じ判定・同じ文言）。
                    // Warn on invalid outer radii (same check and wording as OreEntryPlacer for veins).
                    ValidateBandRadii(entry);

                    if (entry.useClusterMode)
                        ObjectBackboneClusterPlacer.Generate(entry, dims, heights, hRes,
                            mask, borderMarginPx, rng, noiseOffsets, placements, objAlgCfg, ref nextClusterId);
                    else
                        ObjectIndependentPlacer.GenerateIndependent(entry, dims, heights, hRes,
                            mask, borderMarginPx, rng, noiseOffsets, placements, treeSpatialGrid);
                }
            }

            return placements;
        }

        // 不正な外半径（-1以外の負値・重複）を警告する（鉱脈 OreEntryPlacer.Place と同じ判定・文言）。
        // Warn on invalid outer radii (negative other than -1, duplicates) — mirrors OreEntryPlacer.Place.
        static void ValidateBandRadii(BiomeObjectConfig.ObjectEntry entry)
        {
            var seenKeys = new HashSet<float>();
            foreach (var b in entry.bands)
            {
                if (b.outerRadiusMeters < 0f && b.outerRadiusMeters != -1f)
                    Debug.LogWarning($"[ObjectPlacement] '{entry.mapObjectGuids[0]}' has a negative outer radius ({b.outerRadiusMeters}) other than -1; treated as infinite.");
                float key = b.outerRadiusMeters < 0f ? float.PositiveInfinity : b.outerRadiusMeters;
                if (!seenKeys.Add(key))
                    Debug.LogWarning($"[ObjectPlacement] '{entry.mapObjectGuids[0]}' has bands with duplicate outer radius ({b.outerRadiusMeters}); later ones degenerate.");
            }
        }
    }
}
