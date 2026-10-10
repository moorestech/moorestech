using Game.Block.Blocks.BeltConveyor.Topology;

namespace Game.Block.Blocks.BeltConveyor.Transport.Rebuild
{
    // 新構成の中でのマスの位置。所属segmentと、segmentの出口からそのマスの出口までの距離
    // Where a cell sits in the new assembly: its segment and the distance from the segment exit to the cell exit
    public readonly struct BeltCellLocation
    {
        public readonly int SegmentIndex;
        public readonly BeltTopologyCell Cell;
        public readonly int CellExitDistance;
        // 末尾マスなら、合流・分岐のbufferはこのマスにある
        // On the last cell, a merge or branch buffer belongs to this cell
        public readonly bool IsLastCell;

        public BeltCellLocation(int segmentIndex, BeltTopologyCell cell, int cellExitDistance, bool isLastCell)
        {
            SegmentIndex = segmentIndex;
            Cell = cell;
            CellExitDistance = cellExitDistance;
            IsLastCell = isLastCell;
        }
    }
}
