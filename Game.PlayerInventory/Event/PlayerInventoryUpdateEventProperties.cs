using Core.Item;

namespace PlayerInventory.Event
{
    public class PlayerInventoryUpdateEventProperties
    {
        public readonly int playerId;
        public readonly int inventorySlot;
        public readonly int itemId;
        public readonly int amount;


        public PlayerInventoryUpdateEventProperties(int playerId, int inventorySlot, IItemStack itemStack)
        {
            this.playerId = playerId;
            this.inventorySlot = inventorySlot;
            this.itemId = itemStack.Id;
            this.amount = itemStack.Amount;
        }
    }
}