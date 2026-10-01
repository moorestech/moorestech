namespace Core.BeltTransport
{
    internal sealed class BeltPathOccupancy : IBeltItemMovementObserver
    {
        private readonly BeltNetworkCell[] cells;
        private readonly BeltCellOccupancy occupancy;
        internal BeltPathOccupancy(BeltNetworkCell[] cells, BeltCellOccupancy occupancy)
        { this.cells = cells; this.occupancy = occupancy; }

        public void Insert(int distance) => occupancy.Add(CellId(distance), 1);
        public void Remove(int distance) => occupancy.Add(CellId(distance), -1);
        public void Move(int before, int after)
        {
            // 物理移動がセル境界を跨いだ時だけ所有数を移す。
            // Transfer ownership only when physical movement crosses a cell boundary.
            int source = CellId(before), target = CellId(after);
            if (source == target) return;
            occupancy.Add(source, -1);
            occupancy.Add(target, 1);
        }
        private int CellId(int distance) => cells[cells.Length - 1 - distance / BeltConstants.ItemWidth].Id;
    }
}
