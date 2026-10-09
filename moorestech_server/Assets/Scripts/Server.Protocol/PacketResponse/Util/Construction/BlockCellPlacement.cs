using System.Collections.Generic;
using Core.Master;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    public readonly struct BlockCellPlacement
    {
        public readonly IConstructionPlacementPlan WalletPlan;
        public readonly bool IsPaymentWaived;
        public readonly bool IsAffordable;
        public IReadOnlyList<(ItemId itemId, int count)> ItemsToConsume => WalletPlan.ItemsToConsume;

        public BlockCellPlacement(IConstructionPlacementPlan walletPlan, bool isPaymentWaived, bool isAffordable)
        {
            WalletPlan = walletPlan;
            IsPaymentWaived = isPaymentWaived;
            IsAffordable = isAffordable;
        }
    }
}
