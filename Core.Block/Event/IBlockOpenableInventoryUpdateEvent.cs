using Core.Item;

namespace Core.Block.Event
{
    public interface IBlockOpenableInventoryUpdateEvent
    {
        public delegate void BlockInventoryEvent(
            BlockOpenableInventoryUpdateEventProperties properties);

        public void Subscribe(BlockInventoryEvent blockInventoryEvent);
    }

    public class BlockOpenableInventoryUpdateEventProperties
    {
        public readonly int EntityId;
        public readonly int SlotId;
        public readonly IItemStack ItemStack;

        public BlockOpenableInventoryUpdateEventProperties(IItemStack itemStack, int slotId, int entityId)
        {
            ItemStack = itemStack;
            SlotId = slotId;
            EntityId = entityId;
        }
    }
}