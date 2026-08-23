using System.Collections.Generic;
using Core.Master;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 旧バックボーンクラスター（clusterMode互換）。
    // ・リング毎にタイル面積×densityで中心数を決める（母数は非クラスタ散布と共通）
    // ・中心がリング内の候補のみ、公称の中心数まで採用
    // Legacy backbone clusters (clusterMode).
    // - Centre count per ring comes from the tile area times that ring's density (the same denominator as scatter mode)
    // - Keep centres inside the ring, up to the nominal centre count
    internal static class ObjectBackboneClusterPlacer
    {
        public static void Generate(
            BiomeObjectConfig.ObjectEntry entry, ObjectClusterParam cluster, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, Vector2[] noiseOffsets, List<PlacementEntry> placements,
            ObjectAlgorithmConfig objAlgCfg, ref int nextClusterId)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;
            float area = w * l;
            dims.SpawnDistanceRangeXz(out var tileNearestDistance, out var tileFarthestDistance);

            foreach (var ring in SpawnDistanceRingPlanner.BuildRings(cluster.bands))
            {
                // 中心数はタイル面積×density（1haあたり）で決める。母数を非クラスタ側と揃えないと、
                // 同じdensityでもモードによって近傍リングだけ0個になる。
                // The centre count is tile area times density (per hectare); using a different denominator from scatter mode
                // would zero out only the near rings for the very same density.
                int desiredCenters = Mathf.RoundToInt(ring.Band.clusterCentersPerHectare * area / 10000f);
                if (desiredCenters <= 0) continue;

                // タイルに掛からないリングは全中心が捨てられるだけなので、種だけ引いて飛ばす（乱数消費数＝出力を変えない）。
                // A ring that misses this tile would have every centre discarded, so draw the seed and skip (output and RNG consumption stay identical).
                if (!ring.OverlapsDistanceRange(tileNearestDistance, tileFarthestDistance))
                {
                    rng.Next();
                    continue;
                }

                float centerMinDist = Mathf.Sqrt(area / desiredCenters * objAlgCfg.clusterSpacingFactor);
                var centers = PoissonDiskSampler.Generate(w, l, centerMinDist, rng.Next());

                // Poissonはリング外の中心も返すため、採用数を数えて公称値で打ち切る。
                // Poisson also returns centres outside the ring, so count adoptions and stop at the nominal figure.
                int adoptedCenters = 0;
                foreach (var center in centers)
                {
                    if (desiredCenters <= adoptedCenters) break;

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

                    adoptedCenters++;
                    PlaceBackbone(entry, cluster, center, cx, cz, dims, heights, hRes, rng, placements, nextClusterId++);
                }
            }
        }

        // クラスタ中心から背骨状にメンバーを配置。
        // Lay members along a backbone from the cluster centre.
        static void PlaceBackbone(
            BiomeObjectConfig.ObjectEntry entry, ObjectClusterParam cluster, Vector2 center, int cx, int cz,
            TerrainDimensions dims, float[,] heights, int hRes, System.Random rng,
            List<PlacementEntry> placements, int clusterId)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;
            int boneCount = Mathf.Min(3 + rng.Next(3), cluster.objectsPerCluster);
            float backboneAngle = (float)rng.NextDouble() * Mathf.PI;
            float halfLen = cluster.clusterRadius * 0.5f;

            float centerWorldX = center.x + dims.WorldOffsetX;
            float centerWorldZ = center.y + dims.WorldOffsetZ;
            float centerHt = heights[cz, cx] * dims.TerrainHeight;
            var clusterInfo = new RockClusterInfo
            {
                ClusterId = clusterId,
                Center = new Vector3(centerWorldX, centerHt, centerWorldZ),
                HeroCenter = new Vector3(centerWorldX, centerHt, centerWorldZ),
                Angle = backboneAngle,
                Length = cluster.clusterRadius,
                FootprintRadius = cluster.clusterRadius
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

                placements.Add(PlacementEntry.CreateObject(
                    ObjectPlacementMath.PickRandomGuid(entry.mapObjectGuids, rng),
                    new Vector3(ox + dims.WorldOffsetX, height * dims.TerrainHeight, oz + dims.WorldOffsetZ),
                    rot, new Vector3(scale, yScale, scale), sink,
                    clusterInfo, entry.terrainSurroundEffectType));
            }
        }
    }
}
