using System.Collections.Generic;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost.Plan
{
    /// <summary>
    /// 財布を通らない設置。素材を全額消費するだけ
    /// A placement that bypasses the wallet; it only consumes the full material cost
    /// </summary>
    internal class DirectCostBeltReplacePlacementPlan : IBeltReplacePlacementPlan
    {
        public IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume { get; }

        internal DirectCostBeltReplacePlacementPlan(IReadOnlyList<(ItemId itemId, int count)> itemsToConsume)
        {
            ItemsToConsume = itemsToConsume;
        }

        public void Commit(BeltReplaceCostLedger ledger)
        {
            ledger.ConsumeItems(ItemsToConsume);
        }
    }
}
