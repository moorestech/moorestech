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


        public void OnInventoryUpdateInvoke(int slot, int itemId, int count)
        {
            OnInventoryUpdate?.Invoke(slot, itemId, count);
        }

        public void OnOpenInventoryInvoke(string uiType,params short[] param)
        {
            OnOpenInventory?.Invoke(uiType,param);
        }
    }
}