using System;
using Game.PlacementTarget;

namespace Game.Block.Interface.Extension
{
    /// <summary>
    /// 坂ベルトの解放元をファミリーの直線ブロックへ寄せる（分岐器は自身）
    /// Normalizes a belt slope's unlock source to its family's straight block (splitters keep their own)
    /// </summary>
    public class BeltConveyorPlacementUnlockSourceMap : IPlacementUnlockSourceMap
    {
        public Guid ResolveUnlockSourceId(Guid targetId)
        {
            // 坂は直線の解放状態に従い、直線・分岐器・ファミリー外は自身に従う
            // Slopes follow the straight block's unlock state; straight, splitter and non-members follow their own
            return BeltConveyorPlaceFamilyUtil.ResolveSlopeRepresentativeGuid(targetId);
        }
    }
}
