using Core.Master;
using Game.Block.Interface.Extension;

namespace Game.Construction
{
    /// <summary>
    /// 財布キー解決と凝縮判定。ベルトは直線代表、他は自身
    /// Resolves the wallet key for remaining placements (belt families normalize to the straight block, others are themselves) and owns the condensation predicate
    /// </summary>
    public static class ConstructionWalletUtil
    {
        public static BlockId ResolveWalletBlockId(BlockId blockId)
        {
            return BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var family) ? family.StraightBlockId : blockId;
        }

        // 撤去で+1した結果がNに達するか（達すれば素材1セットへ凝縮し財布は0へ戻る）
        // Whether returning one reaches placementsPerCost, condensing into one material set and resetting the wallet to zero
        public static bool WouldCondense(int remaining, int placementsPerCost)
        {
            return placementsPerCost <= remaining + 1;
        }
    }
}
