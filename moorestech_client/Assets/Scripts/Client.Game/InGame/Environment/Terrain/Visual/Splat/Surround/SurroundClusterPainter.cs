using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround
{
    /// <summary>
    ///     1クラスタぶんの岩を1つのフットプリント集合として扱い、コア帯と遷移帯の2層マスクで裸地を描く。
    ///     移植元 TerrainGenerator.cs:1573-1663 の逐語移植。座標はノイズのサンプル位置を除きタイルローカル
    ///     Treats one cluster's rocks as a single footprint set and paints bare ground with the two-band core and
    ///     transition mask; a verbatim port of the source's TerrainGenerator.cs:1573-1663, tile-local except where the noise is sampled
    /// </summary>
    public static class SurroundClusterPainter
    {
        // 2層Perlinをずらす定数。同じ座標で2枚が相関しないように移植元が置いた値
        // The offsets decorrelating the two Perlin layers at the same coordinate, as the source placed them
        private const float NoiseLowOffsetX = 42.7f;
        private const float NoiseLowOffsetZ = 18.3f;
        private const float NoiseHighOffsetX = 97.1f;
        private const float NoiseHighOffsetZ = 63.5f;

        public static void Paint(
            float[,,] alphamap, TerrainGenerationConfig config, SurroundTextureConfig surroundConfig,
            int layerIndex, IReadOnlyList<TileLocalMapObject> members, float[,] heights,
            Vector3 tileWorldPosition)
        {
            var alphaResolution = alphamap.GetLength(0);
            var heightResolution = config.Resolution;

            // 各岩のフットプリントを集め、遷移帯ぶん広げた外接矩形でピクセル走査範囲を決める
            // Collect each rock's footprint and bound the pixel scan by the box expanded by the transition band
            var footprints = new List<(float x, float z, float radius)>();
            var minLocalX = float.MaxValue;
            var maxLocalX = float.MinValue;
            var minLocalZ = float.MaxValue;
            var maxLocalZ = float.MinValue;

            foreach (var member in members)
            {
                var memberScale = member.Scale;
                var memberPosition = member.LocalPosition;
                var footprintRadius = (memberScale.x + memberScale.z) * 0.5f * surroundConfig.rockMeshBaseSize;
                footprints.Add((memberPosition.x, memberPosition.z, footprintRadius));

                var expand = footprintRadius + surroundConfig.transitionRadius;
                minLocalX = Mathf.Min(minLocalX, memberPosition.x - expand);
                maxLocalX = Mathf.Max(maxLocalX, memberPosition.x + expand);
                minLocalZ = Mathf.Min(minLocalZ, memberPosition.z - expand);
                maxLocalZ = Mathf.Max(maxLocalZ, memberPosition.z + expand);
            }

            var pixelMinX = Mathf.Clamp(
                Mathf.FloorToInt(minLocalX / config.terrainWidth * (alphaResolution - 1)), 0, alphaResolution - 1);
            var pixelMaxX = Mathf.Clamp(
                Mathf.CeilToInt(maxLocalX / config.terrainWidth * (alphaResolution - 1)), 0, alphaResolution - 1);
            var pixelMinZ = Mathf.Clamp(
                Mathf.FloorToInt(minLocalZ / config.terrainLength * (alphaResolution - 1)), 0, alphaResolution - 1);
            var pixelMaxZ = Mathf.Clamp(
                Mathf.CeilToInt(maxLocalZ / config.terrainLength * (alphaResolution - 1)), 0, alphaResolution - 1);

            for (var pixelZ = pixelMinZ; pixelZ <= pixelMaxZ; pixelZ++)
            for (var pixelX = pixelMinX; pixelX <= pixelMaxX; pixelX++)
            {
                var localX = (float)pixelX / (alphaResolution - 1) * config.terrainWidth;
                var localZ = (float)pixelZ / (alphaResolution - 1) * config.terrainLength;

                // 距離はフットプリント円の縁から測る。クラスタ内で最も近い1つが効く
                // Distance is measured from the footprint circle's edge, and the nearest one in the cluster wins
                var minDistance = float.MaxValue;
                foreach (var (footprintX, footprintZ, footprintRadius) in footprints)
                {
                    var deltaX = localX - footprintX;
                    var deltaZ = localZ - footprintZ;
                    var edgeDistance = Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ) - footprintRadius;
                    if (edgeDistance < minDistance) minDistance = edgeDistance;
                }

                if (surroundConfig.transitionRadius < minDistance) continue;

                var heightX = Mathf.Clamp(
                    Mathf.RoundToInt(localX / config.terrainWidth * (heightResolution - 1)), 0, heightResolution - 1);
                var heightZ = Mathf.Clamp(
                    Mathf.RoundToInt(localZ / config.terrainLength * (heightResolution - 1)), 0, heightResolution - 1);
                var slopeBias = SurroundDownhillBias.Compute(
                    config, heights, heightResolution, heightX, heightZ, localX, localZ, footprints);

                // ノイズだけはシーン絶対座標で引く。タイルローカルのままだと隣タイルで同じ模様が再開し境界に直線が出る
                // The noise alone samples scene-absolute coordinates: tile-local ones restart the pattern next door and draw a line along the seam
                var noiseModulation = ComputeNoiseModulation(
                    localX + tileWorldPosition.x, localZ + tileWorldPosition.z);
                var blend = ComputeBlend(minDistance / slopeBias, noiseModulation);

                // 移植元と同じ足切り。ここを外すと縁の全画素に無限小の泥が乗って輪郭が濁る
                // The source's cutoff; dropping it would smear an infinitesimal mud over every edge pixel
                if (blend < 0.01f) continue;
                SurroundBlendWriter.Blend(alphamap, pixelZ, pixelX, layerIndex, blend);
            }

            #region Internal

            float ComputeNoiseModulation(float sampleX, float sampleZ)
            {
                var noiseLow = Mathf.PerlinNoise(
                    sampleX * surroundConfig.noiseLowFrequency + NoiseLowOffsetX,
                    sampleZ * surroundConfig.noiseLowFrequency + NoiseLowOffsetZ);
                var noiseHigh = Mathf.PerlinNoise(
                    sampleX * surroundConfig.noiseHighFrequency + NoiseHighOffsetX,
                    sampleZ * surroundConfig.noiseHighFrequency + NoiseHighOffsetZ);
                return noiseLow * surroundConfig.noiseLowWeight + noiseHigh * (1f - surroundConfig.noiseLowWeight);
            }

            // コア帯はノイズを0.7-1.0の変調に留め、遷移帯はノイズをそのまま掛けて縁を割る
            // The core keeps noise as a 0.7-1.0 modulation while the transition band multiplies by it outright to break the edge
            float ComputeBlend(float biasedDistance, float noiseModulation)
            {
                if (biasedDistance < surroundConfig.coreRadius)
                {
                    var coreFactor = 1f - Mathf.Clamp01(biasedDistance / surroundConfig.coreRadius);
                    return Mathf.Lerp(surroundConfig.coreBlendMin, surroundConfig.coreBlendMax, coreFactor)
                           * (0.7f + noiseModulation * 0.3f);
                }

                if (biasedDistance < surroundConfig.transitionRadius)
                {
                    var outerFactor = 1f - Mathf.Clamp01(
                        (biasedDistance - surroundConfig.coreRadius)
                        / (surroundConfig.transitionRadius - surroundConfig.coreRadius));
                    return Mathf.Lerp(surroundConfig.transitionBlendMin, surroundConfig.transitionBlendMax, outerFactor)
                           * noiseModulation;
                }

                return 0f;
            }

            #endregion
        }
    }
}
