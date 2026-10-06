using Game.MapGeneration.Pipeline.Surface.Placement;
using System.Collections.Generic;
using Core.Master;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Tiling;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 単一鉱脈エントリのバンド別クラスター配置。中心 Poisson 散布→リング判定→
    // マスク/傾斜/距離フィルタ→極座標メンバー配置の順で PlacementEntry を積む。
    // Per-entry band cluster placement: Poisson centers, ring test, mask/slope/distance filters,
    // then polar member placement, appending PlacementEntry results.
    internal static class OreEntryPlacer
    {
        public static void Place(
            OreEntry entry,
            int entryIndex,
            bool[,] mask,
            float[,] heights,
            TerrainDimensions dims,
            System.Random rng,
            float borderPx,
            SpatialGrid treeSpatialGrid,
            SpatialGrid objectSpatialGrid,
            SpatialGrid oreGrid,
            PlacementHaloChannelMap centerHalos,
            float haloRadius,
            IReadOnlyList<PlacedVein> excludedVeins,
            VeinPlacementBatch result, IVeinLandConstraint landConstraint)
        {
            float w = dims.TerrainWidth;
            float l = dims.TerrainLength;
            int hRes = dims.Resolution;
            float minDist = entry.minDistanceFromOthers;

            // 中心グリッドは全bandで共有するのでband最大の間隔を採る。band値まで下げると小半径の中心が大クラスタの内側へ入る。
            // The center grid is shared by every band, so it takes the band-maximum spacing; lowering it to each band's own value would let a small-radius center sit inside a large cluster.
            float centerSpacing = 0f;
            if (entry.bands != null)
                foreach (var band in entry.bands)
                    if (band != null) centerSpacing = Mathf.Max(centerSpacing,
                        OrePlacementMath.CalculateClusterCenterSpacing(band.clusterRadius));

            var clusterCenterGrid = new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f));
            centerHalos.GetOrCreate(entryIndex).SeedGrid(
                clusterCenterGrid, dims.WorldOffsetX, dims.WorldOffsetZ, w, l, haloRadius);

            // 地形への効き方はmapVeinsマスタが正本。veinGuidの解決はGenerationMasterのバリデーションが保証する
            // The mapVeins master owns the terrain effect; GenerationMaster validation guarantees the veinGuid resolves
            var veinElement = MasterHolder.MapVeinMaster.GetElementOrNull(System.Guid.Parse(entry.veinGuid));
            var surroundEffect = RuntimeConvert.ToTerrainSurroundEffectType(
                veinElement.TerrainSurroundEffectType, "mapVeins.terrainSurroundEffectType");

            var rings = SpawnDistanceRingPlanner.BuildRings(entry.bands);
            dims.SpawnDistanceRangeXz(out var tileNearestDistance, out var tileFarthestDistance);

            foreach (var range in rings)
            {
                var band = range.Band;

                // density<=0は「この帯には置かない」宣言。Maxクランプで拾うと1個分の間隔が残り黙って湧く。
                // A density of zero or less declares "place nothing in this band"; the Max clamp would leave one cluster's spacing and spawn silently.
                if (band.density <= 0f) continue;

                // タイルに掛からないリングは全中心が捨てられるだけなので、種だけ引いて飛ばす（乱数消費数＝出力を変えない）。
                // A ring that misses this tile would have every centre discarded, so draw the seed and skip (output and RNG consumption stay identical).
                if (!range.OverlapsDistanceRange(tileNearestDistance, tileFarthestDistance))
                {
                    rng.Next();
                    continue;
                }

                float poissonArea = w * l;
                float adjustedMinDist = Mathf.Sqrt(poissonArea / (band.density * 100f));
                adjustedMinDist = Mathf.Max(adjustedMinDist,
                    OrePlacementMath.CalculateClusterCenterSpacing(band.clusterRadius));

                var candidates = PoissonDiskSampler.Generate(w, l, adjustedMinDist, rng.Next());

                foreach (var candidate in candidates)
                {
                    float localX = candidate.x;
                    float localZ = candidate.y;

                    // リング判定（ワールド座標距離・クラスター中心のみ）。
                    // Ring test (world-distance of the cluster center only).
                    if (!range.Contains(dims.DistanceFromSpawnXz(localX, localZ))) continue;

                    int px = Mathf.Clamp(Mathf.RoundToInt(localX / w * (hRes - 1)), 0, hRes - 1);
                    int pz = Mathf.Clamp(Mathf.RoundToInt(localZ / l * (hRes - 1)), 0, hRes - 1);
                    if (!mask[pz, px]) continue;
                    landConstraint.RecordEligibleCenter();
                    if (BiomeMaskBuilder.IsNearMaskEdge(mask, px, pz, hRes, borderPx)) continue;

                    if (entry.useSlopeFilter)
                    {
                        float slope = OrePlacementMath.ComputeSlopeAngle(heights, px, pz, hRes, w, dims.TerrainHeight, l);
                        float swt = OrePlacementMath.EvaluateSlopeFilter(slope, entry.slopeMax, entry.slopeSmoothness);
                        if (swt <= 0f) continue;
                        if (swt < 1f && swt < (float)rng.NextDouble()) continue;
                    }

                    if (clusterCenterGrid.HasNeighborWithin(localX, localZ, centerSpacing))
                        continue;

                    if (0f < minDist)
                    {
                        if (treeSpatialGrid != null && treeSpatialGrid.HasNeighborWithin(localX, localZ, minDist))
                            continue;
                        if (objectSpatialGrid != null && objectSpatialGrid.HasNeighborWithin(localX, localZ, minDist))
                            continue;
                        if (oreGrid.HasNeighborWithin(localX, localZ, minDist))
                            continue;
                    }

                    var cluster = new VeinPlacementCluster(
                        entryIndex, entry.veinGuid,
                        new Vector2(localX + dims.WorldOffsetX, localZ + dims.WorldOffsetZ));
                    OreClusterMemberPlacer.Place(entry, band, localX, localZ, cluster.Members,
                        heights, dims, rng, oreGrid, surroundEffect, excludedVeins, result, landConstraint);
                    if (cluster.Members.Count == 0) continue;

                    // AABB排他を生き残った実メンバーを持つ中心だけを同タイル後続候補の排他に使う。
                    // Only centers with real members that survived the AABB exclusion exclude later candidates in this tile.
                    clusterCenterGrid.Add(localX, localZ);
                    result.Clusters.Add(cluster);
                }
            }

        }
    }
}
