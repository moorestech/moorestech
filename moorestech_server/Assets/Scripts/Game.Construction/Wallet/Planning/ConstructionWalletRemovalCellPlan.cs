using System.Collections.Generic;
using Core.Master;

namespace Game.Construction
{
    // 問い合わせ時点の撤去判断。確定側はこの答えを実行する
    // Removal decisions at query time; the commit side executes this answer
    public readonly struct ConstructionWalletRemovalCellPlan
    {
        public readonly BlockId WalletBlockId;
        public readonly bool WouldCondense;
        public readonly IReadOnlyList<(ItemId itemId, int count)> ItemsToRefund;

        internal ConstructionWalletRemovalCellPlan(BlockId walletBlockId, bool wouldCondense, IReadOnlyList<(ItemId itemId, int count)> itemsToRefund)
        {
            WalletBlockId = walletBlockId;
            WouldCondense = wouldCondense;
            ItemsToRefund = itemsToRefund;
        }
    }
}
