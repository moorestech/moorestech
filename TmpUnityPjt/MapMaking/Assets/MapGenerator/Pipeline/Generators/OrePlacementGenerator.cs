using System.Collections.Generic;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Generators.Util;
using UnityEngine;

namespace MapGenerator.Pipeline.Generators
{
    /// <summary>
    /// Stage 6: 鉱脈のクラスター配置ジェネレーター。
    /// 鉱脈はワールド全体で一元管理する。各エントリの出現バイオーム(biomes)から構築した
    /// 合成マスク内でPoissonDisk中心→極座標クラスター展開の順で処理する。
    /// Tree/ObjectのSpatialGridを参照し、既存配置物との距離制約を適用する。
    /// </summary>
    public static class OrePlacementGenerator
    {
        /// <summary>
        /// ワールド全体の鉱脈を配置する。entryMasks[i] は entries[i] の対象バイオーム群の
        /// 合成マスク（呼び出し側で構築）。null のエントリ（対象バイオーム未指定など）はスキップする。
        /// 共有グリッド・centerSpacing はワールド全体で1セットとし、全エントリで共有する。
        /// </summary>
        public static List<PlacementEntry> GenerateForWorld(
            OreEntry[] entries,
            bool[][,] entryMasks,
            float borderMargin,
            float[,] heights,
            TerrainDimensions dims,
            System.Random rng,
            SpatialGrid treeSpatialGrid,
            SpatialGrid objectSpatialGrid)
        {
            var result = new List<PlacementEntry>();
            if (entries == null || entries.Length == 0)
                return result;

            float w = dims.TerrainWidth;
            float l = dims.TerrainLength;
            int hRes = dims.Resolution;
            float borderPx = BiomeMaskBuilder.MetersToPixels(borderMargin, w, hRes);

            // 鉱石メンバー（既存物との距離・鉱石同士の距離チェック用）
            var oreGrid = new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f));
            // クラスター中心（中心同士の最小間隔チェック用。バンド跨ぎ・エントリ跨ぎのクランプ防止）
            var clusterCenterGrid = new SpatialGrid(w, l, Mathf.Max(w / 50f, 5f));

            // クラスター中心の共有間隔。clusterCenterGrid は全エントリ共有のため、
            // 間隔も全エントリ・全バンド横断の clusterRadius*2.5 の最大値（単一半径）にして
            // 大半径中心の近くに小半径中心が寄る非対称問題を避ける。
            float centerSpacing = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                // 生成対象（prefab/bands/マスクあり）と同じ集合で算出する。無効エントリの大半径が
                // 有効な鉱石を過剰に間引かないよう、スキップ条件を生成ループと揃える。
                if (entry == null || entry.prefab == null || entry.bands == null) continue;
                if (entryMasks == null || i >= entryMasks.Length || entryMasks[i] == null) continue;
                foreach (var b in entry.bands)
                    if (b != null) centerSpacing = Mathf.Max(centerSpacing, b.clusterRadius * 2.5f);
            }

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.prefab == null) continue;
                if (entryMasks == null || i >= entryMasks.Length || entryMasks[i] == null) continue;
                GenerateEntry(entry, entryMasks[i], heights, dims, rng,
                    borderPx, treeSpatialGrid, objectSpatialGrid,
                    oreGrid, clusterCenterGrid, centerSpacing, result);
            }

            return result;
        }

        static void GenerateEntry(
            OreEntry entry,
            bool[,] mask,
            float[,] heights,
            TerrainDimensions dims,
            System.Random rng,
            float borderPx,
            SpatialGrid treeSpatialGrid,
            SpatialGrid objectSpatialGrid,
            SpatialGrid oreGrid,
            SpatialGrid clusterCenterGrid,
            float centerSpacing,
            List<PlacementEntry> result)
        {
            // --- バンドの検証警告（生成器側で実施。OreBandPlanner は純粋関数のため） ---
            if (entry.bands == null || entry.bands.Length == 0)
            {
                Debug.LogWarning($"[OrePlacement] 鉱石エントリ '{entry.prefab.name}' に距離バンド(bands)が未設定です。スキップします。");
                return;
            }
            // -1 以外の負値（無限扱いになる）と、外周半径の重複（後続バンドが縮退で無効化される）を警告。
            // 重複検出は finite/無限(+∞) 双方を同一キーで扱うため、複数の -1 もここで検出される。
            var seenKeys = new HashSet<float>();
            foreach (var b in entry.bands)
            {
                if (b == null) continue;
                if (b.outerRadiusMeters < 0f && b.outerRadiusMeters != -1f)
                    Debug.LogWarning($"[OrePlacement] '{entry.prefab.name}' に -1 以外の負の外周半径 ({b.outerRadiusMeters}) があります。無限として扱われます。");
                float key = b.outerRadiusMeters < 0f ? float.PositiveInfinity : b.outerRadiusMeters;
                if (!seenKeys.Add(key))
                    Debug.LogWarning($"[OrePlacement] '{entry.prefab.name}' に外周半径が重複するバンド ({b.outerRadiusMeters}) があります。後続は縮退で無効になります。");
            }

            float w = dims.TerrainWidth;
            float l = dims.TerrainLength;
            int hRes = dims.Resolution;
            float minDist = entry.minDistanceFromOthers;
            float sx = dims.SpawnWorldX;
            float sz = dims.SpawnWorldZ;

            var ranges = OreBandPlanner.BuildRanges(entry.bands);

            foreach (var range in ranges)
            {
                var band = range.Band;

                // Poisson Disk でクラスター中心候補を散布（バンドの密度・半径で）
                float poissonArea = w * l;
                float adjustedMinDist = Mathf.Sqrt(poissonArea / Mathf.Max(band.density * 100f, 1f));
                adjustedMinDist = Mathf.Max(adjustedMinDist, band.clusterRadius * 2.5f);

                var candidates = PoissonDiskSampler.Generate(w, l, adjustedMinDist, rng.Next());

                foreach (var candidate in candidates)
                {
                    float localX = candidate.x;
                    float localZ = candidate.y;

                    // リング判定（ワールド座標距離・クラスター中心のみ）
                    float dx = (localX + dims.WorldOffsetX) - sx;
                    float dz = (localZ + dims.WorldOffsetZ) - sz;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (!range.Contains(dist)) continue;

                    // バイオームマスク判定
                    int px = Mathf.Clamp(Mathf.RoundToInt(localX / w * (hRes - 1)), 0, hRes - 1);
                    int pz = Mathf.Clamp(Mathf.RoundToInt(localZ / l * (hRes - 1)), 0, hRes - 1);
                    if (!mask[pz, px]) continue;
                    if (BiomeMaskBuilder.IsNearMaskEdge(mask, px, pz, hRes, borderPx)) continue;

                    // 傾斜フィルタ（entry単位）
                    if (entry.useSlopeFilter)
                    {
                        float slope = ComputeSlopeAngle(heights, px, pz, hRes, w, dims.TerrainHeight, l);
                        float swt = EvaluateSlopeFilter(slope, entry.slopeMax, entry.slopeSmoothness);
                        if (swt <= 0f) continue;
                        if (swt < 1f && swt < (float)rng.NextDouble()) continue;
                    }

                    // クラスター中心同士の最小間隔（バンド跨ぎ・エントリ跨ぎ含む。単一半径 centerSpacing）
                    if (clusterCenterGrid.HasNeighborWithin(localX, localZ, centerSpacing))
                        continue;

                    // 既存配置物・既存鉱石メンバーとの距離（entry単位）
                    if (0f < minDist)
                    {
                        if (treeSpatialGrid != null && treeSpatialGrid.HasNeighborWithin(localX, localZ, minDist))
                            continue;
                        if (objectSpatialGrid != null && objectSpatialGrid.HasNeighborWithin(localX, localZ, minDist))
                            continue;
                        if (oreGrid.HasNeighborWithin(localX, localZ, minDist))
                            continue;
                    }

                    // クラスター中心を登録
                    clusterCenterGrid.Add(localX, localZ);

                    // クラスターメンバーを極座標で配置（バンドのパラメータ）
                    int clusterCount = rng.Next(1, band.maxObjectsPerCluster + 1);
                    float oreMinDist = band.minDistanceBetweenOres;
                    int retries = Mathf.Max(1, band.placementRetries);
                    for (int i = 0; i < clusterCount; i++)
                    {
                        float mx = 0f, mz = 0f;
                        bool placed = false;
                        for (int attempt = 0; attempt < retries; attempt++)
                        {
                            float angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                            float radius = (float)rng.NextDouble() * band.clusterRadius;
                            // 鉱脈はグリッド（整数ワールド座標）に整合させる。ワールドX/Zを整数へ
                            // スナップし、以降の距離・グリッド判定もスナップ後座標で行う。
                            // localへ戻すのは oreGrid 等がローカル空間のため（WorldOffsetはfloat）。
                            mx = Mathf.Round(localX + Mathf.Cos(angle) * radius + dims.WorldOffsetX) - dims.WorldOffsetX;
                            mz = Mathf.Round(localZ + Mathf.Sin(angle) * radius + dims.WorldOffsetZ) - dims.WorldOffsetZ;

                            if (mx < 0 || w <= mx || mz < 0 || l <= mz) continue;
                            if (0f < oreMinDist && oreGrid.HasNeighborWithin(mx, mz, oreMinDist))
                                continue;

                            placed = true;
                            break;
                        }
                        if (!placed) continue;

                        float my = SampleHeight(heights, mx, mz, w, l, hRes) * dims.TerrainHeight;

                        result.Add(new PlacementEntry
                        {
                            Prefab = entry.prefab,
                            WorldPosition = new Vector3(
                                mx + dims.WorldOffsetX,  // = Mathf.Round(...) なので整数
                                my,                      // YはTerrainApplier側でSampleHeight後に整数化
                                mz + dims.WorldOffsetZ), // = Mathf.Round(...) なので整数
                            Rotation = Quaternion.identity,
                            Scale = Vector3.one,
                            Sink = 0f,
                            Cluster = null
                        });

                        // メンバー位置を oreGrid に登録（後続鉱石との距離保証用）
                        oreGrid.Add(mx, mz);
                    }
                }
            }
        }

        static float SampleHeight(float[,] heights, float localX, float localZ,
            float w, float l, int hRes)
        {
            int hx = Mathf.Clamp(Mathf.RoundToInt(localX / w * (hRes - 1)), 0, hRes - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(localZ / l * (hRes - 1)), 0, hRes - 1);
            return heights[hz, hx];
        }

        static float ComputeSlopeAngle(float[,] heights, int x, int z, int res,
            float terrainWidth, float terrainHeight, float terrainLength)
        {
            float h = heights[z, x];
            float hR = (x < res - 1) ? heights[z, x + 1] : h;
            float hU = (z < res - 1) ? heights[z + 1, x] : h;
            float cellX = terrainWidth / (res - 1);
            float cellZ = terrainLength / (res - 1);
            float dhdx = (hR - h) * terrainHeight / cellX;
            float dhdz = (hU - h) * terrainHeight / cellZ;
            var normal = new Vector3(-dhdx, 1f, -dhdz).normalized;
            return Mathf.Acos(Mathf.Clamp01(normal.y)) * Mathf.Rad2Deg;
        }

        // slopeMax以下を通過、smoothness幅で遷移するフィルタ
        static float EvaluateSlopeFilter(float slope, float max, float smoothness)
        {
            if (smoothness <= 0.001f)
                return slope <= max ? 1f : 0f;
            return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((slope - max) / smoothness));
        }
    }
}
