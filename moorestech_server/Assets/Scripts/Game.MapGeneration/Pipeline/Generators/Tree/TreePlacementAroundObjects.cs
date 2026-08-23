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
                // クラスタを組まない配置は樹木パッチの芯にならないので飛ばす
                // A placement without a cluster is no core for a tree patch, so it is skipped
                if (!obj.ClusterInfo.HasValue) continue;
                int cid = obj.ClusterInfo.Value.ClusterId;
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
                    // グループ化でクラスタ有りだけを残しているので、代表メンバーは必ず値を持つ
                    // Grouping kept only the clustered ones, so the representative member always has a value
                    var info = members[0].ClusterInfo.Value;
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

                            // パッチのマスクもワールド座標で引く。タイルローカルのままだと同じ絵が全タイルに出る
                            // The patch mask is world-space too; tile-local coordinates would print one picture on every tile
                            float worldX = tx + dims.WorldOffsetX;
                            float worldZ = tz + dims.WorldOffsetZ;
                            float mk = Mathf.PerlinNoise(worldX * proxCfg.maskCoarseFrequency + noiseOffX,
                                worldZ * proxCfg.maskCoarseFrequency + noiseOffZ);
                            float detail = Mathf.PerlinNoise(worldX * proxCfg.maskFineFrequency + noiseOffX + 77f,
                                worldZ * proxCfg.maskFineFrequency + noiseOffZ + 33f);
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

                            placements.Add(PlacementEntry.CreateTree(
                                TreePlacementCommon.PickRandomGuid(entry.mapObjectGuids, rng),
                                new Vector3(tx, height * dims.TerrainHeight, tz),
                                Quaternion.Euler(0, entry.randomRotation ? (float)rng.NextDouble() * 360f : 0f, 0),
                                new Vector3(scale, scale, scale),
                                entry.sink,
                                entry.terrainSurroundEffectType));
                            sharedGrid.Add(tx, tz);
                        }
                    }
                }
            }
            return placements;
        }
    }
}
