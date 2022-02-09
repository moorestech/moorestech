namespace MainGame.UnityView.Interface
{
    public interface IBlockInventoryUpdateEvent
    {
        public delegate void SettingInventory(string uiType,params short[] param);
        public delegate void InventoryUpdate(int slot,int itemId,int count);
        
        public void Subscribe(InventoryUpdate inventoryUpdate,SettingInventory settingInventory);
    }
}