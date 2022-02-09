using MainGame.UnityView.Interface;
using static MainGame.UnityView.Interface.IPlayerInventoryViewUpdateEvent;

namespace MainGame.GameLogic.Event
{
    public class PlayerInventoryViewUpdateEvent : IPlayerInventoryViewUpdateEvent
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