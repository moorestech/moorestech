using System.Collections.Generic;

namespace Core.BeltTransport
{
    internal static class BeltNetworkEntry
    {
        internal static BeltDirection Direction(BeltTransportPath path, int index, BeltNetworkConnection[] connections,
            BeltDirection previous)
        {
            var cellId = path.Cells[index].Id;
            foreach (var edge in connections)
            {
                if (!edge.TargetIsBelt || edge.TargetId != cellId) continue;
                var entry = BeltDirections.Opposite(edge.Direction);
                if (path.Segment.Kind != BeltSegmentKind.Merge || entry == previous) return entry;
            }
            return BeltDirections.Opposite(path.Cells[index].Forward);
        }

        internal static int Height(int cellId, BeltDirection direction, BeltNetworkConnection[] connections,
            IReadOnlyDictionary<int, BeltNetworkCell> cells)
        {
            foreach (var edge in connections)
                if (edge.TargetIsBelt && edge.TargetId == cellId && BeltDirections.Opposite(edge.Direction) == direction)
                    return edge.SourceIsBelt ? cells[edge.SourceId].Y - cells[cellId].Y : edge.EntryHeight;
            return 0;
        }
    }
}
