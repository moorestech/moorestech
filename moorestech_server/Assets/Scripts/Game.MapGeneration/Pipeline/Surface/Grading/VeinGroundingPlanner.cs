using System;
using System.Collections.Generic;
using Game.MapGeneration.Surface;
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
                var core = CoreFootprint(vein, envelope);
                if (!grid.Land.ContainsSupport(OuterFootprint(vein, envelope)))
                    throw SurfaceGenerationValidation.Failure(grid.Config, "grading", $"Non-land vein footprint at {core.center}.");
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

            // 全成分で同じ格納下限、範囲厚は保つ
            // Use one storage lower bound for all components, keep range thickness
            int minimum = (int)SurfaceQuantization.MinimumMiningBottom(grid.Config, envelope, "grading");
            int maximum = Mathf.FloorToInt(grid.Config.terrainHeight);
            if (maximum < minimum)
                throw SurfaceGenerationValidation.Failure(grid.Config, "grading", "No valid integer mining bottom interval.");
            var groundings = new List<VeinGrounding>(veins.Count);
            for (int i = 0; i < veins.Count; i++)
            {
                int bottom = Mathf.Clamp(Mathf.CeilToInt(maximums[roots[i]]), minimum, maximum);
                var pad = new VeinGroundingPad(cores[i], SurfaceQuantization.PadHeight(bottom, grid.Config, "grading"), envelope.BlendWidth);
                groundings.Add(new VeinGrounding(veins[i], bottom, pad));
            }
            return new GroundingPlan(grid, groundings);
        }

        // inclusive AABB中心でscene上の平坦部を作る
        // Construct the scene-space core around the inclusive AABB center
        internal static Rect CoreFootprint(PlacedVein sceneVein, SurfaceEnvelope envelope)
        {
            var center = (Vector3)(sceneVein.Min + sceneVein.Max + Vector3Int.one) * 0.5f;
            return new Rect(new Vector2(center.x, center.z) - Vector2.one * envelope.CoreHalfSize,
                Vector2.one * (2f * envelope.CoreHalfSize));
        }

        internal static Rect OuterFootprint(PlacedVein sceneVein, SurfaceEnvelope envelope)
        {
            return OuterOf(CoreFootprint(sceneVein, envelope), envelope.BlendWidth);
        }

        internal static Rect OuterOf(Rect core, float blendWidth)
        {
            return Rect.MinMaxRect(core.xMin - blendWidth, core.yMin - blendWidth,
                core.xMax + blendWidth, core.yMax + blendWidth);
        }
    }
}
