using System.Collections.Generic;
using Core.Item.Interface;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 財布が返す撤去指示。見えるのは返却アイテム列と確定操作のみ
    /// The removal instruction from the wallet; only the items to hand back and the commit are visible
    /// </summary>
    public interface IConstructionRemovalPlan
    {
        // 返却すべきアイテム。空なら何も返さない
        // Items to refund; empty means refund nothing
        IReadOnlyList<IItemStack> ItemsToRefund { get; }

        // 撤去確定後にのみ呼ぶ。返却物は Plan の時点で確保済み
        // Call only after removal is final; the refund was already reserved when the plan was made
        void Commit();
    }
}
