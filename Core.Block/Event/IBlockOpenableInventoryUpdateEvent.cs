using Core.Item;

namespace Core.Block.Event
{
    /// <summary>
    /// Subscribeだけができるイベントインタフェース
    /// 勝手にInvokeされないように定義している
    /// </summary>
    public interface IBlockOpenableInventoryUpdateEvent
    {
        public delegate void BlockInventoryEvent(
            BlockOpenableInventoryUpdateEventProperties properties);

        public void Subscribe(BlockInventoryEvent blockInventoryEvent);
    }

    public class BlockOpenableInventoryUpdateEventProperties
    {
        public readonly int EntityId;
        public readonly int Slot;
        public readonly IItemStack ItemStack;

        public BlockOpenableInventoryUpdateEventProperties(int entityId, int slot,IItemStack itemStack)
        {
            ItemStack = itemStack;
            Slot = slot;
            EntityId = entityId;
        }
    }
}