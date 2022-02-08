using MainGame.UnityView.Interface;
using static MainGame.UnityView.Interface.IBlockInventoryUpdateEvent;

namespace MainGame.GameLogic.Event
{
    public class BlockInventoryUpdateEvent : IBlockInventoryUpdateEvent
    {
        public event InventoryUpdate OnInventoryUpdate;
        public event OpenInventory OnOpenInventory;
        
        public void Subscribe(InventoryUpdate inventoryUpdate, OpenInventory openInventory)
        {
            OnInventoryUpdate += inventoryUpdate;
            OnOpenInventory += openInventory;
        }

        protected virtual void OnOnOpenInventory()
        {
            OnOpenInventory?.Invoke();
        }

        protected virtual void OnOnInventoryUpdate(int slot, int itemid, int count)
        {
            OnInventoryUpdate?.Invoke(slot, itemid, count);
        }
    }
}