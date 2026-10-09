using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal sealed class GroundedSurfaceHeightPolicy : SurfaceHeightPolicy
    {
        private readonly LandCellField _land;
        private readonly SurfaceEnvelope _envelope;

        // 陸分類は生成時に確定したものを受け取り、表示側で再抽出しない
        // Takes the land classification settled at generation and never re-extracts it on the display side
        internal GroundedSurfaceHeightPolicy(LandCellField land, SurfaceEnvelope envelope)
        {
            _land = land;
            _envelope = envelope;
        }

        internal override float[,] Apply(float[,] heights, TerrainGenerationConfig config,
            Vector3 scene, PlacementLedger ledger)
        {
            return FinalSurfaceProjector.Apply(heights, config, scene, _land, ledger.GroundingPads, _envelope);
        }

        internal override void CopyBoundary(float[,] heights, Vector3 scene, PlacementLedger ledger,
            SurfaceDisplayBoundaryOwner owner)
        {
            owner.CopyTo(heights, scene, ledger, this);
        }
    }
}
