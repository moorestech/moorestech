using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal sealed class GroundedSurfaceHeightPolicy : SurfaceHeightPolicy
    {
        private readonly SurfaceLandReconstructor _land;
        private readonly SurfaceEnvelope _envelope;
        public override TerrainSurfacePresentation Presentation { get; }

        internal GroundedSurfaceHeightPolicy(TerrainGenerationConfig config, SurfaceEnvelope envelope)
        {
            _land = new SurfaceLandReconstructor(config);
            _envelope = envelope;
            Presentation = new TerrainSurfacePresentation.Grounded(envelope);
        }

        public override float[,] Apply(float[,] heights, TerrainGenerationConfig config,
            Vector3 scene, PlacementLedger ledger)
        {
            return FinalSurfaceProjector.Apply(heights, config, scene, _land.Resolve(), ledger.GroundingPads, _envelope);
        }
        internal override void CopyBoundary(float[,] heights, Vector3 scene, PlacementLedger ledger,
            SurfaceDisplayBoundaryOwner owner)
        {
            owner.CopyTo(heights, scene, ledger, _land.Resolve(), _envelope, true);
        }
    }
}
