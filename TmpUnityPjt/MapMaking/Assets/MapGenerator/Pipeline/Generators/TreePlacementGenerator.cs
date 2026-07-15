using System.Collections.Generic;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Generators.Util;
using MapGenerator.Pipeline.Jobs;
using Unity.Collections;
using UnityEngine;

namespace MapGenerator.Pipeline.Generators
{
    /// <summary>
    /// プロトタイプ独立型の樹木配置。各TreePrototypeEntryが独自のdensityConfigで
    /// 4パスPoisson Diskサンプリングを実行し、共有SpatialGridで重なりを防止する。
    /// </summary>
    public static class TreePlacementGenerator
    {

        /// <summary>
        /// 1バイオーム分の樹木配置を生成する。各プロトタイプが独立した配置パイプラインを持つ。
        /// </summary>
        public static List<PlacementEntry> GenerateForBiome(
            bool[,] mask,
            float[] heights,
            TerrainDimensions dims,
            TreePlacementConfig treeConfig,
            System.Random rng)
        {
            var placements = new List<PlacementEntry>();
            if (treeConfig?.prototypes == null) return placements;
            int res = dims.Resolution;

            // エントリ間の重なり防止用SpatialGrid
            var sharedGrid = new SpatialGrid(dims.TerrainWidth, dims.TerrainLength, 3f);
            var curvatureMap = ComputeCurvatureMap(heights, res);
            var nativeHeights = new NativeArray<float>(heights, Allocator.Temp);

            // バイオーム全体のPrefab数とエントリ数を先に算出。
            // 旧システムと同じPoisson候補密度を維持するため、全体のtotalDesiredをエントリ数で等分する
            int biomeTotalPrefabs = 0;
            int activeEntryCount = 0;
            foreach (var e in treeConfig.prototypes)
            {
                if (e == null || e.disabled || e.prefabs == null) continue;
                bool valid = false;
                foreach (var go in e.prefabs) if (go != null) { valid = true; biomeTotalPrefabs++; }
                if (valid) activeEntryCount++;
            }
            int biomeDesired = biomeTotalPrefabs * 1500;

            try
            {
                foreach (var entry in treeConfig.prototypes)
                {
                    if (entry == null || entry.disabled || entry.prefabs == null) continue;
                    bool hasValid = false;
                    foreach (var go in entry.prefabs) if (go != null) { hasValid = true; break; }
                    if (!hasValid) continue;

                    var densityCfg = entry.densityConfig ?? new TreeDensityConfig();
                    var uCfg = entry.understoryConfig ?? new UnderstoryConfig();
                    float borderMarginPx = BiomeMaskBuilder.MetersToPixels(
                        entry.borderMargin, dims.TerrainWidth, res);

                    // Poisson候補数をエントリ数で等分し、旧システムと同等の全体密度を維持
                    int totalDesired = activeEntryCount > 0 ? biomeDesired / activeEntryCount : 0;
                    if (totalDesired == 0) continue;

                    float area = dims.TerrainWidth * dims.TerrainLength;
                    var densityOffsets = ManagedNoise.GenerateOffsets(new System.Random(dims.Seed + 500), 4);
                    var detailOffsets = ManagedNoise.GenerateOffsets(new System.Random(dims.Seed + 600), 4);
                    var islandOffsets = ManagedNoise.GenerateOffsets(new System.Random(dims.Seed + 700), 4);
                    var noiseOffsets = ManagedNoise.GenerateOffsets(new System.Random(rng.Next()), 8);

                    // 旧システムでは1つのPoissonで全エントリ分を生成し、各ポイントで1/K確率で選択していた。
                    // 独立化では各エントリが個別Poissonを回すため、最小距離をsqrt(K)倍して1/K密度に調整
                    float distScale = Mathf.Sqrt(activeEntryCount);
                    int beforeCount = placements.Count;

                    // ===== Pass 1 (Dense) =====
                    float denseMinDist = Mathf.Max(densityCfg.densePassMinDistance * distScale,
                        Mathf.Sqrt(area / (totalDesired * densityCfg.densePassMultiplier) * 0.8f));
                    foreach (var point in PoissonDiskSampler.Generate(
                        dims.TerrainWidth, dims.TerrainLength, denseMinDist, rng.Next()))
                    {
                        if (!CheckMask(mask, point, dims, res, borderMarginPx)) continue;
                        float dn = SampleDensityNoise(point.x, point.y,
                            densityOffsets, detailOffsets, islandOffsets, densityCfg);
                        if (dn < densityCfg.denseMinThreshold) continue;
                        TryPlaceEntry(entry, point, dims, heights, curvatureMap, nativeHeights,
                            noiseOffsets, densityCfg, sharedGrid, rng, placements);
                    }

                    // ===== Pass 2 (Transition) =====
                    float transMinDist = Mathf.Max(densityCfg.transitionPassMinDistance * distScale,
                        Mathf.Sqrt(area / (totalDesired * densityCfg.transitionPassMultiplier) * 0.8f));
                    foreach (var point in PoissonDiskSampler.Generate(
                        dims.TerrainWidth, dims.TerrainLength, transMinDist, rng.Next()))
                    {
                        if (!CheckMask(mask, point, dims, res, borderMarginPx)) continue;
                        float dn = SampleDensityNoise(point.x, point.y,
                            densityOffsets, detailOffsets, islandOffsets, densityCfg);
                        if (dn >= densityCfg.denseMinThreshold || dn < densityCfg.transitionMinThreshold)
                            continue;
                        float transRatio = (dn - densityCfg.transitionMinThreshold)
                                         / (densityCfg.denseMinThreshold - densityCfg.transitionMinThreshold);
                        float transProb = densityCfg.transitionBaseProb
                            + Mathf.Pow(transRatio, densityCfg.transitionProbPower) * densityCfg.transitionPeakProb;
                        if ((float)rng.NextDouble() > transProb) continue;
                        TryPlaceEntry(entry, point, dims, heights, curvatureMap, nativeHeights,
                            noiseOffsets, densityCfg, sharedGrid, rng, placements);
                    }

                    // ===== Pass 3 (Sparse) =====
                    float sparseMinDist = Mathf.Max(densityCfg.sparsePassMinDistance * distScale,
                        Mathf.Sqrt(area / (totalDesired * densityCfg.sparsePassMultiplier) * 0.8f));
                    foreach (var point in PoissonDiskSampler.Generate(
                        dims.TerrainWidth, dims.TerrainLength, sparseMinDist, rng.Next()))
                    {
                        if (!CheckMask(mask, point, dims, res, borderMarginPx)) continue;
                        float dn = SampleDensityNoise(point.x, point.y,
                            densityOffsets, detailOffsets, islandOffsets, densityCfg);
                        if (dn >= densityCfg.transitionMinThreshold) continue;
                        float openRatio = 1f - dn / densityCfg.transitionMinThreshold;
                        if ((float)rng.NextDouble() < openRatio * densityCfg.sparseOpenRejectFactor) continue;
                        TryPlaceEntry(entry, point, dims, heights, curvatureMap, nativeHeights,
                            noiseOffsets, densityCfg, sharedGrid, rng, placements);
                    }

                    // ===== Pass 4 (Scatter) =====
                    foreach (var point in PoissonDiskSampler.Generate(
                        dims.TerrainWidth, dims.TerrainLength,
                        densityCfg.scatterPassMinDistance * distScale, rng.Next()))
                    {
                        if (!CheckMask(mask, point, dims, res, borderMarginPx)) continue;
                        float dn = SampleDensityNoise(point.x, point.y,
                            densityOffsets, detailOffsets, islandOffsets, densityCfg);
                        if (dn >= densityCfg.transitionMinThreshold) continue;
                        float scatterProb = dn / densityCfg.transitionMinThreshold
                            * densityCfg.scatterDensityFactor + densityCfg.scatterBaseProb;
                        if ((float)rng.NextDouble() > scatterProb) continue;
                        TryPlaceEntry(entry, point, dims, heights, curvatureMap, nativeHeights,
                            noiseOffsets, densityCfg, sharedGrid, rng, placements);
                    }

                    // ===== Understory（自己クラスター方式）=====
                    // canopy木の周辺に同prefabsを小スケールで配置
                    if (uCfg.understoryScaleThreshold > 0f)
                    {
                        AddSelfUnderstory(placements, beforeCount, mask, dims, heights,
                            nativeHeights, entry, densityOffsets, detailOffsets, islandOffsets,
                            rng, densityCfg, uCfg, borderMarginPx, sharedGrid);
                    }
                }
            }
            finally
            {
                nativeHeights.Dispose();
            }

            return placements;
        }

        /// <summary>
        /// 岩クラスター周辺の樹木パッチを生成する。各エントリのrockProximityConfigが有効なもののみ処理。
        /// </summary>
        public static List<PlacementEntry> GenerateAroundObjects(
            bool[,] mask, float[] heights, TerrainDimensions dims,
            TreePlacementConfig treeConfig,
            List<Config.ObjectPlacementResult> objectPlacements,
            System.Random rng,
            SpatialGrid sharedGrid)
        {
            var placements = new List<PlacementEntry>();
            if (treeConfig?.prototypes == null || objectPlacements == null || objectPlacements.Count == 0)
                return placements;
            int res = dims.Resolution;

            // クラスターごとにグルーピング
            var clusterGroups = new Dictionary<int, List<Config.ObjectPlacementResult>>();
            foreach (var obj in objectPlacements)
            {
                int cid = obj.ClusterInfo.ClusterId;
                if (cid < 0) continue;
                if (!clusterGroups.ContainsKey(cid))
                    clusterGroups[cid] = new List<Config.ObjectPlacementResult>();
                clusterGroups[cid].Add(obj);
            }

            foreach (var entry in treeConfig.prototypes)
            {
                if (entry == null || entry.disabled || entry.prefabs == null) continue;
                var proxCfg = entry.rockProximityConfig;
                if (proxCfg == null || !proxCfg.enabled) continue;

                bool hasValid = false;
                foreach (var go in entry.prefabs) if (go != null) { hasValid = true; break; }
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
                            if (!CheckMask(mask, new Vector2(tx, tz), dims, res, borderMarginPx)) continue;

                            // 共有グリッドで既存木との最小距離チェック
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
                                Prefab = PickRandomPrefab(entry.prefabs, rng),
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

        // =================================================================
        // 内部: 単一エントリの配置ポイント評価
        // =================================================================

        static void TryPlaceEntry(
            TreePrototypeEntry entry, Vector2 point,
            TerrainDimensions dims, float[] heights, float[] curvatureMap,
            NativeArray<float> nativeHeights, Vector2[] noiseOffsets,
            TreeDensityConfig densityCfg, SpatialGrid sharedGrid,
            System.Random rng, List<PlacementEntry> placements)
        {
            int res = dims.Resolution;
            float normX = point.x / dims.TerrainWidth;
            float normZ = point.y / dims.TerrainLength;
            int hx = Mathf.Clamp(Mathf.RoundToInt(normX * (res - 1)), 0, res - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(normZ * (res - 1)), 0, res - 1);
            int idx = hz * res + hx;

            // 共有グリッドで他エントリ・他パスとの最小距離チェック
            if (sharedGrid.HasNeighborWithin(point.x, point.y, entry.sharedGridMinDistance)) return;

            // 局所密度上限: 半径内に既に上限数以上の木があれば配置しない
            if (densityCfg.localDensityCapCount > 0 &&
                sharedGrid.CountNeighborsWithin(point.x, point.y, densityCfg.localDensityCapRadius)
                >= densityCfg.localDensityCapCount) return;

            float height = heights[idx];
            float slope = BurstTerrainMath.ComputeSlope(nativeHeights, res, hx, hz,
                dims.TerrainWidth, dims.TerrainHeight, dims.TerrainLength);
            float curvature = curvatureMap[idx];

            // 傾斜フィルタ
            if (slope > densityCfg.slopeHardReject) return;
            if (slope > densityCfg.slopeSoftReject)
            {
                float slopeReject = (slope - densityCfg.slopeSoftReject)
                    / (densityCfg.slopeHardReject - densityCfg.slopeSoftReject);
                if ((float)rng.NextDouble() < slopeReject) return;
            }

            // per-protoフィルタ
            float weight = 1f;
            if (entry.slopeFilter.enabled)
            {
                float n = SampleFilterNoise(entry.slopeFilter.noise, point.x, point.y, noiseOffsets,
                    dims.TerrainWidth, dims.TerrainLength);
                weight *= entry.slopeFilter.Evaluate(slope, n);
            }
            if (entry.curvatureFilter.enabled)
            {
                float n = SampleFilterNoise(entry.curvatureFilter.noise, point.x, point.y, noiseOffsets,
                    dims.TerrainWidth, dims.TerrainLength);
                weight *= entry.curvatureFilter.Evaluate(curvature, n);
            }
            if (weight <= 0f) return;
            if (weight < 1f && (float)rng.NextDouble() > weight) return;

            // クラスタリングノイズ
            if (entry.clusterNoise.noiseType != MapNoiseType.None || entry.clusterNoise.texture != null)
            {
                float noise1 = ManagedNoise.SamplePlacementNoise(entry.clusterNoise,
                    point.x, point.y, noiseOffsets, dims.TerrainWidth, dims.TerrainLength);
                if (entry.clusterNoise2.noiseType != MapNoiseType.None || entry.clusterNoise2.texture != null)
                {
                    float noise2 = ManagedNoise.SamplePlacementNoise(entry.clusterNoise2,
                        point.x, point.y, noiseOffsets, dims.TerrainWidth, dims.TerrainLength);
                    noise1 = ManagedNoise.CombineNoise(noise1, noise2, entry.noise2Op);
                }
                // ソフト遷移
                float threshold = entry.clusterNoiseThreshold;
                float hardEdge = threshold * 0.6f;
                if (noise1 < hardEdge) return;
                if (noise1 < threshold)
                {
                    float ratio = (noise1 - hardEdge) / (threshold - hardEdge);
                    if ((float)rng.NextDouble() > ratio * ratio * ratio) return;
                }
            }

            // スケール計算
            float heightScale = Mathf.Lerp(entry.scaleHeightRange.x, entry.scaleHeightRange.y,
                (float)rng.NextDouble());
            float widthScale = entry.lockWidthHeight
                ? heightScale
                : Mathf.Lerp(entry.scaleWidthRange.x, entry.scaleWidthRange.y, (float)rng.NextDouble());

            // 巨木判定
            if (entry.oldGrowthRatio > 0f && (float)rng.NextDouble() < entry.oldGrowthRatio)
            {
                heightScale *= entry.oldGrowthScale;
                widthScale *= entry.oldGrowthScale;
            }

            float rotation = entry.randomRotation ? (float)rng.NextDouble() * 360f : 0f;
            float sinkNorm = dims.TerrainHeight > 0f ? entry.sink / dims.TerrainHeight : 0f;

            placements.Add(new PlacementEntry
            {
                Prefab = PickRandomPrefab(entry.prefabs, rng),
                WorldPosition = new Vector3(point.x, height * dims.TerrainHeight, point.y),
                Rotation = Quaternion.Euler(0, rotation, 0),
                Scale = new Vector3(widthScale, heightScale, widthScale),
                Sink = sinkNorm * dims.TerrainHeight
            });
            sharedGrid.Add(point.x, point.y);
        }

        // =================================================================
        // 自己クラスター下層木: 同じprefabsを小スケールでcanopy木の周辺に配置
        // =================================================================

        static void AddSelfUnderstory(
            List<PlacementEntry> placements, int entryStartIdx,
            bool[,] mask, TerrainDimensions dims,
            float[] heights, NativeArray<float> nativeHeights,
            TreePrototypeEntry entry,
            Vector2[] densityOffsets, Vector2[] detailOffsets, Vector2[] islandOffsets,
            System.Random rng, TreeDensityConfig dCfg, UnderstoryConfig uCfg,
            float borderMarginPx, SpatialGrid sharedGrid)
        {
            int res = dims.Resolution;
            int count = placements.Count;

            for (int i = entryStartIdx; i < count; i++)
            {
                var canopy = placements[i];
                // canopy判定: スケールがcanopyScaleThreshold以上
                if (canopy.Scale.y < dCfg.canopyScaleThreshold && canopy.Scale.y < 1.15f) continue;

                float parentX = canopy.WorldPosition.x;
                float parentZ = canopy.WorldPosition.z;
                float parentDensity = SampleDensityNoise(parentX, parentZ,
                    densityOffsets, detailOffsets, islandOffsets, dCfg);

                int desiredPatches = parentDensity >= dCfg.denseMinThreshold
                    ? uCfg.densePatches + rng.Next(uCfg.densePatchesRandom)
                    : parentDensity >= dCfg.transitionMinThreshold
                        ? uCfg.transitionPatches + rng.Next(uCfg.transitionPatchesRandom) : 0;
                if (desiredPatches <= 0) continue;

                int targetChildren = parentDensity >= dCfg.denseMinThreshold
                    ? uCfg.denseTreesPerCanopy + rng.Next(uCfg.denseTreesRandom)
                    : uCfg.transitionTreesPerCanopy + rng.Next(uCfg.transitionTreesRandom);
                int placedChildren = 0;

                for (int patchIdx = 0; patchIdx < desiredPatches && placedChildren < targetChildren; patchIdx++)
                {
                    float patchAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float patchDist = Mathf.Lerp(uCfg.patchDistanceMin, uCfg.patchDistanceMax,
                        Mathf.Sqrt((float)rng.NextDouble()));
                    float pcx = parentX + Mathf.Cos(patchAngle) * patchDist;
                    float pcz = parentZ + Mathf.Sin(patchAngle) * patchDist;
                    float prx = parentDensity >= dCfg.denseMinThreshold
                        ? Mathf.Lerp(uCfg.densePatchRadiusMin, uCfg.densePatchRadiusMax, (float)rng.NextDouble())
                        : Mathf.Lerp(uCfg.transitionPatchRadiusMin, uCfg.transitionPatchRadiusMax, (float)rng.NextDouble());
                    float prz = prx * Mathf.Lerp(uCfg.patchAspectMin, uCfg.patchAspectMax, (float)rng.NextDouble());
                    float maskOffX = (float)rng.NextDouble() * 200f;
                    float maskOffZ = (float)rng.NextDouble() * 200f;
                    float mThreshold = parentDensity >= dCfg.denseMinThreshold
                        ? uCfg.denseMaskThreshold : uCfg.transitionMaskThreshold;
                    int patchTarget = Mathf.Min(targetChildren - placedChildren,
                        parentDensity >= dCfg.denseMinThreshold
                            ? uCfg.patchTargetDense + rng.Next(uCfg.patchTargetDenseRandom)
                            : uCfg.patchTargetTransition + rng.Next(uCfg.patchTargetTransitionRandom));
                    int patchAttempts = patchTarget * 8;

                    for (int att = 0; att < patchAttempts && placedChildren < targetChildren; att++)
                    {
                        float lx = ((float)rng.NextDouble() - 0.5f) * prx * 2f;
                        float lz = ((float)rng.NextDouble() - 0.5f) * prz * 2f;
                        float ellipse = (lx * lx) / (prx * prx) + (lz * lz) / (prz * prz);
                        if (ellipse > 1f) continue;
                        float tx = pcx + lx, tz = pcz + lz;
                        if (tx < 0f || tx > dims.TerrainWidth || tz < 0f || tz > dims.TerrainLength) continue;
                        if (sharedGrid.HasNeighborWithin(tx, tz, uCfg.understoryNeighborRadius)) continue;
                        // 局所密度上限: understory木も含めて密集を防止
                        if (dCfg.localDensityCapCount > 0 &&
                            sharedGrid.CountNeighborsWithin(tx, tz, dCfg.localDensityCapRadius)
                            >= dCfg.localDensityCapCount) continue;
                        if (!CheckMask(mask, new Vector2(tx, tz), dims, res, borderMarginPx)) continue;

                        float localDensity = SampleDensityNoise(tx, tz,
                            densityOffsets, detailOffsets, islandOffsets, dCfg);
                        float mk = Mathf.PerlinNoise(tx * uCfg.patchMaskFrequency + maskOffX,
                            tz * uCfg.patchMaskFrequency + maskOffZ);
                        float combined = mk * uCfg.patchMaskWeight + localDensity * (1f - uCfg.patchMaskWeight);
                        if (combined < mThreshold - ellipse * uCfg.patchMaskEllipseOffset) continue;

                        int thx = Mathf.Clamp(Mathf.RoundToInt(tx / dims.TerrainWidth * (res - 1)), 0, res - 1);
                        int thz = Mathf.Clamp(Mathf.RoundToInt(tz / dims.TerrainLength * (res - 1)), 0, res - 1);
                        float th = heights[thz * res + thx];
                        float slope = BurstTerrainMath.ComputeSlope(nativeHeights, res, thx, thz,
                            dims.TerrainWidth, dims.TerrainHeight, dims.TerrainLength);
                        if (slope > uCfg.understorySlopeLimit) continue;

                        // 同prefabsを小スケールで配置
                        float childH = Mathf.Lerp(entry.scaleHeightRange.x, entry.scaleHeightRange.y,
                            (float)rng.NextDouble()) * uCfg.understoryScaleThreshold;
                        childH *= Mathf.Lerp(uCfg.edgeScaleMax, uCfg.edgeScaleMin,
                            Mathf.Clamp01(Mathf.Sqrt(ellipse)));
                        float childW = entry.lockWidthHeight ? childH
                            : Mathf.Lerp(entry.scaleWidthRange.x, entry.scaleWidthRange.y,
                                (float)rng.NextDouble()) * uCfg.understoryScaleThreshold;
                        float sinkNorm = dims.TerrainHeight > 0f ? entry.sink / dims.TerrainHeight : 0f;

                        placements.Add(new PlacementEntry
                        {
                            Prefab = PickRandomPrefab(entry.prefabs, rng),
                            WorldPosition = new Vector3(tx, th * dims.TerrainHeight, tz),
                            Rotation = Quaternion.Euler(0,
                                entry.randomRotation ? (float)rng.NextDouble() * 360f : 0f, 0),
                            Scale = new Vector3(childW, childH, childW),
                            Sink = sinkNorm * dims.TerrainHeight
                        });
                        sharedGrid.Add(tx, tz);
                        placedChildren++;
                    }
                }
            }
        }

        // maskチェック: ワールド座標のポイントがmask内かつborderMargin外か
        static bool CheckMask(bool[,] mask, Vector2 point, TerrainDimensions dims, int res, float borderMarginPx)
        {
            int hx = Mathf.Clamp(Mathf.RoundToInt(point.x / dims.TerrainWidth * (res - 1)), 0, res - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(point.y / dims.TerrainLength * (res - 1)), 0, res - 1);
            if (!mask[hz, hx]) return false;
            if (borderMarginPx > 0f && BiomeMaskBuilder.IsNearMaskEdge(mask, hx, hz, res, borderMarginPx))
                return false;
            return true;
        }

        /// <summary>
        /// 3スケール密度ノイズ＋島変調を合成。
        /// </summary>
        static float SampleDensityNoise(float worldX, float worldZ,
            Vector2[] densityOffsets, Vector2[] detailOffsets, Vector2[] islandOffsets,
            TreeDensityConfig cfg)
        {
            float largeN = ManagedNoise.SampleFBm(worldX, worldZ,
                cfg.densityLargeFrequency, densityOffsets, 0.5f, 2f, 4);
            float midN = ManagedNoise.SampleFBm(worldX, worldZ,
                cfg.densityMidFrequency, detailOffsets, 0.5f, 2f, 3);
            float smallN = ManagedNoise.SampleFBm(worldX, worldZ,
                cfg.densitySmallFrequency, densityOffsets, 2, 0.5f, 2f, 3);
            float baseDensity = largeN * cfg.densityLargeWeight + midN * cfg.densityMidWeight + smallN * cfg.densitySmallWeight;

            float islandN = ManagedNoise.SampleFBm(worldX, worldZ,
                cfg.islandModulationFrequency, islandOffsets, 0.5f, 2f, 3);
            float islandMod = Mathf.Lerp(cfg.islandModulationMin, cfg.islandModulationMax, islandN);
            return Mathf.Max(baseDensity * islandMod, cfg.densityFloor);
        }

        static float[] ComputeCurvatureMap(float[] heights, int res)
        {
            var curvature = new float[heights.Length];
            for (int z = 1; z < res - 1; z++)
            for (int x = 1; x < res - 1; x++)
            {
                int idx = z * res + x;
                float center = heights[idx];
                float laplacian = heights[idx - 1] + heights[idx + 1]
                                + heights[idx - res] + heights[idx + res]
                                - 4f * center;
                curvature[idx] = laplacian;
            }
            return curvature;
        }

        static float SampleFilterNoise(PlacementNoise noise, float worldX, float worldZ,
            Vector2[] offsets, float terrainWidth, float terrainLength)
        {
            if (noise.noiseType == MapNoiseType.None && noise.texture == null) return 0f;
            return ManagedNoise.SamplePlacementNoise(noise, worldX, worldZ, offsets,
                terrainWidth, terrainLength);
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

        // =====================================================
        // Post-placement: 地形変更・テクスチャ変更
        // =====================================================

        /// <summary>
        /// 配置済み樹木の周辺ハイトマップを変更する（ガウシアンフォールオフ）。
        /// </summary>
        public static void ApplyHeightModification(
            float[] heights, int res, float terrainWidth, float terrainHeight,
            TreeInstance[] trees, BiomePlacementHelper helper, BiomeType[] biomeTypes, int[] prototypeOffsets)
        {
            for (int b = 0; b < biomeTypes.Length; b++)
            {
                var activeEntries = helper.GetActivePrototypeEntries(biomeTypes[b]);
                if (activeEntries.Length == 0) continue;

                int protoStart = prototypeOffsets[b];
                int protoEnd = b + 1 < biomeTypes.Length ? prototypeOffsets[b + 1] : int.MaxValue;

                foreach (var tree in trees)
                {
                    if (tree.prototypeIndex < protoStart || tree.prototypeIndex >= protoEnd) continue;

                    int localIdx = tree.prototypeIndex - protoStart;
                    TreePrototypeEntry entry = localIdx >= 0 && localIdx < activeEntries.Length
                        ? activeEntries[localIdx] : null;
                    if (entry == null) continue;

                    float modAmount = entry.heightModAmount;
                    float modWidth = entry.heightModWidth;

                    if (Mathf.Approximately(modAmount, 0f)) continue;

                    float radiusPixels = modWidth / terrainWidth * (res - 1);
                    int radiusInt = Mathf.CeilToInt(radiusPixels);
                    float modNorm = modAmount / terrainHeight;

                    int cx = Mathf.RoundToInt(tree.position.x * (res - 1));
                    int cz = Mathf.RoundToInt(tree.position.z * (res - 1));

                    for (int dz = -radiusInt; dz <= radiusInt; dz++)
                    for (int dx = -radiusInt; dx <= radiusInt; dx++)
                    {
                        int px = cx + dx;
                        int pz = cz + dz;
                        if (px < 0 || px >= res || pz < 0 || pz >= res) continue;

                        float dist = Mathf.Sqrt(dx * dx + dz * dz);
                        if (dist > radiusPixels) continue;

                        float sigma = radiusPixels / 3f;
                        float falloff = Mathf.Exp(-(dist * dist) / (2f * sigma * sigma));
                        heights[pz * res + px] += modNorm * falloff;
                    }
                }
            }
        }

        /// <summary>
        /// 配置済み樹木の周辺スプラットマップにテクスチャを追加する。
        /// </summary>
        public static void ApplyTextureModification(
            float[,,] splatmap, int aRes, float terrainWidth,
            TerrainLayer[] terrainLayers,
            TreeInstance[] trees, BiomePlacementHelper helper, BiomeType[] biomeTypes, int[] prototypeOffsets)
        {
            if (splatmap == null || terrainLayers == null) return;
            int layerCount = splatmap.GetLength(2);

            for (int b = 0; b < biomeTypes.Length; b++)
            {
                var activeEntries = helper.GetActivePrototypeEntries(biomeTypes[b]);
                if (activeEntries.Length == 0) continue;

                int protoStart = prototypeOffsets[b];
                int protoEnd = b + 1 < biomeTypes.Length ? prototypeOffsets[b + 1] : int.MaxValue;

                foreach (var tree in trees)
                {
                    if (tree.prototypeIndex < protoStart || tree.prototypeIndex >= protoEnd) continue;

                    int localIdx = tree.prototypeIndex - protoStart;
                    TreePrototypeEntry entry = localIdx >= 0 && localIdx < activeEntries.Length
                        ? activeEntries[localIdx] : null;
                    if (entry == null) continue;

                    TerrainLayer layer = entry.surroundLayer;
                    float weight = entry.surroundLayerWeight;
                    float width = entry.surroundLayerWidth;

                    if (layer == null || weight <= 0f) continue;

                    int layerIndex = -1;
                    for (int l = 0; l < terrainLayers.Length; l++)
                    {
                        if (terrainLayers[l] == layer) { layerIndex = l; break; }
                    }
                    if (layerIndex < 0 || layerIndex >= layerCount) continue;

                    float radiusPixels = width / terrainWidth * (aRes - 1);
                    int radiusInt = Mathf.CeilToInt(radiusPixels);

                    int cx = Mathf.RoundToInt(tree.position.x * (aRes - 1));
                    int cz = Mathf.RoundToInt(tree.position.z * (aRes - 1));

                    for (int dz = -radiusInt; dz <= radiusInt; dz++)
                    for (int dx = -radiusInt; dx <= radiusInt; dx++)
                    {
                        int px = cx + dx;
                        int pz = cz + dz;
                        if (px < 0 || px >= aRes || pz < 0 || pz >= aRes) continue;

                        float dist = Mathf.Sqrt(dx * dx + dz * dz);
                        if (dist > radiusPixels) continue;

                        float sigma = radiusPixels / 3f;
                        float falloff = Mathf.Exp(-(dist * dist) / (2f * sigma * sigma));
                        float blendWeight = weight * falloff;

                        float remaining = 1f - blendWeight;
                        for (int l = 0; l < layerCount; l++)
                        {
                            if (l == layerIndex)
                                splatmap[pz, px, l] = splatmap[pz, px, l] * remaining + blendWeight;
                            else
                                splatmap[pz, px, l] *= remaining;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// prototypeOffsets配列を外部から取得するためのヘルパー。
        /// </summary>
        public static int[] ComputePrototypeOffsets(BiomePlacementHelper helper, BiomeType[] biomeTypes)
        {
            var offsets = new int[biomeTypes.Length];
            int total = 0;
            for (int b = 0; b < biomeTypes.Length; b++)
            {
                offsets[b] = total;
                total += helper.GetTreePrototypes(biomeTypes[b]).Length;
            }
            return offsets;
        }
    }
}
