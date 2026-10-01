using System.Collections.Generic;

namespace Core.BeltTransport
{
    public sealed class BeltTransportNetwork
    {
        private readonly BeltCellOccupancy occupancy = new BeltCellOccupancy();
        private readonly IBeltExternalReceiverFactory receivers;
        private readonly IBeltItemDropObserver dropObserver;
        private readonly Dictionary<int, BeltTransportPath> pathsByCell = new Dictionary<int, BeltTransportPath>();
        private readonly Dictionary<int, BeltNetworkCell> cellsById = new Dictionary<int, BeltNetworkCell>();
        private BeltNetworkCell[] cells = new BeltNetworkCell[0];
        private BeltNetworkConnection[] connections = new BeltNetworkConnection[0];
        private BeltTransportPath[] paths = new BeltTransportPath[0];
        private BeltSimulation simulation = new BeltSimulation(new BeltConveyorSegment[0]);

        public BeltTransportNetwork(IBeltExternalReceiverFactory receivers, IBeltItemDropObserver dropObserver)
        {
            this.receivers = receivers;
            this.dropObserver = dropObserver;
        }

        public void Restore(BeltNetworkSnapshot snapshot)
        {
            var priorities = new Dictionary<int, int>();
            foreach (var priority in snapshot.Priorities) priorities.Add(priority.CellId, priority.Order);
            Build(snapshot.Cells, snapshot.Connections, snapshot.Items, priorities);
        }

        public void Rebuild(BeltNetworkCell[] newCells, BeltNetworkConnection[] newConnections, BeltCellItemState[] addedItems)
        {
            var items = new List<BeltCellItemState>(CaptureItems());
            items.AddRange(addedItems);
            Build(newCells, newConnections, items.ToArray(), new Dictionary<int, int>());
        }

        public void Tick() => simulation.Tick();
        public IReadOnlyDictionary<int, int> DrainOccupancyChanges() => occupancy.DrainChanges();
        public int GetSlotSize(int cellId)
        {
            var path = pathsByCell[cellId];
            return path.Cells[path.Cells.Length - 1].Id == cellId && path.Segment.Buffer != null ? 2 : 1;
        }

        public int GetPriority(int cellId)
        {
            var path = pathsByCell[cellId];
            return path.Cells[path.Cells.Length - 1].Id == cellId ? path.Segment.PriorityOrder : 0;
        }

        public bool TryGetBufferedItem(int cellId, out BeltItem item) => pathsByCell[cellId].Segment.Buffer.TryGetItem(out item);

        public void SetSpeeds(BeltCellSpeed[] speeds)
        {
            var snapshot = Capture();
            foreach (var change in speeds)
                for (int i = 0; i < snapshot.Cells.Length; i++)
                    if (snapshot.Cells[i].Id == change.CellId) snapshot.Cells[i] = snapshot.Cells[i].WithSpeed(change.Speed);
            // 速度境界の分割と結合もセル位置を保って再現する。
            // Repartition speed boundaries while preserving cell-local positions.
            Restore(snapshot);
        }

        public bool TryInsert(int cellId, BeltDirection direction, int length, in BeltItem item)
        {
            var path = pathsByCell[cellId];
            return path.Cells[0].Id == cellId && path.Segment.Kind != BeltSegmentKind.Merge && path.Segment.TryReceive(direction, length, item);
        }

        public bool CanInsert(int cellId)
        {
            var path = pathsByCell[cellId];
            return path.Cells[0].Id == cellId && path.Segment.Kind != BeltSegmentKind.Merge && 0 < path.Segment.GetOffer(BeltDirection.None);
        }

        public void ReplaceCellItems(int cellId, BeltCellItemState[] replacement)
        {
            var snapshot = Capture();
            var items = new List<BeltCellItemState>();
            foreach (var item in snapshot.Items) if (item.CellId != cellId) items.Add(item);
            items.AddRange(replacement);
            Restore(new BeltNetworkSnapshot(snapshot.Cells, snapshot.Connections, items.ToArray(), snapshot.Priorities));
        }

        public BeltNetworkSnapshot Capture()
        {
            var priorities = new List<BeltCellPriority>();
            foreach (var path in paths)
                if (path.Segment.Kind != BeltSegmentKind.Normal)
                    priorities.Add(new BeltCellPriority(path.Cells[path.Cells.Length - 1].Id, path.Segment.PriorityOrder));
            return new BeltNetworkSnapshot((BeltNetworkCell[])cells.Clone(), (BeltNetworkConnection[])connections.Clone(), CaptureItems(), priorities.ToArray());
        }

        public BeltCellItemState[] CaptureItems()
        {
            var result = new List<BeltCellItemState>();
            foreach (var path in paths)
            {
                foreach (var state in path.Segment.CaptureItems())
                {
                    int index = path.Cells.Length - 1 - state.DistanceToExit / BeltConstants.ItemWidth;
                    var cell = path.Cells[index];
                    var previous = path.Segment.Kind == BeltSegmentKind.Merge
                        ? path.Segment.InputDirection : BeltDirection.None;
                    var entry = BeltNetworkEntry.Direction(path, index, connections, previous);
                    int height = BeltNetworkEntry.Height(cell.Id, entry, connections, cellsById);
                    result.Add(new BeltCellItemState(cell.Id, BeltConstants.ItemWidth - state.DistanceToExit % BeltConstants.ItemWidth,
                        entry, height, state.Item, false));
                }
                if (path.Segment.Buffer == null || !path.Segment.Buffer.TryGetItem(out var item)) continue;
                var tail = path.Cells[path.Cells.Length - 1];
                result.Add(new BeltCellItemState(tail.Id, BeltConstants.ItemWidth, BeltDirections.Opposite(tail.Forward), 0, item, true));
            }
            return result.ToArray();
        }

        private void Build(BeltNetworkCell[] newCells, BeltNetworkConnection[] newConnections, BeltCellItemState[] items,
            IReadOnlyDictionary<int, int> priorities)
        {
            // 全経路を作ってから接続し、旧経路の位置を復元する。
            // Build every path before wiring ports and restoring old positions.
            occupancy.ReleasePaths();
            cells = (BeltNetworkCell[])newCells.Clone();
            connections = (BeltNetworkConnection[])newConnections.Clone();
            paths = BeltTopologyBuilder.Build(cells, connections, priorities, occupancy);
            pathsByCell.Clear();
            cellsById.Clear();
            var segments = new List<BeltConveyorSegment>();
            foreach (var path in paths)
            {
                segments.Add(path.Segment);
                foreach (var cell in path.Cells) { pathsByCell.Add(cell.Id, path); cellsById.Add(cell.Id, cell); }
            }
            foreach (var path in paths)
            foreach (var edge in path.Outputs)
            {
                // 合流入力の登録は内部グラフだけで行う。
                // Register merge inputs only within the internal belt graph.
                IBeltReceiver target;
                if (edge.TargetIsBelt)
                {
                    var targetSegment = pathsByCell[edge.TargetId].Segment;
                    IBeltSource source = path.Segment.Buffer == null ? path.Segment : path.Segment.Buffer;
                    targetSegment.AttachInput(source, BeltDirections.Opposite(edge.Direction));
                    target = targetSegment;
                }
                else target = receivers.Create(edge, path.Segment.Buffer == null ? 4 : 3);
                if (path.Segment.Buffer == null) path.Segment.ConnectTo(target, edge.Direction);
                else path.Segment.Buffer.ConnectTo(target, edge.Direction);
            }
            BeltItemRestorer.Restore(paths, connections, items, dropObserver);
            simulation = new BeltSimulation(segments);
        }
    }
}
