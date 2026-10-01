namespace Core.BeltTransport
{
    public abstract class BeltBoundaryChange
    {
        public abstract void Apply(BeltTransportNetwork network);
    }
    public sealed class BeltSpeedChange : BeltBoundaryChange
    {
        public readonly BeltCellSpeed[] Speeds;
        public BeltSpeedChange(BeltCellSpeed[] speeds) { Speeds = speeds; }
        public override void Apply(BeltTransportNetwork network) => network.SetSpeeds(Speeds);
    }
    public sealed class BeltInputChange : BeltBoundaryChange
    {
        public readonly int CellId, Length;
        public readonly BeltDirection Direction;
        public readonly BeltItem Item;
        public BeltInputChange(int cellId, BeltDirection direction, int length, BeltItem item)
        { CellId = cellId; Direction = direction; Length = length; Item = item; }
        public override void Apply(BeltTransportNetwork network)
        {
            if (!network.TryInsert(CellId, Direction, Length, Item))
                throw new System.InvalidOperationException($"Committed belt input rejected: cell={CellId}, item={Item.Guid}.");
        }
    }
    public sealed class BeltCellItemsChange : BeltBoundaryChange
    {
        public readonly int CellId;
        public readonly BeltCellItemState[] Items;
        public BeltCellItemsChange(int cellId, BeltCellItemState[] items) { CellId = cellId; Items = items; }
        public override void Apply(BeltTransportNetwork network) => network.ReplaceCellItems(CellId, Items);
    }
}
