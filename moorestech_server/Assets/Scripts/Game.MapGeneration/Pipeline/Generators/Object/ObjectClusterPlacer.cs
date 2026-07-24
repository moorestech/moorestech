using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // ClusterEntry の Primary(大岩)クラスターを極座標展開で配置する。ヒーロー岩＋角度 rejection の従属岩。
    // Places the Primary rock cluster of a ClusterEntry via polar expansion: a hero rock plus
    // subordinate rocks selected with angular rejection.
    internal static class ObjectClusterPlacer
    {
        public static void GeneratePrimaryClusters(
            ObjectClusterEntry cluster, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, Vector2[] noiseOffsets,
            List<PlacementEntry> placements, List<RockClusterInfo> clusterInfos,
            SpatialGrid treeSpatialGrid, ObjectAlgorithmConfig objAlgCfg, ref int nextClusterId)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;
            float centerMinDist = Mathf.Sqrt(w * l / cluster.clusterCount * objAlgCfg.clusterSpacingFactor);
            var centers = PoissonDiskSampler.Generate(w, l, centerMinDist, rng.Next());

            int placed = 0;
            foreach (var center in centers)
            {
                if (placed >= cluster.clusterCount) break;

                int cx = Mathf.Clamp(Mathf.RoundToInt(center.x / w * (hRes - 1)), 0, hRes - 1);
                int cz = Mathf.Clamp(Mathf.RoundToInt(center.y / l * (hRes - 1)), 0, hRes - 1);
                if (!mask[cz, cx] || BiomeMaskBuilder.IsNearMaskEdge(mask, cx, cz, hRes, borderMarginPx)) continue;

                if (cluster.noiseType != MapNoiseType.None)
                {
                    float noise = ManagedNoise.SampleByType(cluster.noiseType, center.x, center.y,
                        cluster.noiseFrequency, noiseOffsets) * cluster.noiseAmplitude;
                    if (noise < cluster.noiseThreshold) continue;
                }

                placed++;
                int clusterId = nextClusterId++;
                int memberCount = Mathf.Clamp(cluster.objectsPerCluster, 1, 5);
                float radius = cluster.clusterRadius;

                float centerWorldX = center.x + dims.WorldOffsetX;
                float centerWorldZ = center.y + dims.WorldOffsetZ;
                float centerHt = heights[cz, cx] * dims.TerrainHeight;

                float heroOffX = ((float)rng.NextDouble() - 0.5f) * radius * objAlgCfg.heroOffsetFactor;
                float heroOffZ = ((float)rng.NextDouble() - 0.5f) * radius * objAlgCfg.heroOffsetFactor;
                float heroLocalX = center.x + heroOffX;
                float heroLocalZ = center.y + heroOffZ;

                float heroScale = Mathf.Lerp(cluster.scaleRange.x, cluster.scaleRange.y,
                    objAlgCfg.heroScaleMinRatio + (float)rng.NextDouble() * objAlgCfg.heroScaleRange);

                var heroWorldPos = new Vector3(
                    heroLocalX + dims.WorldOffsetX,
                    ObjectPlacementMath.SampleHeight(heights, heroLocalX, heroLocalZ, w, l, hRes) * dims.TerrainHeight,
                    heroLocalZ + dims.WorldOffsetZ);

                float heroYRot = (float)rng.NextDouble() * 360f;
                var heroRot = Quaternion.Euler(0, heroYRot, 0);
                if (cluster.slopeAlignment > 0.001f)
                    heroRot = ObjectPlacementMath.ApplySlopeAlignment(heroRot, heights, heroLocalX, heroLocalZ,
                        w, l, hRes, dims.TerrainHeight, cluster.slopeAlignment);

                float heroSink = Mathf.Lerp(cluster.sinkRange.x, cluster.sinkRange.y, (float)rng.NextDouble());

                placements.Add(new PlacementEntry
                {
                    MapObjectGuid = ObjectPlacementMath.PickRandomGuid(cluster.primary, rng),
                    WorldPosition = heroWorldPos,
                    Rotation = heroRot,
                    Scale = new Vector3(heroScale,
                        heroScale * (objAlgCfg.heroYScaleMin + (float)rng.NextDouble() * objAlgCfg.heroYScaleRange),
                        heroScale),
                    Sink = heroSink,
                    Cluster = new RockClusterInfo
                    {
                        ClusterId = clusterId,
                        Center = new Vector3(centerWorldX, centerHt, centerWorldZ),
                        HeroCenter = heroWorldPos,
                        Angle = 0, Length = radius, FootprintRadius = radius
                    }
                });

                var placedAngles = new List<float>();
                for (int i = 1; i < memberCount; i++)
                {
                    float dist = radius * (objAlgCfg.subordinateDistMin +
                        (float)rng.NextDouble() * objAlgCfg.subordinateDistRange);
                    float angle = 0f;
                    bool angleOk = false;
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                        angleOk = true;
                        foreach (var prevAngle in placedAngles)
                        {
                            float diff = Mathf.Abs(Mathf.DeltaAngle(
                                angle * Mathf.Rad2Deg, prevAngle * Mathf.Rad2Deg));
                            if (diff < objAlgCfg.subordinateAngleReject) { angleOk = false; break; }
                        }
                        if (angleOk) break;
                    }
                    placedAngles.Add(angle);

                    float ox = center.x + heroOffX + dist * Mathf.Cos(angle);
                    float oz = center.y + heroOffZ + dist * Mathf.Sin(angle);
                    if (ox < 0 || ox > w || oz < 0 || oz > l) continue;

                    if (treeSpatialGrid != null && cluster.minDistanceFromTree > 0f &&
                        treeSpatialGrid.HasNeighborWithin(ox, oz, cluster.minDistanceFromTree))
                        continue;

                    float ht = ObjectPlacementMath.SampleHeight(heights, ox, oz, w, l, hRes);

                    float scale = Mathf.Lerp(cluster.scaleRange.x, cluster.scaleRange.y,
                        (float)rng.NextDouble() * objAlgCfg.subordinateScaleMaxRatio);
                    float yScale = scale * (objAlgCfg.subordinateYScaleMin +
                        (float)rng.NextDouble() * objAlgCfg.subordinateYScaleRange);
                    float yRot = (float)rng.NextDouble() * 360f;
                    var rot = Quaternion.Euler(0, yRot, 0);
                    if (cluster.slopeAlignment > 0.001f)
                        rot = ObjectPlacementMath.ApplySlopeAlignment(rot, heights, ox, oz, w, l, hRes,
                            dims.TerrainHeight, cluster.slopeAlignment);

                    float sink = Mathf.Lerp(cluster.sinkRange.x, cluster.sinkRange.y, (float)rng.NextDouble());

                    placements.Add(new PlacementEntry
                    {
                        MapObjectGuid = ObjectPlacementMath.PickRandomGuid(cluster.primary, rng),
                        WorldPosition = new Vector3(ox + dims.WorldOffsetX,
                            ht * dims.TerrainHeight, oz + dims.WorldOffsetZ),
                        Rotation = rot,
                        Scale = new Vector3(scale, yScale, scale),
                        Sink = sink,
                        Cluster = new RockClusterInfo
                        {
                            ClusterId = clusterId,
                            Center = new Vector3(centerWorldX, centerHt, centerWorldZ),
                            HeroCenter = heroWorldPos,
                            Angle = angle, Length = radius, FootprintRadius = radius
                        }
                    });
                }

                clusterInfos.Add(new RockClusterInfo
                {
                    ClusterId = clusterId,
                    Center = new Vector3(centerWorldX, centerHt, centerWorldZ),
                    HeroCenter = heroWorldPos,
                    Angle = 0, Length = radius, FootprintRadius = radius
                });
            }
        }
    }
}
