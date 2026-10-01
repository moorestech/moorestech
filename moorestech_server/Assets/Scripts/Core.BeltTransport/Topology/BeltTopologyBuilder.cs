using System.Collections.Generic;

namespace Core.BeltTransport
{
    internal static class BeltTopologyBuilder
    {
        internal static BeltTransportPath[] Build(BeltNetworkCell[] cells, BeltNetworkConnection[] connections,
            IReadOnlyDictionary<int, int> priorities, BeltCellOccupancy occupancy)
        {
            var nodes = new Dictionary<int, BeltTopologyNode>();
            var ordered = new List<BeltTopologyNode>();
            foreach (var cell in cells)
            {
                var node = new BeltTopologyNode(cell);
                nodes.Add(cell.Id, node);
                ordered.Add(node);
            }

            // 接続済み方向から合流・分岐の役割を確定する。
            // Derive merge and branch roles from resolved connections.
            foreach (var edge in connections)
            {
                if (edge.SourceIsBelt) nodes[edge.SourceId].Outputs.Add(edge);
                if (edge.TargetIsBelt) nodes[edge.TargetId].Inputs.Add(edge);
            }
            ordered.Sort(Compare);
            var paths = new List<BeltTransportPath>();
            foreach (var node in ordered)
                if (!node.Assigned && !HasPredecessor(node)) AppendPath(node);

            // 輪の切れ目は座標のX,Y,Z順で最小のセルに固定する。
            // Cut a cycle at its lexicographically smallest X,Y,Z cell.
            foreach (var node in ordered) if (!node.Assigned) AppendPath(node);
            return paths.ToArray();

            #region Internal
            bool HasPredecessor(BeltTopologyNode node)
            {
                if (node.Kind == BeltSegmentKind.Merge || node.Inputs.Count != 1 || !node.Inputs[0].SourceIsBelt) return false;
                var previous = nodes[node.Inputs[0].SourceId];
                return previous.Kind == BeltSegmentKind.Normal && previous.Outputs.Count == 1 && SameSpeed(previous, node);
            }

            void AppendPath(BeltTopologyNode start)
            {
                var chain = new List<BeltNetworkCell>();
                var tail = start;
                while (true)
                {
                    tail.Assigned = true;
                    chain.Add(tail.Cell);
                    if (tail.Kind != BeltSegmentKind.Normal || tail.Outputs.Count != 1 || !tail.Outputs[0].TargetIsBelt) break;
                    var next = nodes[tail.Outputs[0].TargetId];
                    if (next.Assigned || next.Kind == BeltSegmentKind.Merge || !HasPredecessor(next)) break;
                    tail = next;
                }
                int priority = priorities.TryGetValue(tail.Cell.Id, out var saved) ? saved : -1;
                var forward = tail.Kind == BeltSegmentKind.Merge && tail.Outputs.Count == 1 ? tail.Outputs[0].Direction : tail.Cell.Forward;
                paths.Add(new BeltTransportPath(chain.ToArray(), tail.Kind, priority, forward, tail.Outputs, occupancy));
            }
            bool SameSpeed(BeltTopologyNode first, BeltTopologyNode second) =>
                first.Cell.SpeedProfile == second.Cell.SpeedProfile && first.Cell.Speed == second.Cell.Speed;

            int Compare(BeltTopologyNode first, BeltTopologyNode second)
            {
                int x = first.Cell.X.CompareTo(second.Cell.X);
                if (x != 0) return x;
                int y = first.Cell.Y.CompareTo(second.Cell.Y);
                return y != 0 ? y : first.Cell.Z.CompareTo(second.Cell.Z);
            }
            #endregion
        }

    }
}
