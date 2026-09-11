using System.Collections.Generic;
using Core.Item.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost.Plan
{
    /// <summary>
    /// 財布を通らない撤去。課金元に関わらず全額戻るので見積りは確実
    /// A removal that bypasses the wallet; the full cost comes back whoever paid, so the estimate is certain
    /// </summary>
    internal class DirectCostBeltReplaceRemovalPlan : IBeltReplaceRemovalPlan
    {
        public IReadOnlyList<IItemStack> RefundItems { get; }

        internal DirectCostBeltReplaceRemovalPlan(IReadOnlyList<IItemStack> refundItems)
        {
            RefundItems = refundItems;
        }

        public void Commit(BeltReplaceCostLedger ledger)
        {
            ledger.AddArrivedRefund(RefundItems);
        }
    }
}
