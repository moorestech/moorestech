using System;
using System.Collections.Generic;
using static Server.Util.MessagePack.BeltSegment.BeltWireValidation;
namespace Server.Util.MessagePack.BeltSegment
{
    internal static class BeltTickValidation
    {
        internal static void Validate(BeltReplayTickMessagePack tick)
        {
            Require(tick != null && tick.Complete, "Missing tick.");
            Array(tick.SpeedChanges); Array(tick.Insertions); UniqueIds(tick.ReadyInputs); UniqueIds(tick.SuccessfulOutputs);
            var speeds = new HashSet<int>(); var insertions = new HashSet<int>(); var identities = new HashSet<Guid>();
            foreach (var speed in tick.SpeedChanges)
            {
                Require(speed != null && speed.Complete && speed.SegmentId >= 0 && speed.SegmentId < MaximumCount && speeds.Add(speed.SegmentId),
                    "Invalid or repeated speed ID.");
                Speed(speed.Speed);
            }
            foreach (var insertion in tick.Insertions)
            {
                Require(insertion != null && insertion.Complete && insertion.InputId >= 0 && insertion.InputId < MaximumCount && insertions.Add(insertion.InputId),
                    "Invalid or repeated insertion ID.");
                Require(insertion.Length > 0 && insertion.Length <= 256, "Invalid insertion length.");
                Item(insertion.Item, identities);
            }
        }
    }
}
