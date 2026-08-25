using UnityEngine;

namespace Game.Block.Interface
{
    /// <summary>
    ///     採掘機の見た目上のドリルが占めるワールドセルを求める
    ///     Resolves the world cell occupied by a miner's visual drill
    /// </summary>
    public static class MinerDrillPositionCalculator
    {
        /// <summary>
        ///     ドリル位置はブロックローカル座標なので、コネクターのoffsetと同じ規約でワールドへ変換する
        ///     The drill position is block-local, so convert it to world with the same convention as connector offsets
        /// </summary>
        public static Vector3Int Calculate(BlockPositionInfo blockPositionInfo, Vector3Int drillLocalPosition)
        {
            var blockDirection = blockPositionInfo.BlockDirection;
            var baseOriginPos = blockDirection.GetBlockBaseOriginPos(blockPositionInfo);
            
            return baseOriginPos + blockDirection.GetCoordinateConvertAction()(drillLocalPosition);
        }
    }
}
