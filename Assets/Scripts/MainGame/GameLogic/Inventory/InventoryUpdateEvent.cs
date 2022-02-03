using MainGame.UnityView.Interface;
using static MainGame.UnityView.Interface.IInventoryUpdateEvent;

namespace MainGame.GameLogic.Inventory
{
    public class InventoryUpdateEvent : IInventoryUpdateEvent
    {
        
        private event InventoryUpdate OnInventoryUpdate;
        public void Subscribe(InventoryUpdate inventoryUpdate)
        {
            OnInventoryUpdate += inventoryUpdate;
        }

        public void OnOnInventoryUpdate(int slot, int itemId, int count)
        {
            OnInventoryUpdate?.Invoke(slot, itemId, count);
        }
    }
}