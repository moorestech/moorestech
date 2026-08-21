using System.Collections.Generic;
using Core.Master;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 旧バックボーンクラスター（clusterMode互換）。
    // ・リング毎にリング面積×densityで中心数を決める
    // ・中心がリング内の候補のみ採用
    // Legacy backbone clusters (clusterMode).
    // - Centre count per ring comes from the ring area times that ring's density
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

            foreach (var ring in SpawnDistanceRingPlanner.BuildRings(SpawnDistanceBand.OuterRadiiOf(entry.bands)))
            {
                var band = entry.bands[ring.BandIndex];

                // 中心数はリング面積×density（1haあたり）で決める。面積非依存の個数だと近傍リングでほぼ0個になるため。
                // The centre count is ring area times density (per hectare); an area-independent count would yield almost none in a near ring.
                float ringArea = RingAreaWithinTile(ring);
                int desiredCenters = Mathf.RoundToInt(band.density * ringArea / 10000f);
                if (desiredCenters <= 0) continue;

                // Poissonはタイル全面に撒いてリングで絞るため、間隔はリング面積あたりdesiredCenters個になる密度で決める。
                // Poisson covers the whole tile and is then filtered by the ring, so the spacing targets desiredCenters points per ring area.
                float centerMinDist = Mathf.Sqrt(ringArea / desiredCenters * objAlgCfg.clusterSpacingFactor);
                var centers = PoissonDiskSampler.Generate(w, l, centerMinDist, rng.Next());

                foreach (var center in centers)
                {
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

                    PlaceBackbone(entry, center, cx, cz, dims, heights, hRes, rng, placements, nextClusterId++);
                }
            }

            #region Internal

            // リングとタイル矩形の交差面積は解析が重いため、円環面積とタイル面積の小さい方で近似する。
            // The exact ring-tile intersection is heavy to derive, so approximate it with the smaller of the annulus area and the tile area.
            float RingAreaWithinTile(SpawnDistanceRing targetRing)
            {
                if (float.IsPositiveInfinity(targetRing.Outer)) return w * l;
                float annulusArea = Mathf.PI * (targetRing.Outer * targetRing.Outer - targetRing.Inner * targetRing.Inner);
                return Mathf.Min(annulusArea, w * l);
            }

            #endregion
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
