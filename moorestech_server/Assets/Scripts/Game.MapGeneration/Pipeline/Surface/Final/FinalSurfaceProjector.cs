using System.Collections.Generic;
using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface.Grading;
using UnityEngine;
using Game.MapGeneration.Pipeline.Visual.Placement;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal static class FinalSurfaceProjector
    {
        public static float[,] Apply(float[,] postTreeHeights, TerrainGenerationConfig tileConfig,
            Vector3 tileScene, LandCellField land, IReadOnlyList<VeinGroundingPad> pads, SurfaceEnvelope envelope)
        {
            var heights = (float[,])postTreeHeights.Clone();
            var origin = new Vector2(tileScene.x, tileScene.z);
            var spacing = SurfaceLattice.SpacingFor(tileConfig);
            float floor = SurfaceQuantization.LandFloor(tileConfig, envelope, $"scene={tileScene.x},{tileScene.z}");

            // 木の変位後に元分類の支持頂点を保護する
            // Protect the original land support vertices after tree displacement
            for (int z = 0; z < heights.GetLength(0); z++)
            for (int x = 0; x < heights.GetLength(1); x++)
            {
                var global = land.Geometry.GridPosition(origin + new Vector2(x * spacing.x, z * spacing.y));
                if (land.IsProtectedVertex(Mathf.RoundToInt(global.x), Mathf.RoundToInt(global.y)))
                    heights[z, x] = Mathf.Max(heights[z, x], floor / tileConfig.terrainHeight);
            }

            // coreだけ再代入しr16へ符号化。skirtは生成時に保存高さへ焼き済みで二重補間しない
            // Reassign cores only and encode into r16; skirts are already baked into the stored heights and never blended twice
            heights = GroundingHeightProjector.ApplyCores(heights, origin, spacing, tileConfig.terrainHeight, pads);
            for (int z = 0; z < heights.GetLength(0); z++)
            for (int x = 0; x < heights.GetLength(1); x++)
                heights[z, x] = SurfaceQuantization.EncodeNormalized(heights[z, x]);
            return heights;
        }
    }
}
