using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal static class TileSurfaceHeightBuilder
    {
        internal static float[,] Build(float[,] pre, TerrainGenerationConfig config,
            Vector3 tileScene, PlacementLedger ledger, SurfaceHeightPolicy policy, SurfaceDisplayBoundaryOwner owner)
        {
            // 密度とsplatは木加工前を読む
            // Density and splat read the pre-tree field
            var post = TreePerturbationApplier.Apply(pre, config, tileScene, ledger.Placements);
            post = policy.Apply(post, config, tileScene, ledger);
            policy.CopyBoundary(post, tileScene, ledger, owner);
            return post;
        }
    }
}
