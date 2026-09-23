using Game.Block.Interface.Extension;
using System.Collections.Generic;
using Game.BeltSegment;
using Game.Block.Interface;
using UnityEngine;
namespace Game.Block.Blocks.BeltConveyor
{
    internal sealed class BeltTopologyRoutes
    {
        internal readonly List<SegmentBeltComponent[]> Owners = new();
        internal readonly List<BeltRoute> Routes = new();
        internal readonly Dictionary<SegmentBeltComponent, int> SegmentIds = new();
        internal readonly List<BeltSegmentKind> Kinds = new();
        internal BeltTopologyRoutes(List<BeltTopologyNode> nodes, Dictionary<SegmentBeltComponent, BeltTopologyNode> lookup)
        {
            // 境界から最大経路を取り出し、残りの輪は最小座標から切る。
            // Trace maximal paths from boundaries, then cut remaining cycles at their minimum cell.
            foreach (var node in nodes)
            {
                bool start = node.Incoming.Count != 1 || node.IsMerge;
                if (!start)
                {
                    var source = node.Incoming[0].Source;
                    start = !source.TryGetComponent<SegmentBeltComponent>(out var belt) ||
                        lookup[belt].Outgoing.Count != 1 || lookup[belt].IsMerge;
                }
                if (start && !SegmentIds.ContainsKey(node.Belt)) Trace(node);
            }
            foreach (var node in nodes) if (!SegmentIds.ContainsKey(node.Belt)) Trace(node);

            #region Internal
            void Trace(BeltTopologyNode head)
            {
                var path = new List<SegmentBeltComponent>();
                var current = head;
                while (true)
                {
                    SegmentIds.Add(current.Belt, Owners.Count);
                    path.Add(current.Belt);
                    if (current.IsMerge || current.Outgoing.Count != 1 || current.Outgoing[0].Target is not SegmentBeltComponent next ||
                        lookup[next].Incoming.Count != 1 || SegmentIds.ContainsKey(next)) break;
                    current = lookup[next];
                }
                Owners.Add(path.ToArray());
                Kinds.Add(head.IsMerge ? BeltSegmentKind.Merge : 1 < current.Outgoing.Count ? BeltSegmentKind.Branch : BeltSegmentKind.Normal);
                var cells = new BeltRouteCell[path.Count];
                var entries = new BeltRouteCell[4];
                // 消えた接続のitemにも所有headの隣接セルを残す。
                // Keep local neighboring geometry for items whose incoming edge disappeared.
                var headPosition = head.Belt.Position.OriginalPos;
                var directions = new[] { Vector3Int.forward, Vector3Int.back, Vector3Int.left, Vector3Int.right };
                for (int direction = 0; direction < entries.Length; direction++)
                    entries[direction] = BeltTopologyGeometry.ExternalCell(headPosition + directions[direction]);
                for (int i = 0; i < path.Count; i++)
                {
                    var belt = path[i];
                    var incoming = lookup[belt].Incoming;
                    var upstream = 0 < i ? path[i - 1].Position.OriginalPos : 0 < incoming.Count
                        ? incoming[0].SourceCell : belt.Position.OriginalPos - belt.Position.BlockDirection.ConvertLocalCell(Vector3Int.forward);
                    cells[i] = BeltTopologyGeometry.Cell(belt, upstream);
                }
                foreach (var edge in head.Incoming)
                {
                    int direction = (int)edge.Direction ^ 1;
                    entries[direction] = edge.Source.TryGetComponent<SegmentBeltComponent>(out var source)
                        ? BeltTopologyGeometry.Cell(source, source.Position.OriginalPos - source.Position.BlockDirection.ConvertLocalCell(Vector3Int.forward))
                        : BeltTopologyGeometry.ExternalCell(edge.SourceCell);
                }
                Routes.Add(new BeltRoute(cells, entries));
            }
            #endregion
        }
    }
}
