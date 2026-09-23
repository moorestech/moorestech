using Game.BeltSegment;
using System;
using System.Collections.Generic;
using static Server.Util.MessagePack.BeltSegment.BeltWireValidation;
namespace Server.Util.MessagePack.BeltSegment
{
    internal static class BeltTickValidation
    {
        internal static string Validate(BeltReplayTickMessagePack tick)
        {
            if (!(tick != null && tick.Complete)) return "Missing tick.";
            if (Array(tick.SpeedChanges) is string error1) return error1; if (Array(tick.Insertions) is string error2) return error2; if (UniqueIds(tick.ReadyInputs) is string error3) return error3; if (UniqueIds(tick.SuccessfulOutputs) is string error4) return error4;
            var speeds = new HashSet<int>(); var insertions = new HashSet<int>(); var identities = new HashSet<Guid>();
            foreach (var speed in tick.SpeedChanges)
            {
                if (!(speed != null && speed.Complete && 0 <= speed.SegmentId && speed.SegmentId < MaximumCount && speeds.Add(speed.SegmentId))) return "Invalid or repeated speed ID.";
                if (Speed(speed.Speed) is string error5) return error5;
            }
            foreach (var insertion in tick.Insertions)
            {
                if (!(insertion != null && insertion.Complete && 0 <= insertion.InputId && insertion.InputId < MaximumCount && insertions.Add(insertion.InputId))) return "Invalid or repeated insertion ID.";
                if (!(0 < insertion.Length && insertion.Length <= BeltConstants.ItemWidth)) return "Invalid insertion length.";
                if (Item(insertion.Item, identities) is string error6) return error6;
            }
            return null;
        }
    }
}
