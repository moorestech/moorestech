using System.Collections.Generic;
using Core.Master;
using Game.Construction;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost.Plan
{
    /// <summary>
    /// 財布を通る設置。素材を払うか残りで賄うかは計画時に確定済み
    /// A placement that goes through the wallet; whether it pays materials or draws from the remainder was settled when planning
    /// </summary>
    internal class WalletBeltReplacePlacementPlan : IBeltReplacePlacementPlan
    {
        public IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume { get; }

        private readonly BlockId _walletBlockId;
        private readonly int _placementsPerCost;
        private readonly bool _coveredByWallet;

        internal WalletBeltReplacePlacementPlan(IReadOnlyList<(ItemId itemId, int count)> itemsToConsume, BlockId walletBlockId, int placementsPerCost, bool coveredByWallet)
        {
            ItemsToConsume = itemsToConsume;
            _walletBlockId = walletBlockId;
            _placementsPerCost = placementsPerCost;
            _coveredByWallet = coveredByWallet;
        }

        public void Commit(BeltReplaceCostLedger ledger)
        {
            ledger.ConsumeItems(ItemsToConsume);

            // 残高の進め方はサーバーと同じConstructionWalletUtilが所有する
            // ConstructionWalletUtil, the very one the server uses, owns how the remainder advances
            var remaining = ledger.GetWalletRemainder(_walletBlockId);
            ledger.SetWalletRemainder(_walletBlockId, ConstructionWalletUtil.AdvanceOnPlacement(remaining, _placementsPerCost, _coveredByWallet));
        }
    }
}
