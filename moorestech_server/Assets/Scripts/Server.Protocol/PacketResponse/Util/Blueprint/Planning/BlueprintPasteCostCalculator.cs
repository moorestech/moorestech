using System.Collections.Generic;
using Core.Master;
using Game.Construction;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    public static class BlueprintPasteCostCalculator
    {
        public static List<(ItemId itemId, int count)> CalcRequiredItems(IReadOnlyList<BlueprintPasteCopyDraft> drafts, ConstructionWalletQuery wallet, bool isPaymentWaived)
        {
            var accumulator = new BlueprintPasteMaterialAccumulator(wallet, isPaymentWaived);
            foreach (var draft in drafts)
            {
                accumulator.AddDraft(draft);
            }
            return accumulator.GetRequiredItems();
        }
    }
}
