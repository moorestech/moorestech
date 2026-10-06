using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    // 木変位後の高さを表示高さへ仕上げる手順。生成時のbefore/afterと表示焼きが同じ実装を通る
    // Finishes post-tree heights into display heights; generation's before/after and the display bake share this implementation
    public abstract class SurfaceHeightPolicy
    {
        internal abstract float[,] Apply(float[,] heights, TerrainGenerationConfig config,
            Vector3 scene, PlacementLedger ledger);

        internal abstract void CopyBoundary(float[,] heights, Vector3 scene, PlacementLedger ledger,
            SurfaceDisplayBoundaryOwner owner);
    }
}
