using Game.MapGeneration.Pipeline.Surface.Grading;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    public static class SurfaceObjectReanchor
    {
        public static PlacementLedger Apply(MapGenerationOutput output, PlacementLedger ledger,
            SurfacePlacementBindings bindings, SurfaceTileGrid before, SurfaceTileGrid after)
        {
            // 既存sinkと姿勢を残し、mapObjectだけへ地表差分を加える
            // Add only the surface delta to map objects, preserving their sink and rotation
            foreach (var item in output.MapObjects)
            {
                var point = new Vector2(item.Position.x, item.Position.z);
                item.Position += Vector3.up * (after.SampleHeight(point) - before.SampleHeight(point));
            }
            return bindings.ApplyMapObjectPositions(output, ledger);
        }
    }
}
