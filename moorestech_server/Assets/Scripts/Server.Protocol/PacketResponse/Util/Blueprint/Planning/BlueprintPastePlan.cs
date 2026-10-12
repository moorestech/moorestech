using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    public class BlueprintPastePlan
    {
        public IReadOnlyList<BlueprintPasteCopyPlan> Copies { get; }
        public bool IsPaymentWaived { get; }
        public IReadOnlyList<(ItemId itemId, int held, int required)> ShortageRequirements { get; }

        public BlueprintPastePlan(IReadOnlyList<BlueprintPasteCopyPlan> copies, bool isPaymentWaived,
            IReadOnlyList<(ItemId itemId, int held, int required)> shortageRequirements)
        {
            Copies = Array.AsReadOnly(copies.ToArray());
            IsPaymentWaived = isPaymentWaived;
            ShortageRequirements = Array.AsReadOnly(shortageRequirements.ToArray());
        }

        public IEnumerable<BlueprintPasteCopyPlan> EnumerateCopiesToPlace()
        {
            return Copies.Where(copy => copy.IsPlaced);
        }

        public int CountCopies(BlueprintPasteCopyState state)
        {
            return Copies.Count(copy => copy.State == state);
        }
    }
}
