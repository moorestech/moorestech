using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;

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
            var noiseOffsets = ManagedNoise.GenerateOffsets(new System.Random(rng.Next()), 4);

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
                    if (entry.mapObjectGuids == null || entry.mapObjectGuids.Length == 0 || entry.density <= 0.001f) continue;
                    if (entry.useClusterMode)
                        ObjectIndependentPlacer.GenerateClusterObjects(entry, dims, heights, hRes,
                            mask, borderMarginPx, rng, noiseOffsets, placements, treeSpatialGrid, objAlgCfg, ref nextClusterId);
                    else
                        ObjectIndependentPlacer.GenerateIndependent(entry, dims, heights, hRes,
                            mask, borderMarginPx, rng, noiseOffsets, placements, treeSpatialGrid);
                }
            }

            return placements;
        }
    }
}
