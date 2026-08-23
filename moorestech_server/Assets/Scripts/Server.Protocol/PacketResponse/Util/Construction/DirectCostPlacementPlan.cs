using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 財布を通らない設置。素材を全額消費するだけ
    /// A placement that bypasses the wallet; it only consumes the full material cost
    /// </summary>
    internal class DirectCostPlacementPlan : IConstructionPlacementPlan
    {
        public IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume { get; }

        internal DirectCostPlacementPlan(IReadOnlyList<(ItemId itemId, int count)> itemsToConsume)
        {
            ItemsToConsume = itemsToConsume;
        }

        public void Commit(IOpenableInventory inventory, BlockInstanceId blockInstanceId)
        {
            ConstructionCostService.ConsumeRequiredItems(ItemsToConsume, inventory);
        }
    }
}
