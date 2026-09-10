using System.Collections.Generic;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace
{
    /// <summary>
    /// 張替え1セルの設置計画。財布を通らないブロックはWalletBlockIdがnull
    /// One replace cell's placement plan; WalletBlockId is null for a block that bypasses the wallet
    /// </summary>
    public readonly struct BeltReplacePlacementPlan
    {
        public readonly IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume;
        public readonly BlockId? WalletBlockId;
        public readonly int PlacementsPerCost;
        public readonly bool CoveredByWallet;

        public BeltReplacePlacementPlan(IReadOnlyList<(ItemId itemId, int count)> itemsToConsume, BlockId? walletBlockId, int placementsPerCost, bool coveredByWallet)
        {
            ItemsToConsume = itemsToConsume;
            WalletBlockId = walletBlockId;
            PlacementsPerCost = placementsPerCost;
            CoveredByWallet = coveredByWallet;
        }
    }
}
