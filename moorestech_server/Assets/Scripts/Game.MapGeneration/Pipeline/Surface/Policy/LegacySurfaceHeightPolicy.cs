using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal sealed class LegacySurfaceHeightPolicy : SurfaceHeightPolicy
    {
        internal override float[,] Apply(float[,] heights, TerrainGenerationConfig config,
            Vector3 scene, PlacementLedger ledger)
        {
            return heights;
        }

        internal override void CopyBoundary(float[,] heights, Vector3 scene, PlacementLedger ledger,
            SurfaceDisplayBoundaryOwner owner)
        {
            // 旧表示経路では所有境界を評価しない
            // Do not evaluate owned boundaries on the legacy presentation path
        }
    }
}
