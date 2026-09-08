using System;
using Core.Master;
using Game.PlacementTarget;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// 坂ベルトの解放元をファミリーの直線ブロックへ寄せる
    /// Normalizes a belt slope's unlock source to its family's straight block
    /// </summary>
    public class BeltConveyorPlacementUnlockSourceMap : IPlacementUnlockSourceMap
    {
        public Guid ResolveUnlockSourceId(Guid targetId)
        {
            // ベルトファミリー外はそのまま自分の解放状態に従う
            // Anything outside a belt family follows its own unlock state
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamilyByGuid(targetId, out var family)) return targetId;
            return MasterHolder.BlockMaster.GetBlockMaster(family.StraightBlockId).BlockGuid;
        }
    }
}
