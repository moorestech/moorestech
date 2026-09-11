using System.Collections.Generic;
using Core.Item.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost.Plan
{
    /// <summary>
    /// 張替え1セルの撤去指示。サーバーのIConstructionRemovalPlanと同型で、見えるのは返却アイテム列と確定操作のみ
    /// One replace cell's removal instruction, shaped like the server's IConstructionRemovalPlan; only the items handed back and the commit are visible
    /// </summary>
    internal interface IBeltReplaceRemovalPlan
    {
        IReadOnlyList<IItemStack> RefundItems { get; }

        void Commit(BeltReplaceCostLedger ledger);
    }
}
