using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// リプレース設置で相互置き換え可能なブロックファミリー判定
    /// Determines blocks mutually replaceable via replace-placement
    /// </summary>
    public static class BlockReplaceFamilyUtil
    {
        public static bool IsReplaceFamily(BlockId blockId)
        {
            // ベルト系3タイプのみ相互リプレース可能とする
            // Only the three belt-type blocks are mutually replaceable
            var blockType = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockType;
            return blockType == BlockMasterElement.BlockTypeConst.BeltConveyor ||
                   blockType == BlockMasterElement.BlockTypeConst.GearBeltConveyor ||
                   blockType == BlockMasterElement.BlockTypeConst.FilterSplitter;
        }
    }
}
