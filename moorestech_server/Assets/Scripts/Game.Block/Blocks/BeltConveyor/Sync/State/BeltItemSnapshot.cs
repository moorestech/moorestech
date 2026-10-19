using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;

namespace Game.Block.Blocks.BeltConveyor.Sync.State
{
    // 全量に載せるアイテム1個。走行中は所属segmentの出口までの距離を持ち、buffer内は0
    // One item in the full state; a running item carries its distance to the owning segment's exit, a buffered one carries 0
    public readonly struct BeltItemSnapshot
    {
        public readonly ItemId ItemId;
        public readonly ItemInstanceId ItemInstanceId;
        public readonly BeltEntryDirection EntryDirection;
        public readonly int DistanceToExit;

        public BeltItemSnapshot(in BeltItem item, int distanceToExit)
        {
            ItemId = item.ItemId;
            ItemInstanceId = item.ItemInstanceId;
            EntryDirection = item.EntryDirection;
            DistanceToExit = distanceToExit;
        }

        public BeltItem ToItem()
        {
            return new BeltItem(ItemId, ItemInstanceId, EntryDirection);
        }

        public BeltItemState ToState()
        {
            return new BeltItemState(ToItem(), DistanceToExit);
        }
    }
}
