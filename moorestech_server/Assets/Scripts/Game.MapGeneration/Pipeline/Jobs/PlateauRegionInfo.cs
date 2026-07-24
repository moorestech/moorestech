using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.MapGeneration.Pipeline.Jobs
{
    /// <summary>
    /// 台地化領域の共通メタデータ。PlateauRegionAnalysisJobが算出し、
    /// PlateauFlattenJobが参照する。1連結領域につき1インスタンス。
    /// </summary>
    public struct PlateauRegionInfo
    {
        public float targetHeight;
        public int pixelCount;
        public int boundaryCount;
    }
}
