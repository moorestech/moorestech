using System;
using System.Collections.Generic;
using Game.MapGeneration.Facade.Surface;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    public static class VeinGroundingPlanner
    {
        public static GroundingPlan Build(SurfaceTileGrid grid, SurfaceEnvelope envelope)
        {
            var veins = new List<PlacedVein>(grid.Output.ItemVeins);
            veins.AddRange(grid.Output.FluidVeins);
            var cores = new List<Rect>();
            var supports = new List<RectInt>();
            foreach (var vein in veins)
            {
                // inclusive AABB中心でscene上の平坦部を作る
                // Construct the scene-space core around the inclusive AABB center
                var center = (Vector3)(vein.Min + vein.Max + Vector3Int.one) * 0.5f;
                var core = new Rect(new Vector2(center.x, center.z) - Vector2.one * envelope.CoreHalfSize,
                    Vector2.one * (2f * envelope.CoreHalfSize));
                var outer = Rect.MinMaxRect(core.xMin - envelope.BlendWidth, core.yMin - envelope.BlendWidth,
                    core.xMax + envelope.BlendWidth, core.yMax + envelope.BlendWidth);
                if (!grid.Land.ContainsSupport(outer))
                    throw SurfaceGenerationValidation.Failure(grid.Config, "grading", $"Non-land vein footprint at {center}.");
                cores.Add(core);
                supports.Add(grid.Geometry.SupportVertices(core));
            }

            var roots = GroundingComponents.Build(supports);
            var maximums = new float[veins.Count];
            for (int i = 0; i < veins.Count; i++)
            {
                var support = supports[i];
                for (int z = support.yMin; z < support.yMax; z++)
                for (int x = support.xMin; x < support.xMax; x++)
                    maximums[roots[i]] = Mathf.Max(maximums[roots[i]], grid.GetHeight(x, z));
            }

            // 全成分で同じUnity格納下限を使い、範囲の厚さは保つ
            // Use the same Unity storage lower bound for all components and retain range thickness
            float floor = SurfaceQuantization.LandFloor(grid.Config, envelope, "grading");
            double quantum = (double)grid.Config.terrainHeight / SurfaceQuantization.TerrainStorageSteps;
            int minimum = (int)Math.Ceiling(floor + quantum + 0.001d);
            int maximum = Mathf.FloorToInt(grid.Config.terrainHeight);
            if (minimum > maximum)
                throw SurfaceGenerationValidation.Failure(grid.Config, "grading", "No valid integer mining bottom interval.");
            var bottoms = new int[veins.Count];
            var pads = new List<VeinGroundingPad>();
            for (int i = 0; i < veins.Count; i++)
            {
                int bottom = Mathf.Clamp(Mathf.CeilToInt(maximums[roots[i]]), minimum, maximum);
                bottoms[i] = bottom;
                pads.Add(new VeinGroundingPad(cores[i], SurfaceQuantization.PadHeight(bottom, grid.Config, "grading"), envelope.BlendWidth));
            }
            return new GroundingPlan(grid, bottoms, pads);
        }
    }
}
