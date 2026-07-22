using Core.Master;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// リプレース設置で相互置き換え可能なブロックファミリー判定
    /// Determines blocks mutually replaceable via replace-placement
    /// </summary>
    public static class BlockReplaceFamilyUtil
    {
        public static bool IsSameReplaceFamily(BlockId blockIdA, BlockId blockIdB)
        {
            // 両ブロックが同一のreplaceFamiliesエントリに属する場合のみ相互リプレース可
            // Mutually replaceable only when both blocks belong to the same replaceFamilies entry
            var blockGuidA = MasterHolder.BlockMaster.GetBlockMaster(blockIdA).BlockGuid;
            var blockGuidB = MasterHolder.BlockMaster.GetBlockMaster(blockIdB).BlockGuid;

            // 1ブロック1ファミリー所属はバリデーション済みのため、片方を含む最初のエントリで確定する
            // One family per block is validated, so the first entry containing either block decides
            foreach (var family in MasterHolder.BuildMenuCategoryMaster.ReplaceFamilies)
            {
                var containsA = false;
                var containsB = false;
                foreach (var target in family.TargetBlocks)
                {
                    if (target.BlockGuid == blockGuidA) containsA = true;
                    if (target.BlockGuid == blockGuidB) containsB = true;
                }

                if (containsA || containsB) return containsA && containsB;
            }

            return false;
        }
    }
}
