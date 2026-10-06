using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal sealed class LegacySurfaceHeightPolicy : SurfaceHeightPolicy
    {
        public override TerrainSurfacePresentation Presentation { get; } = new TerrainSurfacePresentation.Existing();

        public override float[,] Apply(float[,] heights, TerrainGenerationConfig config,
            Vector3 scene, PlacementLedger ledger)
        {
            return heights;
        }
    }
}
