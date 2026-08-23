using System.Collections.Generic;
using Core.Master;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
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
            SpatialGrid clusterCenterGrid,
            float centerSpacing,
            PlacementHaloChannel centerHalo,
            List<PlacementEntry> result)
        {
            float w = dims.TerrainWidth;
            float l = dims.TerrainLength;
            int hRes = dims.Resolution;
            float minDist = entry.minDistanceFromOthers;

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
                adjustedMinDist = Mathf.Max(adjustedMinDist, band.clusterRadius * 2.5f);

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

                    clusterCenterGrid.Add(localX, localZ);
                    centerHalo.Add(localX + dims.WorldOffsetX, localZ + dims.WorldOffsetZ);

                    PlaceClusterMembers(band, localX, localZ);
                }
            }

            #region Internal

            // クラスターメンバーを極座標で配置（ワールド整数座標にスナップ）。
            // Place cluster members in polar coordinates, snapped to integer world coordinates.
            void PlaceClusterMembers(OreBand targetBand, float centerX, float centerZ)
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

                    // 鉱脈は見た目ステージへ渡らず未使用だが、岩に近い性質なのでrockNoBareGroundを明示する
                    // Veins never reach the visual stages so this goes unused, but rockNoBareGround names its rock-like nature explicitly
                    result.Add(PlacementEntry.CreateVein(
                        entry.veinGuid,
                        new Vector3(mx + dims.WorldOffsetX, my, mz + dims.WorldOffsetZ),
                        TerrainSurroundEffectType.rockNoBareGround));

                    oreGrid.Add(mx, mz);
                }
            }

            #endregion
        }
    }
}
