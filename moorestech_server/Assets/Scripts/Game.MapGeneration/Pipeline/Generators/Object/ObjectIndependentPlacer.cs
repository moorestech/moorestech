using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 独立散布（Poisson）。スポーン距離リングごとにそのリングの density で Poisson を回し、リング内の候補だけ採用する。
    // リングをまたぐ点同士の最小間隔は保証されない（リングごとに独立したPoissonのため）。
    // Independent scatter (Poisson): one Poisson pass per spawn-distance ring at that ring's density, keeping only in-ring candidates.
    // Minimum spacing across ring boundaries is not guaranteed, since each ring runs its own independent Poisson pass.
    internal static class ObjectIndependentPlacer
    {
        public static void GenerateIndependent(
            BiomeObjectConfig.ObjectEntry entry, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, Vector2[] noiseOffsets,
            List<PlacementEntry> placements, SpatialGrid treeSpatialGrid)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;
            float area = w * l;

            foreach (var ring in SpawnDistanceRingPlanner.BuildRings(ObjectScatterBand.OuterRadiiOf(entry.bands)))
            {
                var band = entry.bands[ring.BandIndex];
                int desiredCount = Mathf.RoundToInt(band.density * area / 10000f);
                if (desiredCount <= 0) continue;
                float minDist = Mathf.Sqrt(area / desiredCount * 0.8f);
                var points = PoissonDiskSampler.Generate(w, l, minDist, rng.Next());

                foreach (var point in points)
                {
                    // リング判定は候補点そのもののワールド座標距離で行う（鉱脈はクラスタ中心、散布は点）。
                    // The ring test uses the candidate's own world-space distance (veins test the cluster centre, scatter the point).
                    if (!ring.Contains(DistanceFromSpawn(point.x, point.y))) continue;

                    int hx = Mathf.Clamp(Mathf.RoundToInt(point.x / w * (hRes - 1)), 0, hRes - 1);
                    int hz = Mathf.Clamp(Mathf.RoundToInt(point.y / l * (hRes - 1)), 0, hRes - 1);
                    if (!mask[hz, hx] || BiomeMaskBuilder.IsNearMaskEdge(mask, hx, hz, hRes, borderMarginPx)) continue;

                    if (entry.noiseType != MapNoiseType.None)
                    {
                        // 位置は既にワールド座標へ直しているのにノイズだけタイルローカルだと、全タイルが同じ散布を反復する
                        // The position is already world-space; leaving the noise tile-local would repeat one scatter on every tile
                        float noise = ManagedNoise.SampleByType(entry.noiseType,
                            point.x + dims.WorldOffsetX, point.y + dims.WorldOffsetZ,
                            entry.noiseFrequency, noiseOffsets) * entry.noiseAmplitude;
                        if (noise < entry.noiseThreshold) continue;
                    }

                    if (treeSpatialGrid != null)
                    {
                        if (entry.minDistanceFromTree > 0f &&
                            treeSpatialGrid.HasNeighborWithin(point.x, point.y, entry.minDistanceFromTree))
                            continue;
                        if (entry.maxDistanceFromTree > 0f &&
                            !treeSpatialGrid.HasNeighborWithin(point.x, point.y, entry.maxDistanceFromTree))
                            continue;
                    }

                    float height = heights[hz, hx];

                    if (entry.useSlopeFilter)
                    {
                        float slope = ObjectPlacementMath.ComputeSlopeAngle(heights, hx, hz, hRes, w, dims.TerrainHeight, l);
                        float sw = ObjectPlacementMath.EvaluateSlopeFilter(slope, entry.slopeMin, entry.slopeMax, entry.slopeSmoothness);
                        if (sw <= 0f) continue;
                        if (sw < 1f && (float)rng.NextDouble() > sw) continue;
                    }

                    float scale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y, (float)rng.NextDouble());
                    float yRot = (float)rng.NextDouble() * 360f;
                    var rot = Quaternion.Euler(0, yRot, 0);
                    if (entry.slopeAlignment > 0.001f)
                        rot = ObjectPlacementMath.ApplySlopeAlignment(rot, heights, point.x, point.y, w, l, hRes,
                            dims.TerrainHeight, entry.slopeAlignment);

                    float sink = Mathf.Lerp(entry.sinkRange.x, entry.sinkRange.y, (float)rng.NextDouble());

                    placements.Add(new PlacementEntry
                    {
                        MapObjectGuid = ObjectPlacementMath.PickRandomGuid(entry.mapObjectGuids, rng),
                        WorldPosition = new Vector3(point.x + dims.WorldOffsetX, height * dims.TerrainHeight, point.y + dims.WorldOffsetZ),
                        Rotation = rot,
                        Scale = new Vector3(scale, scale, scale),
                        Sink = sink,
                        Cluster = new RockClusterInfo { ClusterId = -1 }
                    });
                }
            }

            #region Internal

            // タイルローカル座標をワールド座標へ直してスポーンXZとの距離を取る（鉱脈 OreEntryPlacer と同じ基準）。
            // Convert tile-local to world and measure the XZ distance to spawn (same basis as OreEntryPlacer).
            float DistanceFromSpawn(float localX, float localZ)
            {
                float dx = (localX + dims.WorldOffsetX) - dims.SpawnWorldX;
                float dz = (localZ + dims.WorldOffsetZ) - dims.SpawnWorldZ;
                return Mathf.Sqrt(dx * dx + dz * dz);
            }

            #endregion
        }
    }
}
