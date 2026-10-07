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
            var post = BuildInterior(pre, config, tileScene, ledger, policy);
            policy.CopyBoundary(post, tileScene, ledger, owner);
            return post;
        }

        // 所有境界コピー前の表示高さを組み立てる唯一の入口
        // The single assembly of display heights before the owned-boundary copy
        internal static float[,] BuildInterior(float[,] pre, TerrainGenerationConfig config,
            Vector3 tileScene, PlacementLedger ledger, SurfaceHeightPolicy policy)
        {
            // 密度とsplatは木加工前を読む
            // Density and splat read the pre-tree field
            var post = TreePerturbationApplier.Apply(pre, config, tileScene, ledger.Placements);
            return policy.Apply(post, config, tileScene, ledger);
        }
    }
}
