using System.Collections.Generic;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Generators.Util;
using UnityEngine;

namespace MapGenerator.Pipeline.Generators
{
    /// <summary>
    /// 階層的オブジェクト配置: Primary(大岩) + 任意数の従属グループ(Ring/Saddle)を生成。
    /// Primaryは極座標クラスター、従属グループはPrimaryクラスターに従属して配置される。
    /// </summary>
    public static class ObjectPlacementGenerator
    {

        /// <summary>
        /// 1バイオーム分のオブジェクト配置を生成する。バイオームの概念を持たない純粋関数。
        /// maskがtrueの領域にのみ配置し、borderMarginで境界からの距離を制御する。
        /// </summary>
        public static List<PlacementEntry> GenerateForBiome(
            bool[,] mask,
            float[,] heights,
            TerrainDimensions dims,
            BiomeObjectConfig objConfig,
            System.Random rng,
            Util.SpatialGrid treeSpatialGrid = null)
        {
            var placements = new List<PlacementEntry>();
            int hRes = dims.Resolution;

            bool hasEntries = objConfig.entries != null && objConfig.entries.Length > 0;
            bool hasClusters = objConfig.clusterEntries != null && objConfig.clusterEntries.Length > 0;
            if (!hasEntries && !hasClusters) return placements;

            var objAlgCfg = objConfig.algorithmConfig ?? new ObjectAlgorithmConfig();
            float borderMarginPx = BiomeMaskBuilder.MetersToPixels(objConfig.borderMargin, dims.TerrainWidth, hRes);
            var noiseOffsets = ManagedNoise.GenerateOffsets(new System.Random(rng.Next()), 4);

            // ===== Phase A: clusterEntries =====
            var clusterInfos = new List<RockClusterInfo>();
            if (hasClusters)
            {
                foreach (var cluster in objConfig.clusterEntries)
                {
                    if (cluster.primary == null || cluster.primary.Length == 0) continue;
                    GeneratePrimaryClustersFromClusterEntry(cluster, dims, heights, hRes,
                        mask, borderMarginPx, rng, noiseOffsets, placements, clusterInfos,
                        treeSpatialGrid, objAlgCfg);
                    // 従属グループをモードに応じてディスパッチ
                    if (cluster.secondaries != null)
                    {
                        foreach (var sec in cluster.secondaries)
                        {
                            if (sec?.prefabs == null || sec.prefabs.Length == 0) continue;
                            switch (sec.mode)
                            {
                                case SecondaryPlacementMode.Ring:
                                    GenerateRingPlacement(sec, dims, heights, hRes,
                                        mask, borderMarginPx, rng, placements, clusterInfos,
                                        treeSpatialGrid, objAlgCfg);
                                    break;
                                case SecondaryPlacementMode.Saddle:
                                    GenerateSaddlePlacement(sec, dims, heights, hRes,
                                        mask, borderMarginPx, rng, placements, clusterInfos,
                                        treeSpatialGrid, objAlgCfg);
                                    break;
                            }
                        }
                    }
                }
            }

            // ===== Phase B: 独立散布エントリ =====
            if (hasEntries)
            {
                foreach (var entry in objConfig.entries)
                {
                    if (entry.prefabs == null || entry.prefabs.Length == 0 || entry.density <= 0.001f) continue;
                    if (entry.useClusterMode)
                        GenerateClusterObjects(entry, dims, heights, hRes,
                            mask, borderMarginPx, rng, noiseOffsets, placements, treeSpatialGrid, objAlgCfg);
                    else
                        GenerateIndependent(entry, dims, heights, hRes,
                            mask, borderMarginPx, rng, noiseOffsets, placements, treeSpatialGrid, objAlgCfg);
                }
            }

            return placements;
        }

        static int _nextClusterId;

        // =================================================================
        // Primary: 極座標クラスターで大岩を不規則な塊として配置
        // =================================================================
        static void GeneratePrimaryClusters(
            BiomeObjectConfig.ObjectEntry entry, TerrainGenerationConfig config,
            float[,] heights, int hRes, float[,] biomeWeights, int biomeIdx,
            System.Random rng, Vector2[] noiseOffsets,
            List<ObjectPlacementResult> placements, List<RockClusterInfo> clusterInfos,
            Util.SpatialGrid treeSpatialGrid)
        {
            float w = config.terrainWidth, l = config.terrainLength;
            float centerMinDist = Mathf.Sqrt(w * l / entry.clusterCount * 0.6f);
            var centers = PoissonDiskSampler.Generate(w, l, centerMinDist, rng.Next());

            int placed = 0;
            foreach (var center in centers)
            {
                if (placed >= entry.clusterCount) break;

                int cx = Mathf.Clamp(Mathf.RoundToInt(center.x / w * (hRes - 1)), 0, hRes - 1);
                int cz = Mathf.Clamp(Mathf.RoundToInt(center.y / l * (hRes - 1)), 0, hRes - 1);
                if (biomeWeights[cz * hRes + cx, 2 + biomeIdx] < 0.5f) continue;
                if (heights[cz, cx] < config.seaLevel + 0.03f) continue;

                if (entry.noiseType != MapNoiseType.None)
                {
                    float noise = ManagedNoise.SampleByType(entry.noiseType, center.x, center.y,
                        entry.noiseFrequency, noiseOffsets) * entry.noiseAmplitude;
                    if (noise < entry.noiseThreshold) continue;
                }

                placed++;
                int clusterId = _nextClusterId++;
                int memberCount = Mathf.Clamp(entry.objectsPerCluster, 1, 5);
                float radius = entry.clusterRadius;

                float centerWorldX = center.x + config.worldOffsetX;
                float centerWorldZ = center.y + config.worldOffsetZ;
                float centerHt = heights[cz, cx] * config.terrainHeight;

                // ヒーロー岩を中心付近に配置（中心からradius*0.15以内のオフセット）
                float heroOffX = ((float)rng.NextDouble() - 0.5f) * radius * 0.3f;
                float heroOffZ = ((float)rng.NextDouble() - 0.5f) * radius * 0.3f;
                float heroLocalX = center.x + heroOffX;
                float heroLocalZ = center.y + heroOffZ;

                // ヒーロー岩は最大スケール寄り
                float heroScale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y,
                    0.7f + (float)rng.NextDouble() * 0.3f);

                var heroWorldPos = new Vector3(
                    heroLocalX + config.worldOffsetX,
                    SampleHeight(heights, heroLocalX, heroLocalZ, w, l, hRes) * config.terrainHeight,
                    heroLocalZ + config.worldOffsetZ);

                float heroYRot = (float)rng.NextDouble() * 360f;
                var heroRot = Quaternion.Euler(0, heroYRot, 0);
                if (entry.slopeAlignment > 0.001f)
                    heroRot = ApplySlopeAlignment(heroRot, heights, heroLocalX, heroLocalZ,
                        w, l, hRes, config.terrainHeight, entry.slopeAlignment);

                float heroSink = Mathf.Lerp(entry.sinkRange.x, entry.sinkRange.y, (float)rng.NextDouble());

                var heroPrefab = PickRandomPrefab(entry.prefabs, rng);
                var heroMeshRadius = EstimateMeshRadius(heroPrefab, heroScale);
                placements.Add(new ObjectPlacementResult
                {
                    Prefab = heroPrefab,
                    Position = heroWorldPos,
                    Rotation = heroRot,
                    Scale = new Vector3(heroScale, heroScale * (0.7f + (float)rng.NextDouble() * 0.15f), heroScale),
                    Sink = heroSink,
                    MeshRadius = heroMeshRadius,
                    ClusterInfo = new RockClusterInfo
                    {
                        ClusterId = clusterId,
                        Center = new Vector3(centerWorldX, centerHt, centerWorldZ),
                        HeroCenter = heroWorldPos,
                        Angle = 0,
                        Length = radius,
                        FootprintRadius = radius
                    }
                });

                // 従属岩を極座標+角度rejection で配置（三角/くの字形を優先）
                var placedAngles = new List<float>();
                for (int i = 1; i < memberCount; i++)
                {
                    // 極座標: 半径はradius*0.35～radius*1.0
                    float dist = radius * (0.35f + (float)rng.NextDouble() * 0.65f);
                    // 角度: 既存の岩と60°以上離れた方向を選ぶ（最大10回リトライ）
                    float angle = 0f;
                    bool angleOk = false;
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                        angleOk = true;
                        foreach (var prevAngle in placedAngles)
                        {
                            float diff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, prevAngle * Mathf.Rad2Deg));
                            if (diff < 55f) { angleOk = false; break; }
                        }
                        if (angleOk) break;
                    }
                    placedAngles.Add(angle);

                    float ox = center.x + heroOffX + dist * Mathf.Cos(angle);
                    float oz = center.y + heroOffZ + dist * Mathf.Sin(angle);
                    if (ox < 0 || ox > w || oz < 0 || oz > l) continue;

                    if (treeSpatialGrid != null && entry.minDistanceFromTree > 0f &&
                        treeSpatialGrid.HasNeighborWithin(ox, oz, entry.minDistanceFromTree))
                        continue;

                    float ht = SampleHeight(heights, ox, oz, w, l, hRes);
                    if (ht < config.seaLevel + 0.03f) continue;

                    // 従属岩はヒーロー岩より小さく
                    float scale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y,
                        (float)rng.NextDouble() * 0.6f);
                    float yScale = scale * (0.5f + (float)rng.NextDouble() * 0.3f);
                    float yRot = (float)rng.NextDouble() * 360f;
                    var rot = Quaternion.Euler(0, yRot, 0);
                    if (entry.slopeAlignment > 0.001f)
                        rot = ApplySlopeAlignment(rot, heights, ox, oz, w, l, hRes,
                            config.terrainHeight, entry.slopeAlignment);

                    float sink = Mathf.Lerp(entry.sinkRange.x, entry.sinkRange.y, (float)rng.NextDouble());

                    var clusterInfo = new RockClusterInfo
                    {
                        ClusterId = clusterId,
                        Center = new Vector3(centerWorldX, centerHt, centerWorldZ),
                        HeroCenter = heroWorldPos,
                        Angle = angle,
                        Length = radius,
                        FootprintRadius = radius
                    };

                    var memberPrefab = PickRandomPrefab(entry.prefabs, rng);
                    placements.Add(new ObjectPlacementResult
                    {
                        Prefab = memberPrefab,
                        Position = new Vector3(ox + config.worldOffsetX, ht * config.terrainHeight, oz + config.worldOffsetZ),
                        Rotation = rot,
                        Scale = new Vector3(scale, yScale, scale),
                        Sink = sink,
                        MeshRadius = EstimateMeshRadius(memberPrefab, scale),
                        ClusterInfo = clusterInfo
                    });
                }

                // クラスター情報を記録（Secondary/Rubbleの従属配置で使用）
                clusterInfos.Add(new RockClusterInfo
                {
                    ClusterId = clusterId,
                    Center = new Vector3(centerWorldX, centerHt, centerWorldZ),
                    HeroCenter = heroWorldPos,
                    Angle = 0,
                    Length = radius,
                    FootprintRadius = radius
                });
            }
        }

        // =================================================================
        // Independent: 独立散布
        // =================================================================
        static void GenerateIndependent(
            BiomeObjectConfig.ObjectEntry entry, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, Vector2[] noiseOffsets,
            List<PlacementEntry> placements, Util.SpatialGrid treeSpatialGrid,
            ObjectAlgorithmConfig objAlgCfg)
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
                    float slope = ComputeSlopeAngle(heights, hx, hz, hRes, w, dims.TerrainHeight, l);
                    float sw = EvaluateSlopeFilter(slope, entry.slopeMin, entry.slopeMax, entry.slopeSmoothness);
                    if (sw <= 0f) continue;
                    if (sw < 1f && (float)rng.NextDouble() > sw) continue;
                }

                float scale = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y, (float)rng.NextDouble());
                float yRot = (float)rng.NextDouble() * 360f;
                var rot = Quaternion.Euler(0, yRot, 0);
                if (entry.slopeAlignment > 0.001f)
                    rot = ApplySlopeAlignment(rot, heights, point.x, point.y, w, l, hRes,
                        dims.TerrainHeight, entry.slopeAlignment);

                float sink = Mathf.Lerp(entry.sinkRange.x, entry.sinkRange.y, (float)rng.NextDouble());

                placements.Add(new PlacementEntry
                {
                    Prefab = PickRandomPrefab(entry.prefabs, rng),
                    WorldPosition = new Vector3(point.x + dims.WorldOffsetX, height * dims.TerrainHeight, point.y + dims.WorldOffsetZ),
                    Rotation = rot,
                    Scale = new Vector3(scale, scale, scale),
                    Sink = sink,
                    Cluster = new RockClusterInfo { ClusterId = -1 }
                });
            }
        }

        // 旧バックボーンクラスター（Independent+clusterMode互換）
        static void GenerateClusterObjects(
            BiomeObjectConfig.ObjectEntry entry, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, Vector2[] noiseOffsets, List<PlacementEntry> placements,
            Util.SpatialGrid treeSpatialGrid, ObjectAlgorithmConfig objAlgCfg)
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
                int clusterId = _nextClusterId++;
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
                        rot = ApplySlopeAlignment(rot, heights, ox, oz, w, l, hRes,
                            dims.TerrainHeight, entry.slopeAlignment);

                    float sink = Mathf.Lerp(entry.sinkRange.x, entry.sinkRange.y, (float)rng.NextDouble());

                    placements.Add(new PlacementEntry
                    {
                        Prefab = PickRandomPrefab(entry.prefabs, rng),
                        WorldPosition = new Vector3(ox + dims.WorldOffsetX, height * dims.TerrainHeight, oz + dims.WorldOffsetZ),
                        Rotation = rot,
                        Scale = new Vector3(scale, yScale, scale),
                        Sink = sink,
                        Cluster = clusterInfo
                    });
                }
            }
        }

        // =================================================================
        // ユーティリティ
        // =================================================================

        static float SampleHeight(float[,] heights, float localX, float localZ,
            float w, float l, int hRes)
        {
            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / w * (hRes - 1)), 0, hRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / l * (hRes - 1)), 0, hRes - 1);
            return heights[hz, hx];
        }

        static Quaternion ApplySlopeAlignment(Quaternion baseRot, float[,] heights,
            float localX, float localZ, float w, float l, int hRes,
            float terrainHeight, float alignment)
        {
            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / w * (hRes - 1)), 0, hRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / l * (hRes - 1)), 0, hRes - 1);
            var normal = ComputeSurfaceNormal(heights, hx, hz, hRes, w, terrainHeight, l);
            var slopeRot = Quaternion.FromToRotation(Vector3.up, normal);
            return Quaternion.Slerp(baseRot, slopeRot * baseRot, alignment);
        }

        // =================================================================
        // ClusterEntry版: Primary→Secondary→RubblePatch を ObjectClusterEntry から処理
        // =================================================================

        static void GeneratePrimaryClustersFromClusterEntry(
            ObjectClusterEntry cluster, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, Vector2[] noiseOffsets,
            List<PlacementEntry> placements, List<RockClusterInfo> clusterInfos,
            Util.SpatialGrid treeSpatialGrid, ObjectAlgorithmConfig objAlgCfg)
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
                int clusterId = _nextClusterId++;
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
                    SampleHeight(heights, heroLocalX, heroLocalZ, w, l, hRes) * dims.TerrainHeight,
                    heroLocalZ + dims.WorldOffsetZ);

                float heroYRot = (float)rng.NextDouble() * 360f;
                var heroRot = Quaternion.Euler(0, heroYRot, 0);
                if (cluster.slopeAlignment > 0.001f)
                    heroRot = ApplySlopeAlignment(heroRot, heights, heroLocalX, heroLocalZ,
                        w, l, hRes, dims.TerrainHeight, cluster.slopeAlignment);

                float heroSink = Mathf.Lerp(cluster.sinkRange.x, cluster.sinkRange.y, (float)rng.NextDouble());

                placements.Add(new PlacementEntry
                {
                    Prefab = PickRandomPrefab(cluster.primary, rng),
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

                    float ht = SampleHeight(heights, ox, oz, w, l, hRes);

                    float scale = Mathf.Lerp(cluster.scaleRange.x, cluster.scaleRange.y,
                        (float)rng.NextDouble() * objAlgCfg.subordinateScaleMaxRatio);
                    float yScale = scale * (objAlgCfg.subordinateYScaleMin +
                        (float)rng.NextDouble() * objAlgCfg.subordinateYScaleRange);
                    float yRot = (float)rng.NextDouble() * 360f;
                    var rot = Quaternion.Euler(0, yRot, 0);
                    if (cluster.slopeAlignment > 0.001f)
                        rot = ApplySlopeAlignment(rot, heights, ox, oz, w, l, hRes,
                            dims.TerrainHeight, cluster.slopeAlignment);

                    float sink = Mathf.Lerp(cluster.sinkRange.x, cluster.sinkRange.y, (float)rng.NextDouble());

                    placements.Add(new PlacementEntry
                    {
                        Prefab = PickRandomPrefab(cluster.primary, rng),
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

        // 環状配置: Primaryクラスター中心の周囲にランダム角度・距離で配置
        static void GenerateRingPlacement(
            ObjectClusterSecondary sec, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, List<PlacementEntry> placements,
            List<RockClusterInfo> clusterInfos, Util.SpatialGrid treeSpatialGrid,
            ObjectAlgorithmConfig objAlgCfg)
        {
            float w = dims.TerrainWidth, l = dims.TerrainLength;
            foreach (var info in clusterInfos)
            {
                float localCX = info.Center.x - dims.WorldOffsetX;
                float localCZ = info.Center.z - dims.WorldOffsetZ;
                for (int i = 0; i < sec.countPerCluster; i++)
                {
                    float dist = Mathf.Lerp(sec.minDistance,
                        sec.maxDistance, (float)rng.NextDouble());
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
                    float scale = Mathf.Lerp(sec.scaleRange.x,
                        sec.scaleRange.y, (float)rng.NextDouble());
                    float yRot = (float)rng.NextDouble() * 360f;
                    var rot = Quaternion.Euler(0, yRot, 0);
                    if (sec.slopeAlignment > 0.001f)
                        rot = ApplySlopeAlignment(rot, heights, ox, oz, w, l, hRes,
                            dims.TerrainHeight, sec.slopeAlignment);
                    float sink = Mathf.Lerp(sec.sinkRange.x,
                        sec.sinkRange.y, (float)rng.NextDouble());
                    placements.Add(new PlacementEntry
                    {
                        Prefab = PickRandomPrefab(sec.prefabs, rng),
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

        // サドル/偏り配置: Primaryメンバー間のサドルポイントや偏った方向にパッチ配置
        static void GenerateSaddlePlacement(
            ObjectClusterSecondary sec, TerrainDimensions dims,
            float[,] heights, int hRes, bool[,] mask, float borderMarginPx,
            System.Random rng, List<PlacementEntry> placements,
            List<RockClusterInfo> clusterInfos, Util.SpatialGrid treeSpatialGrid,
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
                        float patchDist = Mathf.Lerp(sec.minDistance,
                            sec.maxDistance, (float)rng.NextDouble());
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
                        float scale = Mathf.Lerp(sec.scaleRange.x,
                            sec.scaleRange.y, (float)rng.NextDouble()) *
                            (0.5f + falloff * 0.5f);
                        float yRot = (float)rng.NextDouble() * 360f;
                        float sink = Mathf.Lerp(sec.sinkRange.x,
                            sec.sinkRange.y, (float)rng.NextDouble());
                        placements.Add(new PlacementEntry
                        {
                            Prefab = PickRandomPrefab(sec.prefabs, rng),
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

        static GameObject PickRandomPrefab(GameObject[] prefabs, System.Random rng)
        {
            if (prefabs.Length == 1) return prefabs[0];
            int validCount = 0;
            foreach (var go in prefabs) if (go != null) validCount++;
            if (validCount <= 1)
            {
                foreach (var go in prefabs) if (go != null) return go;
                return null;
            }
            int pick = rng.Next(validCount);
            int seen = 0;
            foreach (var go in prefabs)
            {
                if (go == null) continue;
                if (seen == pick) return go;
                seen++;
            }
            return prefabs[0];
        }

        static float ComputeSlopeAngle(float[,] heights, int x, int z, int res,
            float terrainWidth, float terrainHeight, float terrainLength)
        {
            var normal = ComputeSurfaceNormal(heights, x, z, res, terrainWidth, terrainHeight, terrainLength);
            return Mathf.Acos(Mathf.Clamp01(normal.y)) * Mathf.Rad2Deg;
        }

        static float EvaluateSlopeFilter(float slope, float min, float max, float smoothness)
        {
            if (smoothness <= 0.001f)
                return (slope >= min && slope <= max) ? 1f : 0f;
            float low = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((slope - (min - smoothness)) / smoothness));
            float high = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(((max + smoothness) - slope) / smoothness));
            return low * high;
        }

        // プレハブのXZ平面での概算半径をスケール込みで算出
        static float EstimateMeshRadius(GameObject prefab, float scale)
        {
            if (prefab == null) return scale * 5f;
            var filters = prefab.GetComponentsInChildren<MeshFilter>();
            float maxExtent = 1f;
            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                float xz = Mathf.Max(b.extents.x, b.extents.z);
                if (xz > maxExtent) maxExtent = xz;
            }
            return maxExtent * scale;
        }

        static Vector3 ComputeSurfaceNormal(float[,] heights, int x, int z, int res,
            float terrainWidth, float terrainHeight, float terrainLength)
        {
            float h = heights[z, x];
            float hR = (x < res - 1) ? heights[z, x + 1] : h;
            float hU = (z < res - 1) ? heights[z + 1, x] : h;
            float cellX = terrainWidth / (res - 1);
            float cellZ = terrainLength / (res - 1);
            float dhdx = (hR - h) * terrainHeight / cellX;
            float dhdz = (hU - h) * terrainHeight / cellZ;
            return new Vector3(-dhdx, 1f, -dhdz).normalized;
        }
    }
}
