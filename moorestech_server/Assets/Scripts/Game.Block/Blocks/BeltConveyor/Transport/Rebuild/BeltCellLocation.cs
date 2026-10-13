using Core.BeltTransport;
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

        // マス内の残り距離を、segment出口までの距離へ戻す
        // Converts the remaining distance within this cell back to the distance to the segment exit
        public int ToSegmentDistance(int distanceToCellExit)
        {
            return CellExitDistance + distanceToCellExit;
        }

        // 距離とマスの換算はこの対だけで行う。マス番号は搬送順、segmentの出口から各マスの出口までは1マス幅ずつ離れる
        // Distance and cell conversions go through this pair only; cell indices follow transport order and each cell exit lies one cell width further from the segment exit
        public static int CellExitDistanceOf(int cellCount, int cellIndex)
        {
            return (cellCount - 1 - cellIndex) * BeltConstants.ItemWidth;
        }

        // segment出口までの距離から、先頭が乗るマス番号とそのマスの出口までの残り距離を出す
        // From the distance to the segment exit, yields the cell the head is on and the remaining distance to that cell's exit
        public static int CellIndexOf(int cellCount, int distanceToExit, out int distanceToCellExit)
        {
            distanceToCellExit = distanceToExit % BeltConstants.ItemWidth;
            return cellCount - 1 - distanceToExit / BeltConstants.ItemWidth;
        }
    }
}
