using System;
using Core.BeltTransport;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.BeltTransport
{
    internal static class BeltTransportTestFactory
    {
        internal static BeltConveyorSegment Create(int capacity, int speed, BeltSegmentKind kind)
            => new BeltConveyorSegment(capacity, speed, kind, -1, BeltDirection.Front);

        internal static BeltItem Item(int instance)
            => new BeltItem(new Guid(instance, 0, 0, new byte[8]), ForUnitTestItemId.ItemId1.AsPrimitive());
    }
}
