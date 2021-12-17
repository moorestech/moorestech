using Core.Item;

namespace Game.PlayerInventory.Interface.Event
{
    public class PlayerInventoryUpdateEventProperties
    {
        public readonly int PlayerId;
        public readonly int InventorySlot;
        public readonly int ItemId;
        public readonly int Amount;


        public PlayerInventoryUpdateEventProperties(int playerId, int inventorySlot, IItemStack itemStack)
        {
            PlayerId = playerId;
            InventorySlot = inventorySlot;
            ItemId = itemStack.Id;
            Amount = itemStack.Amount;
        }
    }
}