using Core.Item.Interface;
using Core.Master;

namespace Core.BeltTransport
{
    // ベルト上のアイテム。現在マスへの搬入元方向だけを持ち、マスと進行量は別で導出する
    // An item on a belt. Carries only the entry direction into its current cell; cell and progress are derived elsewhere
    public readonly struct BeltItem
    {
        public readonly ItemId ItemId;
        public readonly ItemInstanceId ItemInstanceId;
        public readonly BeltEntryDirection EntryDirection;

        public BeltItem(ItemId itemId, ItemInstanceId itemInstanceId, BeltEntryDirection entryDirection)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            EntryDirection = entryDirection;
        }

        public BeltItem WithEntryDirection(BeltEntryDirection entryDirection)
        {
            return new BeltItem(ItemId, ItemInstanceId, entryDirection);
        }
    }
}
