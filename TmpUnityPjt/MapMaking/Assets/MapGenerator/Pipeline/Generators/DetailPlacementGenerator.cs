using System.Collections.Generic;
using System.Threading.Tasks;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Generators.Util;
using MapGenerator.Pipeline.Jobs;
using UnityEngine;

namespace MapGenerator.Pipeline.Generators
{
    /// <summary>
    /// バイオームごとの BiomeDetailConfig から Detail 密度マップを生成する。
    /// TreePlacementGenerator と同列のパイプラインステージで、草花・低木を担当。
    /// MicroVerse 相当のフィルタ（曲率・角度・テクスチャ・多段ノイズ）をサポート。
    /// </summary>
    public static class DetailPlacementGenerator
    {

        /// <summary>
        /// 1バイオーム分のDetail密度マップを生成する。バイオームの概念を持たない純粋関数。
        /// </summary>
        public static (List<DetailPrototype> prototypes, List<int[,]> maps) GenerateForBiome(
            bool[,] mask,
            float[,] heights,
            float[,] slopes,
            TerrainDimensions dims,
            BiomeDetailConfig detailConfig,
            System.Random rng,
            float[,,] splatmap = null,
            TerrainLayer[] terrainLayers = null,
            float[,] treeDistanceMap = null,
            float[,] objectDistanceMap = null)
        {
            int detailRes = dims.Resolution - 1; // alphamapResolution
            int hRes = dims.Resolution;
            var prototypes = new List<DetailPrototype>();
            var maps = new List<int[,]>();

            // 曲率・方位角の遅延計算
            float[,] curvature = null;
            float[,] azimuth = null;
            bool needCurvature = false, needAzimuth = false;
            PrecomputeRequirementsForConfig(detailConfig, ref needCurvature, ref needAzimuth);
            if (needCurvature) curvature = CurvatureComputer.ComputeCurvature(heights, hRes);
            if (needAzimuth) azimuth = CurvatureComputer.ComputeAzimuth(heights, hRes);

            int splatRes = splatmap != null ? splatmap.GetLength(0) : 0;
            var offsets = ManagedNoise.GenerateOffsets(rng, 8);
            float filterReject = detailConfig.filterRejectThreshold;
            float borderMarginPx = BiomeMaskBuilder.MetersToPixels(detailConfig.borderMargin, dims.TerrainWidth, hRes);

            foreach (var entry in detailConfig.entries)
            {
                if (!entry.prototypeConfig.IsValid) continue;

                prototypes.Add(entry.prototypeConfig.ToDetailPrototype());
                int protoIdx = maps.Count;
                var map = new int[detailRes, detailRes];

                // ラムダキャプチャ用ローカルコピー（エントリ単位で固定の値）
                var e = entry;
                int dRes = detailRes;
                int hR = hRes;
                int sRes = splatRes;
                float fReject = filterReject;
                float bMarginPx = borderMarginPx;
                float tWidth = dims.TerrainWidth;
                float tLength = dims.TerrainLength;
                var msk = mask;
                var hts = heights;
                var slps = slopes;
                var curv = curvature;
                var azm = azimuth;
                var treeMap = treeDistanceMap;
                var objMap = objectDistanceMap;
                var splat = splatmap;
                var tLayers = terrainLayers;
                var ofs = offsets;
                var prevMaps = maps;
                int pIdx = protoIdx;

                // 行単位で並列化。各ピクセルは完全に独立（隣接参照なし）
                // occludedByOthers は先行エントリの map を読むのみ（完成済み）
                Parallel.For(0, dRes, z =>
                {
                    for (int x = 0; x < dRes; x++)
                    {
                        // detail 座標を heightmap 座標に変換
                        int hx = Mathf.Clamp(
                            Mathf.RoundToInt((float)x / (dRes - 1) * (hR - 1)), 0, hR - 1);
                        int hz = Mathf.Clamp(
                            Mathf.RoundToInt((float)z / (dRes - 1) * (hR - 1)), 0, hR - 1);

                        // maskチェック（このバイオームの領域内か）
                        if (!msk[hz, hx]) continue;
                        if (bMarginPx > 0f && BiomeMaskBuilder.IsNearMaskEdge(msk, hx, hz, hR, bMarginPx)) continue;

                        float worldX = (float)x / dRes * tWidth;
                        float worldZ = (float)z / dRes * tLength;

                        // ベース密度 = エントリ重み
                        float density = e.weight;

                        // 傾斜フィルタ
                        float sf = e.slopeFilter.Evaluate(
                            slps[hz, hx], worldX, worldZ, ofs);
                        if (sf < fReject) continue;
                        density *= sf;

                        // 曲率フィルタ
                        if (e.curvatureFilter.enabled && curv != null)
                        {
                            float cf = e.curvatureFilter.Evaluate(
                                curv[hz, hx], worldX, worldZ, ofs);
                            if (cf < fReject) continue;
                            density *= cf;
                        }

                        // 角度（方位）フィルタ
                        if (e.angleFilter.enabled && azm != null)
                        {
                            float af = e.angleFilter.Evaluate(
                                azm[hz, hx], worldX, worldZ, ofs);
                            if (af < fReject) continue;
                            density *= af;
                        }

                        // Tree距離フィルタ
                        if (e.treeDistanceFilter.enabled && treeMap != null)
                        {
                            float treeDist = treeMap[z, x];
                            float tdf = e.treeDistanceFilter.Evaluate(
                                treeDist, worldX, worldZ, ofs);
                            if (tdf < fReject) continue;
                            density *= tdf;
                        }

                        // Object距離フィルタ
                        if (e.objectDistanceFilter.enabled && objMap != null)
                        {
                            float objDist = objMap[z, x];
                            float odf = e.objectDistanceFilter.Evaluate(
                                objDist, worldX, worldZ, ofs);
                            if (odf < fReject) continue;
                            density *= odf;
                        }

                        // テクスチャフィルタ
                        if (e.textureFilter.enabled && splat != null && tLayers != null)
                        {
                            int sx = Mathf.Clamp(
                                Mathf.RoundToInt((float)x / (dRes - 1) * (sRes - 1)),
                                0, sRes - 1);
                            int sz = Mathf.Clamp(
                                Mathf.RoundToInt((float)z / (dRes - 1) * (sRes - 1)),
                                0, sRes - 1);
                            float tf = e.textureFilter.Evaluate(
                                splat, sz, sx, tLayers);
                            if (tf < fReject) continue;
                            density *= tf;
                        }

                        // ノイズスタック
                        if (e.noiseStack.IsActive)
                        {
                            float noiseMod = e.noiseStack.Sample(worldX, worldZ, ofs);
                            density *= noiseMod;
                        }

                        // weightRange
                        if (density < e.weightRange.x || density > e.weightRange.y)
                            continue;

                        // occludedByOthers: 先行エントリの map は完成済みなので読み取り安全
                        if (e.occludedByOthers && pIdx > 0)
                        {
                            bool occluded = false;
                            for (int p = 0; p < pIdx; p++)
                            {
                                if (prevMaps[p][z, x] > 0)
                                {
                                    occluded = true;
                                    break;
                                }
                            }
                            if (occluded) continue;
                        }

                        map[z, x] = Mathf.Clamp(
                            Mathf.RoundToInt(density * e.maxDensity), 0, e.maxDensity);
                    }
                });

                maps.Add(map);
            }

            return (prototypes, maps);
        }

        /// <summary>
        /// 全バイオームの DetailEntry を走査し、曲率・方位角の事前計算が必要かを判定する。
        /// </summary>
        static void PrecomputeRequirements(
            BiomePlacementHelper helper, BiomeType[] biomeTypes,
            ref bool needCurvature, ref bool needAzimuth)
        {
            foreach (var biome in biomeTypes)
            {
                var dc = helper.GetDetailConfig(biome);
                PrecomputeRequirementsForConfig(dc, ref needCurvature, ref needAzimuth);
                if (needCurvature && needAzimuth) return;
            }
        }

        // 単一BiomeDetailConfigから曲率・方位角の必要性を判定
        static void PrecomputeRequirementsForConfig(
            BiomeDetailConfig dc, ref bool needCurvature, ref bool needAzimuth)
        {
            if (dc?.entries == null) return;
            foreach (var entry in dc.entries)
            {
                if (entry.curvatureFilter.enabled) needCurvature = true;
                if (entry.angleFilter.enabled) needAzimuth = true;
                if (needCurvature && needAzimuth) return;
            }
        }
    }
}
