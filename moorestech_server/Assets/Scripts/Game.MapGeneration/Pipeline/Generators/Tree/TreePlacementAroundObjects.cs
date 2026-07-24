using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 岩クラスター周辺の樹木パッチ生成。各エントリの rockProximityConfig が有効なもののみ処理する。
    // Generates tree patches around rock clusters, only for entries whose rockProximityConfig is enabled.
    public static class TreePlacementAroundObjects
    {
        public static List<PlacementEntry> GenerateAroundObjects(
            bool[,] mask, float[] heights, TerrainDimensions dims,
            TreePlacementConfig treeConfig,
            List<ObjectPlacementResult> objectPlacements,
            System.Random rng,
            SpatialGrid sharedGrid)
        {
            var placements = new List<PlacementEntry>();
            if (treeConfig?.prototypes == null || objectPlacements == null || objectPlacements.Count == 0)
                return placements;
            int res = dims.Resolution;

            // クラスターごとにグルーピング
            // Group by cluster
            var clusterGroups = new Dictionary<int, List<ObjectPlacementResult>>();
            foreach (var obj in objectPlacements)
            {
                int cid = obj.ClusterInfo.ClusterId;
                if (cid < 0) continue;
                if (!clusterGroups.ContainsKey(cid))
                    clusterGroups[cid] = new List<ObjectPlacementResult>();
                clusterGroups[cid].Add(obj);
            }

            foreach (var entry in treeConfig.prototypes)
            {
                if (entry == null || entry.disabled || entry.mapObjectGuids == null) continue;
                var proxCfg = entry.rockProximityConfig;
                if (proxCfg == null || !proxCfg.enabled) continue;

                bool hasValid = false;
                foreach (var g in entry.mapObjectGuids) if (!string.IsNullOrEmpty(g)) { hasValid = true; break; }
                if (!hasValid) continue;

                float borderMarginPx = BiomeMaskBuilder.MetersToPixels(
                    entry.borderMargin, dims.TerrainWidth, res);

                foreach (var kvp in clusterGroups)
                {
                    var members = kvp.Value;
                    if (members.Count == 0) continue;
                    var info = members[0].ClusterInfo;
                    float centroidX = info.Center.x - dims.WorldOffsetX;
                    float centroidZ = info.Center.z - dims.WorldOffsetZ;

                    int cx = Mathf.Clamp(Mathf.RoundToInt(centroidX / dims.TerrainWidth * (res - 1)), 0, res - 1);
                    int cz = Mathf.Clamp(Mathf.RoundToInt(centroidZ / dims.TerrainLength * (res - 1)), 0, res - 1);
                    if (!mask[cz, cx]) continue;

                    float patchBaseAngle = info.Angle + Mathf.PI * 0.5f;
                    int patchCount = proxCfg.patchCountMin + rng.Next(proxCfg.patchCountRandom);

                    for (int p = 0; p < patchCount; p++)
                    {
                        float patchAngle = patchBaseAngle + ((float)rng.NextDouble() - 0.5f) * Mathf.Deg2Rad * 80f;
                        float patchDist = proxCfg.patchDistanceMin + (float)rng.NextDouble() * proxCfg.patchDistanceRandom;
                        float patchCX = centroidX + Mathf.Cos(patchAngle) * patchDist;
                        float patchCZ = centroidZ + Mathf.Sin(patchAngle) * patchDist;
                        float patchSize = proxCfg.patchSizeMin + (float)rng.NextDouble() * proxCfg.patchSizeRandom;
                        float noiseOffX = (float)rng.NextDouble() * 200f;
                        float noiseOffZ = (float)rng.NextDouble() * 200f;
                        float maskThreshold = proxCfg.maskThresholdMin + (float)rng.NextDouble() * proxCfg.maskThresholdRandom;
                        int attempts = proxCfg.attemptsMin + rng.Next(proxCfg.attemptsRandom);

                        for (int a = 0; a < attempts; a++)
                        {
                            float localAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
                            float localDist = Mathf.Sqrt((float)rng.NextDouble()) * patchSize;
                            float tx = patchCX + Mathf.Cos(localAngle) * localDist;
                            float tz = patchCZ + Mathf.Sin(localAngle) * localDist;
                            float distFromCenter = localDist / patchSize;

                            float mk = Mathf.PerlinNoise(tx * proxCfg.maskCoarseFrequency + noiseOffX,
                                tz * proxCfg.maskCoarseFrequency + noiseOffZ);
                            float detail = Mathf.PerlinNoise(tx * proxCfg.maskFineFrequency + noiseOffX + 77f,
                                tz * proxCfg.maskFineFrequency + noiseOffZ + 33f);
                            float combined = mk * proxCfg.maskCoarseWeight + detail * (1f - proxCfg.maskCoarseWeight);
                            float distPenalty = distFromCenter * distFromCenter * proxCfg.distancePenaltyFactor;
                            if (combined - distPenalty < maskThreshold) continue;

                            if (tx < 0 || tx > dims.TerrainWidth || tz < 0 || tz > dims.TerrainLength) continue;
                            if (!TreePlacementCommon.CheckMask(mask, new Vector2(tx, tz), dims, res, borderMarginPx)) continue;

                            if (sharedGrid.HasNeighborWithin(tx, tz, entry.sharedGridMinDistance)) continue;

                            int hx = Mathf.Clamp(Mathf.RoundToInt(tx / dims.TerrainWidth * (res - 1)), 0, res - 1);
                            int hz = Mathf.Clamp(Mathf.RoundToInt(tz / dims.TerrainLength * (res - 1)), 0, res - 1);
                            float height = heights[hz * res + hx];

                            float scale = Mathf.Lerp(
                                proxCfg.scaleLowBase + (float)rng.NextDouble() * proxCfg.scaleLowRange,
                                proxCfg.scaleHighBase + (float)rng.NextDouble() * proxCfg.scaleHighRange,
                                combined);

                            placements.Add(new PlacementEntry
                            {
                                MapObjectGuid = TreePlacementCommon.PickRandomGuid(entry.mapObjectGuids, rng),
                                WorldPosition = new Vector3(tx, height * dims.TerrainHeight, tz),
                                Rotation = Quaternion.Euler(0,
                                    entry.randomRotation ? (float)rng.NextDouble() * 360f : 0f, 0),
                                Scale = new Vector3(scale, scale, scale),
                                Sink = entry.sink
                            });
                            sharedGrid.Add(tx, tz);
                        }
                    }
                }
            }
            return placements;
        }
    }
}
