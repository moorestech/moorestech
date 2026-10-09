using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    // 整地前(before)の表示高さ。最終投影はせず、境界は所有タイルの値へ揃える
    // Pre-grading (before) display heights: no final projection, boundaries follow the owner tile
    internal sealed class PreGradingSurfaceHeightPolicy : SurfaceHeightPolicy
    {
        internal override float[,] Apply(float[,] heights, TerrainGenerationConfig config,
            Vector3 scene, PlacementLedger ledger)
        {
            return heights;
        }

        internal override void CopyBoundary(float[,] heights, Vector3 scene, PlacementLedger ledger,
            SurfaceDisplayBoundaryOwner owner)
        {
            owner.CopyTo(heights, scene, ledger, this);
        }
    }
}
