using Core.Master;
using Game.Block.Interface.Extension;

namespace Game.Construction
{
    /// <summary>
    /// 財布キー解決。ベルトは直線代表、他は自身
    /// Resolves the wallet key for remaining placements: belt families normalize to the straight block, others are themselves
    /// </summary>
    public static class ConstructionWalletUtil
    {
        public static BlockId ResolveWalletBlockId(BlockId blockId)
        {
            return BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var family) ? family.StraightBlockId : blockId;
        }
    }
}
