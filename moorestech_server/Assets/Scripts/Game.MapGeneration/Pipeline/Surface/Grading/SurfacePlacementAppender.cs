using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Grading
{
    internal static class SurfacePlacementAppender
    {
        internal static void AppendVeins(VeinPlacementBatch batch, bool item, MapGenerationOutput output,
            PlacementLedger ledger, SurfacePlacementBindings bindings, Vector2 noiseToScene, WorldSurfaceRevision revision)
        {
            var target = item ? output.ItemVeins : output.FluidVeins;
            for (int i = 0; i < batch.Veins.Count; i++)
            {
                int outputIndex = target.Count;
                target.Add(batch.Veins[i]);
                if (revision == WorldSurfaceRevision.Legacy4) continue;

                // 確定した実配置のsurround/scaleを同じappend時点で渡す
                // Transfer the confirmed placement's surround and scale at the same append point
                var entry = batch.Entries[i].Shifted(new Vector3(-noiseToScene.x, 0f, -noiseToScene.y));
                int ledgerIndex = ledger.Placements.Count;
                ledger.Add(new LedgerPlacement(entry.MapObjectGuid, entry.WorldPosition, entry.Scale,
                    entry.SurroundEffect, null));
                if (item) bindings.AddItemVein(outputIndex, ledgerIndex);
                else bindings.AddFluidVein(outputIndex, ledgerIndex);
            }
        }
    }
}
