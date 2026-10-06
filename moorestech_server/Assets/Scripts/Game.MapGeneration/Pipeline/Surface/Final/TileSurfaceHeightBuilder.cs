using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    public static class TileSurfaceHeightBuilder
    {
        public static (float[,] Pre, float[,] Post) Build(float[,] pre, TerrainGenerationConfig config,
            Vector3 tileScene, PlacementLedger ledger, SurfaceHeightPolicy policy)
        {
            // 密度とsplatは保存済みの木加工前を読む
            // Density and splat keep reading the saved pre-tree field
            var post = TreePerturbationApplier.Apply(pre, config, tileScene, ledger.Placements);
            post = policy.Apply(post, config, tileScene, ledger);
            return (pre, post);
        }
        internal static (float[,] Pre, float[,] Post) Build(float[,] pre, TerrainGenerationConfig config,
            Vector3 tileScene, PlacementLedger ledger, SurfaceHeightPolicy policy, SurfaceDisplayBoundaryOwner owner)
        {
            var pair = Build(pre, config, tileScene, ledger, policy);
            policy.CopyBoundary(pair.Post, tileScene, ledger, owner);
            return pair;
        }
    }
}
