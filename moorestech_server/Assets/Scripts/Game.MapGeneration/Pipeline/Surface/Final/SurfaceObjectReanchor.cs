using Game.MapGeneration.Pipeline.Surface.Grading;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal static class SurfaceObjectReanchor
    {
        public static PlacementLedger Apply(MapGenerationOutput output, PlacementLedger ledger,
            SurfacePlacementBindings bindings, SurfaceTileGrid before, SurfaceTileGrid after)
        {
            // sinkと姿勢を残し地表差分だけ加算
            // Add only the surface delta, keeping sink and rotation
            foreach (var item in output.MapObjects)
            {
                var point = new Vector2(item.Position.x, item.Position.z);
                item.Position += Vector3.up * (after.SampleHeight(point) - before.SampleHeight(point));
            }
            return bindings.ApplyMapObjectPositions(output, ledger);
        }
    }
}
