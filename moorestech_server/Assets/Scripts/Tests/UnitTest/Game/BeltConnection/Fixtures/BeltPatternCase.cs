using System.Collections.Generic;

namespace Tests.UnitTest.Game.BeltConnection.Fixtures
{
    internal enum BeltTestSlot { UL, UR, LL, LR }

    internal readonly struct BeltExpectedConnection
    {
        internal readonly BeltTestSlot Source;
        internal readonly BeltTestSlot Target;

        internal BeltExpectedConnection(BeltTestSlot source, BeltTestSlot target)
        {
            Source = source;
            Target = target;
        }

        public override string ToString() => $"{Source}>{Target}";
    }

    internal sealed class BeltPatternCase
    {
        internal readonly string Id;
        internal readonly IReadOnlyList<BeltExpectedConnection> Connections;
        internal readonly IReadOnlyList<BeltTestSlot> TouchingSlots;

        internal BeltPatternCase(string id, IReadOnlyList<BeltExpectedConnection> connections, IReadOnlyList<BeltTestSlot> touchingSlots)
        {
            Id = id;
            Connections = connections;
            TouchingSlots = touchingSlots;
        }
    }
}
