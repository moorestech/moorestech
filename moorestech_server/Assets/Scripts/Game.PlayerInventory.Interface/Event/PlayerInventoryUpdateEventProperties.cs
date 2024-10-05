using Core.Item.Interface;

namespace Game.PlayerInventory.Interface.Event
{
    public class PlayerInventoryUpdateEventProperties
    {
        public readonly int PlayerId;
        public readonly int InventorySlot;
        public readonly IItemStack ItemStack;
        
        public PlayerInventoryUpdateEventProperties(int playerId, int inventorySlot, IItemStack itemStack)
        {
            PlayerId = playerId;
            InventorySlot = inventorySlot;
            ItemStack = itemStack;
        }
    }
}