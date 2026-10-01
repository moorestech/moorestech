namespace Core.BeltTransport
{
    public readonly struct BeltCellPriority
    {
        public readonly int CellId, Order;
        public BeltCellPriority(int cellId, int order) { CellId = cellId; Order = order; }
    }
}
