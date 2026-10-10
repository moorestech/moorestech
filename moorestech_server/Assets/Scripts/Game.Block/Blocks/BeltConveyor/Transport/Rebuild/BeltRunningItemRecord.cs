using Core.BeltTransport;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Transport.Rebuild
{
    // 再構築前の走行中アイテム1個。先頭が乗っていたマスと、先頭からそのマスの出口までの距離で持つ
    // One running item before a rebuild, kept as the cell its head was on and the head's distance to that cell's exit
    public readonly struct BeltRunningItemRecord
    {
        public readonly BlockInstanceId CellBlockInstanceId;
        public readonly int DistanceToCellExit;
        public readonly BeltItem Item;
        // 同じ優先度・同じ位置のときに復元順を固定するための、元のsegment番号と出口からの順番
        // Source segment index and exit order, fixing the restore order among equal priority and position
        public readonly int SourceSegmentIndex;
        public readonly int SourceOrder;

        public BeltRunningItemRecord(BlockInstanceId cellBlockInstanceId, int distanceToCellExit, in BeltItem item, int sourceSegmentIndex, int sourceOrder)
        {
            CellBlockInstanceId = cellBlockInstanceId;
            DistanceToCellExit = distanceToCellExit;
            Item = item;
            SourceSegmentIndex = sourceSegmentIndex;
            SourceOrder = sourceOrder;
        }
    }
}
