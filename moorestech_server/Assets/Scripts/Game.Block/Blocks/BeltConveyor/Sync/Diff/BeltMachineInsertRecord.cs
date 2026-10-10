using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;

namespace Game.Block.Blocks.BeltConveyor.Sync.Diff
{
    // 差分1種め: 機械→ベルコンの搬入成功。受けたsegment番号と、受け側から見た搬入元の方向、入ったアイテム
    // Diff kind 1: a successful machine-to-belt push; the receiving segment number, the source direction seen from the receiver, and the item that entered
    public readonly struct BeltMachineInsertRecord
    {
        public readonly int SegmentIndex;
        public readonly BeltDirection InputDirection;
        public readonly ItemId ItemId;
        public readonly ItemInstanceId ItemInstanceId;
        public readonly BeltEntryDirection EntryDirection;

        public BeltMachineInsertRecord(int segmentIndex, BeltDirection inputDirection, in BeltItem item)
        {
            SegmentIndex = segmentIndex;
            InputDirection = inputDirection;
            ItemId = item.ItemId;
            ItemInstanceId = item.ItemInstanceId;
            EntryDirection = item.EntryDirection;
        }

        public BeltItem ToItem()
        {
            return new BeltItem(ItemId, ItemInstanceId, EntryDirection);
        }
    }
}
