namespace Core.BeltTransport
{
    public sealed class BeltNetworkSnapshot
    {
        public readonly BeltNetworkCell[] Cells;
        public readonly BeltNetworkConnection[] Connections;
        public readonly BeltCellItemState[] Items;
        public readonly BeltCellPriority[] Priorities;

        public BeltNetworkSnapshot(BeltNetworkCell[] cells, BeltNetworkConnection[] connections,
            BeltCellItemState[] items, BeltCellPriority[] priorities)
        {
            Cells = cells; Connections = connections; Items = items; Priorities = priorities;
        }
    }
}
