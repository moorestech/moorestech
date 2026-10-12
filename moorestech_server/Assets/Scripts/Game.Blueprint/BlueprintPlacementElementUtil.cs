using Core.Master;
using Game.Block.Interface;

namespace Game.Blueprint
{
    /// <summary>
    ///     配置要素をマスタのサイズを含む占有情報へ変換する
    ///     Converts a placement element to footprint data using the master size
    /// </summary>
    public static class BlueprintPlacementElementUtil
    {
        public static BlockPositionInfo ToPositionInfo(BlueprintPlacementElement placement)
        {
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(placement.BlockId).BlockSize;
            return new BlockPositionInfo(placement.Position, placement.Direction, blockSize);
        }
    }
}
