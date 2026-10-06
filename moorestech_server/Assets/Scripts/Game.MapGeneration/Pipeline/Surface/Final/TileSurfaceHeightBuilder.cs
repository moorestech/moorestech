using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    public static class TileSurfaceHeightBuilder
    {
        public static (float[,] Pre, float[,] Post) Build(float[,] pre, TerrainGenerationConfig config,
            Vector3 tileScene, PlacementLedger ledger, LandCellField land)
        {
            // 密度とsplatは保存済みの木加工前を読む
            // Density and splat keep reading the saved pre-tree field
            var post = TreePerturbationApplier.Apply(pre, config, tileScene, ledger.Placements);
            if (config.SurfaceRevision == WorldSurfaceRevision.Grounded5)
                post = FinalSurfaceProjector.Apply(post, config, tileScene, land, ledger.GroundingPads);
            return (pre, post);
        }
    }
}
