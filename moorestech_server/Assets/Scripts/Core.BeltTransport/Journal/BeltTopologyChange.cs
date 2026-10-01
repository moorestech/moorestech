using System.Collections.Generic;
namespace Core.BeltTransport
{
    public sealed class BeltTopologyChange : BeltBoundaryChange
    {
        public readonly BeltNetworkCell[] ChangedCells;
        public readonly int[] RemovedCells;
        public readonly BeltNetworkConnection[] AddedConnections, RemovedConnections;
        public readonly BeltCellItemState[] AddedItems;
        public BeltTopologyChange(BeltNetworkCell[] changedCells, int[] removedCells, BeltNetworkConnection[] addedConnections,
            BeltNetworkConnection[] removedConnections, BeltCellItemState[] addedItems)
        {
            ChangedCells = changedCells; RemovedCells = removedCells;
            AddedConnections = addedConnections; RemovedConnections = removedConnections; AddedItems = addedItems;
        }
        public override void Apply(BeltTransportNetwork network)
        {
            var previous = network.Capture();
            var cells = new Dictionary<int, BeltNetworkCell>();
            foreach (var cell in previous.Cells) cells.Add(cell.Id, cell);
            foreach (int id in RemovedCells) cells.Remove(id);
            foreach (var cell in ChangedCells) cells[cell.Id] = cell;
            var connections = new List<BeltNetworkConnection>(previous.Connections);
            foreach (var edge in RemovedConnections) connections.Remove(edge);
            connections.AddRange(AddedConnections);
            network.Rebuild(new List<BeltNetworkCell>(cells.Values).ToArray(), connections.ToArray(), AddedItems);
        }
        public static BeltTopologyChange Between(BeltNetworkSnapshot previous, BeltNetworkCell[] cells,
            BeltNetworkConnection[] connections, BeltCellItemState[] addedItems)
        {
            // 配置変更では経路定義の差分だけを送り、走行列は双方で復元する。
            // Send topology differences only and restore running queues on both sides.
            var oldCells = new Dictionary<int, BeltNetworkCell>();
            foreach (var cell in previous.Cells) oldCells.Add(cell.Id, cell);
            var changed = new List<BeltNetworkCell>();
            foreach (var cell in cells)
            {
                if (!oldCells.TryGetValue(cell.Id, out var old) || !old.Equals(cell)) changed.Add(cell);
                oldCells.Remove(cell.Id);
            }
            var removed = new List<int>(oldCells.Keys);
            var addedEdges = new List<BeltNetworkConnection>();
            var removedEdges = new List<BeltNetworkConnection>(previous.Connections);
            foreach (var edge in connections)
                if (!removedEdges.Remove(edge)) addedEdges.Add(edge);
            return new BeltTopologyChange(changed.ToArray(), removed.ToArray(), addedEdges.ToArray(), removedEdges.ToArray(), addedItems);
        }
    }
}
