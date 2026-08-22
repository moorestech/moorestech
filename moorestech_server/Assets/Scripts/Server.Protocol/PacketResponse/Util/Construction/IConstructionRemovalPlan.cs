using System.Collections.Generic;
using Core.Item.Interface;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 財布が返す撤去指示。見えるのは返却アイテム列のみ
    /// The removal instruction from the wallet; only the items to hand back are visible
    /// </summary>
    public interface IConstructionRemovalPlan
    {
        // 返却すべきアイテム。空なら何も返さない
        // Items to refund; empty means refund nothing
        IReadOnlyList<IItemStack> ItemsToRefund { get; }
    }
}
