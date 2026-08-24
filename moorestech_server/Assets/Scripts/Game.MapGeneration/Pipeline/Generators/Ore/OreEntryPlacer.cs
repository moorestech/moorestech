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
            VeinPlacementBatch result)
        {
            float w = dims.TerrainWidth;
            float l = dims.TerrainLength;
            int hRes = dims.Resolution;
            float minDist = entry.minDistanceFromOthers;

            // 中心排他の設定はエントリと同じ責務に閉じる。
            // Keeps center-exclusion setup with the entry that owns the invariant.
            float centerSpacing = 0f;
            if (entry.bands != null)
                foreach (var band in entry.bands)
                    if (band != null) centerSpacing = Mathf.Max(centerSpacing,
                        OrePlacementMath.CalculateClusterCenterSpacing(band.clusterRadius));

            var clusterCenterGrid = new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f));
            centerHalos.Get(entry.veinGuid).SeedGrid(
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
                        entry.veinGuid, new Vector2(localX + dims.WorldOffsetX, localZ + dims.WorldOffsetZ));
                    PlaceClusterMembers(band, localX, localZ, cluster.Members);
                    if (cluster.Members.Count == 0) continue;

                    // 実メンバーを持つ中心だけを同タイル後続候補の排他に使う。
                    // Only centers with real members exclude later candidates in this tile.
                    clusterCenterGrid.Add(localX, localZ);
                    result.Clusters.Add(cluster);
                }
            }

            #region Internal

            // クラスターメンバーを極座標で配置（ワールド整数座標にスナップ）。
            // Place cluster members in polar coordinates, snapped to integer world coordinates.
            void PlaceClusterMembers(
                OreBand targetBand, float centerX, float centerZ, List<PlacementEntry> clusterMembers)
            {
                int clusterCount = rng.Next(1, targetBand.maxObjectsPerCluster + 1);
                float oreMinDist = targetBand.minDistanceBetweenOres;
                int retries = Mathf.Max(1, targetBand.placementRetries);
                for (int i = 0; i < clusterCount; i++)
                {
                    float mx = 0f, mz = 0f;
                    bool placed = false;
                    for (int attempt = 0; attempt < retries; attempt++)
                    {
                        float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                        float radius = (float)rng.NextDouble() * targetBand.clusterRadius;
                        mx = Mathf.Round(centerX + Mathf.Cos(angle) * radius + dims.WorldOffsetX) - dims.WorldOffsetX;
                        mz = Mathf.Round(centerZ + Mathf.Sin(angle) * radius + dims.WorldOffsetZ) - dims.WorldOffsetZ;

                        if (mx < 0 || w <= mx || mz < 0 || l <= mz) continue;
                        if (0f < oreMinDist && oreGrid.HasNeighborWithin(mx, mz, oreMinDist))
                            continue;

                        placed = true;
                        break;
                    }
                    if (!placed) continue;

                    float my = OrePlacementMath.SampleHeight(heights, mx, mz, w, l, hRes) * dims.TerrainHeight;

                    clusterMembers.Add(PlacementEntry.CreateVein(
                        entry.veinGuid,
                        new Vector3(mx + dims.WorldOffsetX, my, mz + dims.WorldOffsetZ),
                        surroundEffect));

                    oreGrid.Add(mx, mz);
                }
            }

            #endregion
        }
    }
}
