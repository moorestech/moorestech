using Client.Game.InGame.DistanceCulling;
using UnityEngine;

namespace Client.Game.InGame.Block.DistanceCulling
{
    /// <summary>
    /// 1ブロック分の距離カリング登録内容（判定範囲と対象）
    /// One block's distance-culling registration: its bounds and its target
    /// </summary>
    public readonly struct BlockDistanceCullingEntry
    {
        internal readonly Bounds WorldBounds;
        internal readonly IDistanceCullingTarget Target;

        internal BlockDistanceCullingEntry(Bounds worldBounds, IDistanceCullingTarget target)
        {
            WorldBounds = worldBounds;
            Target = target;
        }
    }
}
