namespace Game.BeltSegment
{
    public readonly struct BeltRouteCell
    {
        public readonly BeltCell Cell;
        public readonly BeltEntryDirection Entry;
        public readonly int CenterHeightTwice, InputHeightTwice;
        public BeltRouteCell(BeltCell cell, BeltEntryDirection entry, int centerHeightTwice, int inputHeightTwice)
        { Cell = cell; Entry = entry; CenterHeightTwice = centerHeightTwice; InputHeightTwice = inputHeightTwice; }
    }
    public sealed class BeltRoute
    {
        public readonly BeltRouteCell[] Cells, EntryCells;
        public BeltRoute(BeltRouteCell[] cells, BeltRouteCell[] entryCells)
        { Cells = (BeltRouteCell[])cells.Clone(); EntryCells = (BeltRouteCell[])entryCells.Clone(); }
    }
}
