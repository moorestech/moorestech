using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 旧バックボーンクラスター（clusterMode互換）。
    // ・リング毎にclusterCountを上限に中心を選ぶ
    // ・中心がリング内の候補のみ採用
    // Legacy backbone clusters (clusterMode).
    // - Pick centres per ring, capped at that ring's clusterCount
    // - Keep only centres inside the ring
    internal static class ObjectBackboneClusterPlacer
    {
        public static void Generate(
            BiomeObjectConfig.ObjectEntry entry, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, Vector2[] noiseOffsets, List<PlacementEntry> placements,
            ObjectAlgorithmConfig objAlgCfg, ref int nextClusterId)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;

            foreach (var ring in SpawnDistanceRingPlanner.BuildRings(ObjectScatterBand.OuterRadiiOf(entry.bands)))
            {
                var band = entry.bands[ring.BandIndex];
                if (band.clusterCount <= 0) continue;
                float centerMinDist = Mathf.Sqrt(w * l / band.clusterCount * objAlgCfg.clusterSpacingFactor);
                var centers = PoissonDiskSampler.Generate(w, l, centerMinDist, rng.Next());

                int placed = 0;
                foreach (var center in centers)
                {
                    if (band.clusterCount <= placed) break;

                    // リング判定はクラスタ中心のワールド座標距離（鉱脈 OreEntryPlacer と同じ）。
                    // The ring test uses the cluster centre's world-space distance (as in OreEntryPlacer).
                    if (!ring.Contains(dims.DistanceFromSpawnXz(center.x, center.y))) continue;

                    int cx = Mathf.Clamp(Mathf.RoundToInt(center.x / w * (hRes - 1)), 0, hRes - 1);
                    int cz = Mathf.Clamp(Mathf.RoundToInt(center.y / l * (hRes - 1)), 0, hRes - 1);
                    if (!mask[cz, cx] || BiomeMaskBuilder.IsNearMaskEdge(mask, cx, cz, hRes, borderMarginPx)) continue;

                    if (entry.noiseType != MapNoiseType.None)
                    {
                        float noise = ManagedNoise.SampleByType(entry.noiseType,
                            center.x + dims.WorldOffsetX, center.y + dims.WorldOffsetZ,
                            entry.noiseFrequency, noiseOffsets) * entry.noiseAmplitude;
                        if (noise < entry.noiseThreshold) continue;
                    }

                    placed++;
                    PlaceBackbone(entry, center, cx, cz, dims, heights, hRes, rng, placements, nextClusterId++);
                }
            }
        }

        // クラスタ中心から背骨状にメンバーを配置。
        // Lay members along a backbone from the cluster centre.
        static void PlaceBackbone(
            BiomeObjectConfig.ObjectEntry entry, Vector2 center, int cx, int cz,
            TerrainDimensions dims, float[,] heights, int hRes, System.Random rng,
            List<PlacementEntry> placements, int clusterId)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;
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
                if (ox < 0 || w < ox || oz < 0 || l < oz) continue;

                int hx = Mathf.Clamp(Mathf.RoundToInt(ox / w * (hRes - 1)), 0, hRes - 1);
                int hz = Mathf.Clamp(Mathf.RoundToInt(oz / l * (hRes - 1)), 0, hRes - 1);
                float height = heights[hz, hx];

                float scale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y, (float)rng.NextDouble());
                float yScale = i == 0
                    ? scale * (0.65f + (float)rng.NextDouble() * 0.15f)
                    : scale * (0.45f + (float)rng.NextDouble() * 0.25f);
                float yRotDeg = backboneAngle * Mathf.Rad2Deg + ((float)rng.NextDouble() - 0.5f) * 30f;
                var rot = Quaternion.Euler(0, yRotDeg, 0);
                if (0.001f < entry.slopeAlignment)
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
