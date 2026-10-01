using System.Collections.Generic;

namespace Core.BeltTransport
{
    internal static class BeltNetworkEntry
    {
        internal static BeltDirection Direction(BeltTransportPath path, int index, BeltNetworkConnection[] connections,
            BeltDirection previous)
        {
            var cellId = path.Cells[index].Id;
            var selected = BeltDirection.None;
            foreach (var edge in connections)
            {
                if (!edge.TargetIsBelt || edge.TargetId != cellId) continue;
                var entry = BeltDirections.Opposite(edge.Direction);
                if (path.Segment.Kind == BeltSegmentKind.Merge && entry == previous) return entry;
                if (selected == BeltDirection.None || entry < selected) selected = entry;
            }
            // 消えた入口は存続方向へ揃え、接続配列順へ依存しない。
            // Replace a removed entry with a surviving direction independently of edge ordering.
            return selected == BeltDirection.None ? BeltDirections.Opposite(path.Cells[index].Forward) : selected;
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
