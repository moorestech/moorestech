using Core.Item;

namespace Game.PlayerInventory.Interface.Event
{
    public class PlayerInventoryUpdateEventProperties
    {
        public readonly int PlayerId;
        public readonly int InventorySlot;
        public readonly IItemStack ItemStack;
        public readonly int ItemId;
        public readonly int Count;


        public PlayerInventoryUpdateEventProperties(int playerId, int inventorySlot, IItemStack itemStack)
        {
            PlayerId = playerId;
            InventorySlot = inventorySlot;
            ItemStack = itemStack;
            ItemId = itemStack.Id;
            Count = itemStack.Count;
        }
    }
}