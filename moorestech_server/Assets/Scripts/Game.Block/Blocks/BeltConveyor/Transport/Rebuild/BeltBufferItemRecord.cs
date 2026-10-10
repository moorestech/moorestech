using Core.BeltTransport;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Transport.Rebuild
{
    // 再構築前に合流・分岐のbufferが保持していたアイテム1個。bufferのあるマス(segmentの末尾マス)で持つ
    // One item a merge or branch buffer held before a rebuild, kept by the buffer's cell (the last cell of its segment)
    public readonly struct BeltBufferItemRecord
    {
        public readonly BlockInstanceId CellBlockInstanceId;
        public readonly BeltItem Item;
        public readonly int SourceSegmentIndex;

        public BeltBufferItemRecord(BlockInstanceId cellBlockInstanceId, in BeltItem item, int sourceSegmentIndex)
        {
            CellBlockInstanceId = cellBlockInstanceId;
            Item = item;
            SourceSegmentIndex = sourceSegmentIndex;
        }
    }
}
