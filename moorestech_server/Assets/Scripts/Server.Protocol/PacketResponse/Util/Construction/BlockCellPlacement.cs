using System.Collections.Generic;
using Core.Master;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    internal readonly struct BlockCellPlacement
    {
        public readonly IConstructionPlacementPlan WalletPlan;
        public readonly BlockId BlockId;
        public readonly bool IsPaymentWaived;
        public readonly bool IsAffordable;
        internal IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume => WalletPlan.ItemsToConsume;

        internal BlockCellPlacement(BlockId blockId, IConstructionPlacementPlan walletPlan, bool isPaymentWaived, bool isAffordable)
        {
            WalletPlan = walletPlan;
            BlockId = blockId;
            IsPaymentWaived = isPaymentWaived;
            IsAffordable = isAffordable;
        }
    }
}
