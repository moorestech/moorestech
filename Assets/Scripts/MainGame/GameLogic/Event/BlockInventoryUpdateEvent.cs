using MainGame.UnityView.Interface;
using static MainGame.UnityView.Interface.IBlockInventoryUpdateEvent;

namespace MainGame.GameLogic.Event
{
    public class BlockInventoryUpdateEvent : IBlockInventoryUpdateEvent
    {
        public event InventoryUpdate OnInventoryUpdate;
        public event SettingInventory OnSettingInventory;
        
        public void Subscribe(InventoryUpdate inventoryUpdate, SettingInventory settingInventory)
        {
            OnInventoryUpdate += inventoryUpdate;
            OnSettingInventory += settingInventory;
        }


        public void OnInventoryUpdateInvoke(int slot, int itemId, int count)
        {
            OnInventoryUpdate?.Invoke(slot, itemId, count);
        }

        public void OnSettingInventoryInvoke(string uiType,params short[] param)
        {
            OnSettingInventory?.Invoke(uiType,param);
        }
    }
}