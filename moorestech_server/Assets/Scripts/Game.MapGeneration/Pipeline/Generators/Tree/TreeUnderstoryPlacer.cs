using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Jobs;
using Unity.Collections;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 自己クラスター下層木: canopy 木の周辺に同 mapObjectGuid を小スケールで配置する。
    // Self-cluster understory: place the same mapObjectGuids at small scale around canopy trees.
    internal static class TreeUnderstoryPlacer
    {
        public static void AddSelfUnderstory(
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
                if (canopy.Scale.y < dCfg.canopyScaleThreshold && canopy.Scale.y < 1.15f) continue;

                float parentX = canopy.WorldPosition.x;
                float parentZ = canopy.WorldPosition.z;
                float parentDensity = TreePlacementCommon.SampleDensityNoise(parentX, parentZ,
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
                        if (dCfg.localDensityCapCount > 0 &&
                            sharedGrid.CountNeighborsWithin(tx, tz, dCfg.localDensityCapRadius)
                            >= dCfg.localDensityCapCount) continue;
                        if (!TreePlacementCommon.CheckMask(mask, new Vector2(tx, tz), dims, res, borderMarginPx)) continue;

                        float localDensity = TreePlacementCommon.SampleDensityNoise(tx, tz,
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
                            MapObjectGuid = TreePlacementCommon.PickRandomGuid(entry.mapObjectGuids, rng),
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
    }
}
