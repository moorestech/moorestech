using System.Collections.Generic;
using Core.Item.Interface;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 財布を通らない撤去。全額返却して財布には触れない
    /// A removal that bypasses the wallet; it refunds the full cost and never touches the wallet
    /// </summary>
    internal class DirectCostRemovalPlan : IConstructionRemovalPlan
    {
        public IReadOnlyList<IItemStack> ItemsToRefund { get; }

        internal DirectCostRemovalPlan(IReadOnlyList<IItemStack> itemsToRefund)
        {
            ItemsToRefund = itemsToRefund;
        }

        public void Commit()
        {
        }
    }
}
