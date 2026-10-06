using System.Collections.Generic;
using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface.Grading;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    public static class FinalSurfaceProjector
    {
        public static float[,] Apply(float[,] postTreeHeights, TerrainGenerationConfig tileConfig,
            Vector3 tileScene, LandCellField land, IReadOnlyList<VeinGroundingPad> pads)
        {
            var heights = (float[,])postTreeHeights.Clone();
            var origin = new Vector2(tileScene.x, tileScene.z);
            int stride = tileConfig.Resolution - 1;
            var spacing = new Vector2(tileConfig.terrainWidth / stride, tileConfig.terrainLength / stride);
            float floor = SurfaceQuantization.LandFloor(tileConfig.terrainHeight, SurfaceEnvelope.GeneratedV5);

            // 木の変位後に元分類の支持頂点を保護する
            // Protect the original land support vertices after tree displacement
            for (int z = 0; z < heights.GetLength(0); z++)
            for (int x = 0; x < heights.GetLength(1); x++)
            {
                var global = land.Geometry.GridPosition(origin + new Vector2(x * spacing.x, z * spacing.y));
                if (land.IsProtectedVertex(Mathf.RoundToInt(global.x), Mathf.RoundToInt(global.y)))
                    heights[z, x] = Mathf.Max(heights[z, x], floor / tileConfig.terrainHeight);
            }

            // 整地面を再投影して表示キャッシュと同じr16へ落とす
            // Reproject pads and land on the same r16 steps as the visual cache
            heights = GroundingHeightProjector.Apply(heights, origin, spacing, tileConfig.terrainHeight, pads);
            for (int z = 0; z < heights.GetLength(0); z++)
            for (int x = 0; x < heights.GetLength(1); x++)
                heights[z, x] = Mathf.Clamp(Mathf.RoundToInt(heights[z, x] * ushort.MaxValue), 0, ushort.MaxValue) / (float)ushort.MaxValue;
            return heights;
        }
    }
}
