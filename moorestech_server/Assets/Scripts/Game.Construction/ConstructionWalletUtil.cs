using Core.Master;
using Game.Block.Interface.Extension;

namespace Game.Construction
{
    /// <summary>
    /// 残り設置数の財布キーを解決する。ベルトファミリーは直線代表へ正規化し、それ以外は自分自身（ADR 0026）
    /// Resolves the wallet key for remaining placements: belt families normalize to the straight block, others are themselves (ADR 0026)
    /// </summary>
    public static class ConstructionWalletUtil
    {
        public static BlockId ResolveWalletBlockId(BlockId blockId)
        {
            return BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var family) ? family.StraightBlockId : blockId;
        }
    }
}
