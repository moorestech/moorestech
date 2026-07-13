using UnityEngine;

namespace MapGenerator.Pipeline.Biomes
{
    /// <summary>
    /// TryPlaceObjectの戻り値。TreeInstanceをラップすることで、
    /// 将来的に草・岩など他のオブジェクト種別にも拡張できる余地を残す。
    /// </summary>
    public struct BiomePlacementResult
    {
        public TreeInstance TreeInstance;
    }
}
