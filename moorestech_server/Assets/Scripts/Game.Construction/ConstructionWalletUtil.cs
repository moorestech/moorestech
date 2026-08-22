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

        // 撤去+1がNに達するか（達すれば凝縮し財布0へ）
        // Whether returning one reaches placementsPerCost (condenses and resets wallet to zero)
        public static bool WouldCondense(int remaining, int placementsPerCost)
        {
            return placementsPerCost <= remaining + 1;
        }
    }
}
