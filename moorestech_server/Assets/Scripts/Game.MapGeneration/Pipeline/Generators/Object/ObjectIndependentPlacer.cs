using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 独立散布（Poisson）と旧バックボーンクラスター（clusterMode 互換）の配置。
    // Independent scatter (Poisson) and the legacy backbone-cluster (clusterMode) placement.
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
            int desiredCount = Mathf.RoundToInt(entry.density * area / 10000f);
            if (desiredCount <= 0) return;
            float minDist = Mathf.Sqrt(area / desiredCount * 0.8f);
            var points = PoissonDiskSampler.Generate(w, l, minDist, rng.Next());

            foreach (var point in points)
            {
                int hx = Mathf.Clamp(Mathf.RoundToInt(point.x / w * (hRes - 1)), 0, hRes - 1);
                int hz = Mathf.Clamp(Mathf.RoundToInt(point.y / l * (hRes - 1)), 0, hRes - 1);
                if (!mask[hz, hx] || BiomeMaskBuilder.IsNearMaskEdge(mask, hx, hz, hRes, borderMarginPx)) continue;

                if (entry.noiseType != MapNoiseType.None)
                {
                    float noise = ManagedNoise.SampleByType(entry.noiseType, point.x, point.y,
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

        public static void GenerateClusterObjects(
            BiomeObjectConfig.ObjectEntry entry, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, Vector2[] noiseOffsets, List<PlacementEntry> placements,
            SpatialGrid treeSpatialGrid, ObjectAlgorithmConfig objAlgCfg, ref int nextClusterId)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;
            float centerMinDist = Mathf.Sqrt(w * l / entry.clusterCount * objAlgCfg.clusterSpacingFactor);
            var centers = PoissonDiskSampler.Generate(w, l, centerMinDist, rng.Next());

            int placed = 0;
            foreach (var center in centers)
            {
                if (placed >= entry.clusterCount) break;
                int cx = Mathf.Clamp(Mathf.RoundToInt(center.x / w * (hRes - 1)), 0, hRes - 1);
                int cz = Mathf.Clamp(Mathf.RoundToInt(center.y / l * (hRes - 1)), 0, hRes - 1);
                if (!mask[cz, cx] || BiomeMaskBuilder.IsNearMaskEdge(mask, cx, cz, hRes, borderMarginPx)) continue;

                if (entry.noiseType != MapNoiseType.None)
                {
                    float noise = ManagedNoise.SampleByType(entry.noiseType, center.x, center.y,
                        entry.noiseFrequency, noiseOffsets) * entry.noiseAmplitude;
                    if (noise < entry.noiseThreshold) continue;
                }

                placed++;
                int clusterId = nextClusterId++;
                int boneCount = Mathf.Min(3 + rng.Next(3), entry.objectsPerCluster);
                float backboneAngle = (float)rng.NextDouble() * Mathf.PI;
                float halfLen = entry.clusterRadius * 0.5f;

                float centerWorldX = center.x + dims.WorldOffsetX;
                float centerWorldZ = center.y + dims.WorldOffsetZ;
                float centerHt = heights[cz, cx] * dims.TerrainHeight;
                var clusterInfo = new RockClusterInfo
                {
                    ClusterId = clusterId,
                    Center = new Vector3(centerWorldX, centerHt, centerWorldZ),
                    HeroCenter = new Vector3(centerWorldX, centerHt, centerWorldZ),
                    Angle = backboneAngle,
                    Length = entry.clusterRadius,
                    FootprintRadius = entry.clusterRadius
                };

                for (int i = 0; i < boneCount; i++)
                {
                    float t = boneCount <= 1 ? 0f : (2f * i / (boneCount - 1) - 1f);
                    float axisOff = t * halfLen + ((float)rng.NextDouble() - 0.5f) * halfLen * 0.2f;
                    float latJit = ((float)rng.NextDouble() - 0.5f) * halfLen * 0.3f;
                    float ox = center.x + axisOff * Mathf.Cos(backboneAngle) - latJit * Mathf.Sin(backboneAngle);
                    float oz = center.y + axisOff * Mathf.Sin(backboneAngle) + latJit * Mathf.Cos(backboneAngle);
                    if (ox < 0 || ox > w || oz < 0 || oz > l) continue;

                    int hx = Mathf.Clamp(Mathf.RoundToInt(ox / w * (hRes - 1)), 0, hRes - 1);
                    int hz = Mathf.Clamp(Mathf.RoundToInt(oz / l * (hRes - 1)), 0, hRes - 1);
                    float height = heights[hz, hx];

                    float scale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y, (float)rng.NextDouble());
                    float yScale = i == 0
                        ? scale * (0.65f + (float)rng.NextDouble() * 0.15f)
                        : scale * (0.45f + (float)rng.NextDouble() * 0.25f);
                    float yRotDeg = backboneAngle * Mathf.Rad2Deg + ((float)rng.NextDouble() - 0.5f) * 30f;
                    var rot = Quaternion.Euler(0, yRotDeg, 0);
                    if (entry.slopeAlignment > 0.001f)
                        rot = ObjectPlacementMath.ApplySlopeAlignment(rot, heights, ox, oz, w, l, hRes,
                            dims.TerrainHeight, entry.slopeAlignment);

                    float sink = Mathf.Lerp(entry.sinkRange.x, entry.sinkRange.y, (float)rng.NextDouble());

                    placements.Add(new PlacementEntry
                    {
                        MapObjectGuid = ObjectPlacementMath.PickRandomGuid(entry.mapObjectGuids, rng),
                        WorldPosition = new Vector3(ox + dims.WorldOffsetX, height * dims.TerrainHeight, oz + dims.WorldOffsetZ),
                        Rotation = rot,
                        Scale = new Vector3(scale, yScale, scale),
                        Sink = sink,
                        Cluster = clusterInfo
                    });
                }
            }
        }
    }
}
