using System.Collections.Generic;
using Core.Master;

namespace Game.Construction
{
    // 問い合わせ時点の設置判断。確定側はこの答えを実行する
    // Placement decisions at query time; the commit side executes this answer
    public readonly struct ConstructionWalletCellPlan
    {
        public readonly ConstructionWalletUsage Usage;
        public readonly BlockId WalletBlockId;
        public readonly int PlacementsPerCost;
        public readonly IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume;

        internal ConstructionWalletCellPlan(ConstructionWalletUsage usage, BlockId walletBlockId, int placementsPerCost,
            IReadOnlyList<(ItemId itemId, int count)> itemsToConsume)
        {
            Usage = usage;
            WalletBlockId = walletBlockId;
            PlacementsPerCost = placementsPerCost;
            ItemsToConsume = itemsToConsume;
        }
    }
}
