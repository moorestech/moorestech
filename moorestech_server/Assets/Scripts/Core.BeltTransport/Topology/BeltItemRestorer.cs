using System;
using System.Collections.Generic;

namespace Core.BeltTransport
{
    internal static class BeltItemRestorer
    {
        internal static void Restore(BeltTransportPath[] paths, BeltNetworkConnection[] connections,
            BeltCellItemState[] states, IBeltItemDropObserver observer)
        {
            var candidates = new List<BeltCellItemState>(states);
            var locations = new Dictionary<int, BeltTransportPath>();
            var cells = new Dictionary<int, BeltNetworkCell>();
            var accepted = new Dictionary<BeltTransportPath, List<BeltItemState>>();
            foreach (var path in paths)
            {
                accepted.Add(path, new List<BeltItemState>());
                foreach (var cell in path.Cells) { locations.Add(cell.Id, path); cells.Add(cell.Id, cell); }
            }

            // 経路が一致する走行中アイテムを先に配置する。
            // Place running items whose entry path still matches first.
            candidates.Sort(Compare);
            foreach (var item in candidates)
            {
                if (!locations.TryGetValue(item.CellId, out var path))
                {
                    observer.OnDropped(item, "The owning belt cell was removed.");
                    continue;
                }
                if (item.IsBuffer && path.Cells[path.Cells.Length - 1].Id == item.CellId && path.Segment.Buffer != null)
                {
                    path.Segment.Buffer.RestoreItem(item.Item);
                    continue;
                }

                // 消えたbufferは元セルの全幅が空く場合だけ出口へ戻す。
                // A disappearing buffer requires its original cell's full width.
                int distance = path.Distance(item.CellId, item.IsBuffer ? BeltConstants.ItemWidth : item.Progress);
                bool overlaps = false;
                foreach (var placed in accepted[path])
                    if (Math.Abs((long)placed.DistanceToExit - distance) < BeltConstants.ItemWidth) overlaps = true;
                if (overlaps)
                {
                    observer.OnDropped(item, "The item overlaps a higher-priority restored item.");
                    continue;
                }
                accepted[path].Add(new BeltItemState(item.Item, distance));
                if (path.Segment.Kind == BeltSegmentKind.Merge)
                    path.Segment.RestoreInputDirection(BeltNetworkEntry.Direction(path, 0, connections, item.EntryDirection));
            }
            foreach (var pair in accepted)
            {
                pair.Value.Sort((first, second) => first.DistanceToExit.CompareTo(second.DistanceToExit));
                pair.Key.Segment.RestoreItems(pair.Value.ToArray());
            }

            #region Internal
            int Rank(BeltCellItemState item)
            {
                if (item.IsBuffer) return 2;
                if (!locations.TryGetValue(item.CellId, out var path)) return 1;
                int index = path.IndexOf(item.CellId);
                var entry = BeltNetworkEntry.Direction(path, index, connections, item.EntryDirection);
                int height = BeltNetworkEntry.Height(item.CellId, entry, connections, cells);
                return entry == item.EntryDirection && height == item.EntryHeight ? 0 : 1;
            }

            int Compare(BeltCellItemState first, BeltCellItemState second)
            {
                int rank = Rank(first).CompareTo(Rank(second));
                if (rank != 0) return rank;
                if (locations.TryGetValue(first.CellId, out var left) && locations.TryGetValue(second.CellId, out var right) )
                {
                    int pathOrder = left.Cells[0].Id.CompareTo(right.Cells[0].Id);
                    if (pathOrder != 0) return pathOrder;
                    int distance = left.Distance(first.CellId, first.Progress).CompareTo(right.Distance(second.CellId, second.Progress));
                    if (distance != 0) return distance;
                }
                return first.Item.Guid.CompareTo(second.Item.Guid);
            }
            #endregion
        }
    }
}
