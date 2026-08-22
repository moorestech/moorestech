using System.Collections.Generic;
using Core.Item.Interface;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 撤去時に財布が返す指示。呼び出し側に見えるのは返却すべきアイテム列だけ
    /// The removal instruction from the wallet; all a caller can see is which items to hand back
    /// </summary>
    public interface IConstructionRemovalPlan
    {
        // 返却すべきアイテム。空なら何も返さない
        // Items to refund; empty means refund nothing
        IReadOnlyList<IItemStack> ItemsToRefund { get; }
    }
}
