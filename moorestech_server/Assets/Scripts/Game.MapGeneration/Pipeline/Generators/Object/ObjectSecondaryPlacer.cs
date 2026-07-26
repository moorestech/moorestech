using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 従属グループ配置: Ring(環状) と Saddle(サドル/偏り) の2モード。
    // Subordinate group placement: Ring (annular) and Saddle (saddle/biased) modes.
    internal static class ObjectSecondaryPlacer
    {
        public static void GenerateRingPlacement(
            ObjectClusterSecondary sec, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, List<PlacementEntry> placements,
            List<RockClusterInfo> clusterInfos, SpatialGrid treeSpatialGrid)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;
            foreach (var info in clusterInfos)
            {
                float localCX = info.Center.x - dims.WorldOffsetX;
                float localCZ = info.Center.z - dims.WorldOffsetZ;
                for (int i = 0; i < sec.countPerCluster; i++)
                {
                    float dist = Mathf.Lerp(sec.minDistance, sec.maxDistance, (float)rng.NextDouble());
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float ox = localCX + dist * Mathf.Cos(angle);
                    float oz = localCZ + dist * Mathf.Sin(angle);
                    if (ox < 0 || ox > w || oz < 0 || oz > l) continue;
                    int hx = Mathf.Clamp(Mathf.RoundToInt(ox / w * (hRes - 1)), 0, hRes - 1);
                    int hz = Mathf.Clamp(Mathf.RoundToInt(oz / l * (hRes - 1)), 0, hRes - 1);
                    if (!mask[hz, hx] || BiomeMaskBuilder.IsNearMaskEdge(mask, hx, hz, hRes, borderMarginPx)) continue;
                    float ht = heights[hz, hx];
                    if (treeSpatialGrid != null && sec.minDistanceFromTree > 0f &&
                        treeSpatialGrid.HasNeighborWithin(ox, oz, sec.minDistanceFromTree))
                        continue;
                    float scale = Mathf.Lerp(sec.scaleRange.x, sec.scaleRange.y, (float)rng.NextDouble());
                    float yRot = (float)rng.NextDouble() * 360f;
                    var rot = Quaternion.Euler(0, yRot, 0);
                    if (sec.slopeAlignment > 0.001f)
                        rot = ObjectPlacementMath.ApplySlopeAlignment(rot, heights, ox, oz, w, l, hRes,
                            dims.TerrainHeight, sec.slopeAlignment);
                    float sink = Mathf.Lerp(sec.sinkRange.x, sec.sinkRange.y, (float)rng.NextDouble());
                    placements.Add(new PlacementEntry
                    {
                        MapObjectGuid = ObjectPlacementMath.PickRandomGuid(sec.mapObjectGuids, rng),
                        WorldPosition = new Vector3(ox + dims.WorldOffsetX,
                            ht * dims.TerrainHeight, oz + dims.WorldOffsetZ),
                        Rotation = rot,
                        Scale = new Vector3(scale, scale, scale),
                        Sink = sink,
                        Cluster = info
                    });
                }
            }
        }

        public static void GenerateSaddlePlacement(
            ObjectClusterSecondary sec, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, List<PlacementEntry> placements,
            List<RockClusterInfo> clusterInfos, SpatialGrid treeSpatialGrid,
            ObjectAlgorithmConfig objAlgCfg)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;
            var clusterMembers = new Dictionary<int, List<Vector3>>();
            foreach (var p in placements)
            {
                var ci = p.Cluster ?? new RockClusterInfo { ClusterId = -1 };
                if (ci.ClusterId < 0) continue;
                if (!clusterMembers.ContainsKey(ci.ClusterId))
                    clusterMembers[ci.ClusterId] = new List<Vector3>();
                clusterMembers[ci.ClusterId].Add(p.WorldPosition);
            }
            foreach (var info in clusterInfos)
            {
                List<Vector3> members;
                if (!clusterMembers.TryGetValue(info.ClusterId, out members) || members.Count == 0)
                    continue;
                float clusterBiasAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
                for (int i = 0; i < sec.countPerCluster; i++)
                {
                    var anchor = members[rng.Next(members.Count)];
                    float anchorLocalX = anchor.x - dims.WorldOffsetX;
                    float anchorLocalZ = anchor.z - dims.WorldOffsetZ;
                    bool useSaddle = members.Count >= 2 &&
                        rng.NextDouble() < objAlgCfg.saddleProbability;
                    float patchCX, patchCZ;
                    if (useSaddle)
                    {
                        var other = members[rng.Next(members.Count)];
                        float midX = (anchor.x + other.x) * 0.5f - dims.WorldOffsetX;
                        float midZ = (anchor.z + other.z) * 0.5f - dims.WorldOffsetZ;
                        patchCX = midX + ((float)rng.NextDouble() - 0.5f) * objAlgCfg.saddleJitter;
                        patchCZ = midZ + ((float)rng.NextDouble() - 0.5f) * objAlgCfg.saddleJitter;
                    }
                    else
                    {
                        float patchDist = Mathf.Lerp(sec.minDistance, sec.maxDistance, (float)rng.NextDouble());
                        float patchAngle = clusterBiasAngle +
                            ((float)rng.NextDouble() - 0.5f) * Mathf.PI * objAlgCfg.biasSectorAngle;
                        patchCX = anchorLocalX + patchDist * Mathf.Cos(patchAngle);
                        patchCZ = anchorLocalZ + patchDist * Mathf.Sin(patchAngle);
                    }
                    float sizeVariation = objAlgCfg.rubbleSizeMin +
                        (float)rng.NextDouble() * objAlgCfg.rubbleSizeRange;
                    float patchRadius = sec.clusterRadius * sizeVariation;
                    int piecesPerPatch = Mathf.RoundToInt(
                        sec.density * objAlgCfg.rubbleDensityMultiplier * sizeVariation);
                    for (int j = 0; j < piecesPerPatch; j++)
                    {
                        float r = patchRadius * Mathf.Sqrt((float)rng.NextDouble()) * 0.8f;
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                        float ox = patchCX + r * Mathf.Cos(a);
                        float oz = patchCZ + r * Mathf.Sin(a);
                        if (ox < 0 || ox > w || oz < 0 || oz > l) continue;
                        int hx = Mathf.Clamp(Mathf.RoundToInt(ox / w * (hRes - 1)), 0, hRes - 1);
                        int hz = Mathf.Clamp(Mathf.RoundToInt(oz / l * (hRes - 1)), 0, hRes - 1);
                        if (!mask[hz, hx] || BiomeMaskBuilder.IsNearMaskEdge(mask, hx, hz, hRes, borderMarginPx))
                            continue;
                        float ht = heights[hz, hx];
                        float distFromCenter = Mathf.Sqrt(
                            (ox - patchCX) * (ox - patchCX) + (oz - patchCZ) * (oz - patchCZ));
                        float falloff = 1f - Mathf.Clamp01(distFromCenter / patchRadius);
                        float scale = Mathf.Lerp(sec.scaleRange.x, sec.scaleRange.y, (float)rng.NextDouble()) *
                            (0.5f + falloff * 0.5f);
                        float yRot = (float)rng.NextDouble() * 360f;
                        float sink = Mathf.Lerp(sec.sinkRange.x, sec.sinkRange.y, (float)rng.NextDouble());
                        placements.Add(new PlacementEntry
                        {
                            MapObjectGuid = ObjectPlacementMath.PickRandomGuid(sec.mapObjectGuids, rng),
                            WorldPosition = new Vector3(ox + dims.WorldOffsetX,
                                ht * dims.TerrainHeight, oz + dims.WorldOffsetZ),
                            Rotation = Quaternion.Euler(0, yRot, 0),
                            Scale = new Vector3(scale, scale, scale),
                            Sink = sink,
                            Cluster = info
                        });
                    }
                }
            }
        }
    }
}
