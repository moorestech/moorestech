using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Jobs;
using Unity.Collections;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 単一 Poisson 候補点に対する樹木配置の評価（傾斜/曲率/クラスタノイズ/スケール/巨木判定）。
    // Evaluate tree placement at a single Poisson candidate (slope/curvature/cluster noise/scale/old-growth).
    internal static class TreePlacementEntry
    {
        public static void TryPlaceEntry(
            TreePrototypeEntry entry, Vector2 point,
            TerrainDimensions dims, float[] heights, float[] curvatureMap,
            NativeArray<float> nativeHeights, Vector2[] noiseOffsets,
            TreeDensityConfig densityCfg, SpatialGrid sharedGrid,
            System.Random rng, List<PlacementEntry> placements)
        {
            int res = dims.Resolution;

            // ノイズは全てワールド座標で引く。近傍グリッドと高さの添字だけがタイルローカルのまま。
            // Every noise sample is world-space; only the neighbour grid and the heightmap index stay tile-local.
            float worldX = point.x + dims.WorldOffsetX;
            float worldZ = point.y + dims.WorldOffsetZ;

            float normX = point.x / dims.TerrainWidth;
            float normZ = point.y / dims.TerrainLength;
            int hx = Mathf.Clamp(Mathf.RoundToInt(normX * (res - 1)), 0, res - 1);
            int hz = Mathf.Clamp(Mathf.RoundToInt(normZ * (res - 1)), 0, res - 1);
            int idx = hz * res + hx;

            if (sharedGrid.HasNeighborWithin(point.x, point.y, entry.sharedGridMinDistance)) return;

            if (densityCfg.localDensityCapCount > 0 &&
                sharedGrid.CountNeighborsWithin(point.x, point.y, densityCfg.localDensityCapRadius)
                >= densityCfg.localDensityCapCount) return;

            float height = heights[idx];
            float slope = BurstTerrainMath.ComputeSlope(nativeHeights, res, hx, hz,
                dims.TerrainWidth, dims.TerrainHeight, dims.TerrainLength);
            float curvature = curvatureMap[idx];

            // 傾斜フィルタ
            // Slope filter
            if (slope > densityCfg.slopeHardReject) return;
            if (slope > densityCfg.slopeSoftReject)
            {
                float slopeReject = (slope - densityCfg.slopeSoftReject)
                    / (densityCfg.slopeHardReject - densityCfg.slopeSoftReject);
                if ((float)rng.NextDouble() < slopeReject) return;
            }

            // per-proto フィルタ
            // Per-prototype filters
            float weight = 1f;
            if (entry.slopeFilter.enabled)
            {
                float n = TreePlacementCommon.SampleFilterNoise(entry.slopeFilter.noise, worldX, worldZ, noiseOffsets, dims);
                weight *= entry.slopeFilter.Evaluate(slope, n);
            }
            if (entry.curvatureFilter.enabled)
            {
                float n = TreePlacementCommon.SampleFilterNoise(entry.curvatureFilter.noise, worldX, worldZ, noiseOffsets, dims);
                weight *= entry.curvatureFilter.Evaluate(curvature, n);
            }
            if (weight <= 0f) return;
            if (weight < 1f && (float)rng.NextDouble() > weight) return;

            // クラスタリングノイズ。ノイズタイプ None でもテクスチャ源があれば有効。
            // Clustering noise; still active with noiseType None as long as a texture source is present.
            if (entry.clusterNoise.IsActive)
            {
                float noise1 = ManagedNoise.SamplePlacementNoise(entry.clusterNoise,
                    worldX, worldZ, noiseOffsets, dims.GridOriginX, dims.GridOriginZ, dims.GridWidth, dims.GridLength);
                if (entry.clusterNoise2.IsActive)
                {
                    float noise2 = ManagedNoise.SamplePlacementNoise(entry.clusterNoise2,
                        worldX, worldZ, noiseOffsets, dims.GridOriginX, dims.GridOriginZ, dims.GridWidth, dims.GridLength);
                    noise1 = ManagedNoise.CombineNoise(noise1, noise2, entry.noise2Op);
                }
                float threshold = entry.clusterNoiseThreshold;
                float hardEdge = threshold * 0.6f;
                if (noise1 < hardEdge) return;
                if (noise1 < threshold)
                {
                    float ratio = (noise1 - hardEdge) / (threshold - hardEdge);
                    if ((float)rng.NextDouble() > ratio * ratio * ratio) return;
                }
            }

            // スケール計算・巨木判定
            // Scale computation and old-growth roll
            float heightScale = Mathf.Lerp(entry.scaleHeightRange.x, entry.scaleHeightRange.y,
                (float)rng.NextDouble());
            float widthScale = entry.lockWidthHeight
                ? heightScale
                : Mathf.Lerp(entry.scaleWidthRange.x, entry.scaleWidthRange.y, (float)rng.NextDouble());

            if (entry.oldGrowthRatio > 0f && (float)rng.NextDouble() < entry.oldGrowthRatio)
            {
                heightScale *= entry.oldGrowthScale;
                widthScale *= entry.oldGrowthScale;
            }

            float rotation = entry.randomRotation ? (float)rng.NextDouble() * 360f : 0f;
            float sinkNorm = dims.TerrainHeight > 0f ? entry.sink / dims.TerrainHeight : 0f;

            placements.Add(PlacementEntry.CreateTree(
                TreePlacementCommon.PickRandomGuid(entry.mapObjectGuids, rng),
                new Vector3(point.x, height * dims.TerrainHeight, point.y),
                Quaternion.Euler(0, rotation, 0),
                new Vector3(widthScale, heightScale, widthScale),
                sinkNorm * dims.TerrainHeight,
                entry.terrainSurroundEffectType));
            sharedGrid.Add(point.x, point.y);
        }
    }
}
