namespace MainGame.UnityView.Interface
{
    public interface IBlockInventoryUpdateEvent
    {
        public delegate void OpenInventory(string uiType,params short[] param);
        public delegate void InventoryUpdate(int slot,int itemId,int count);
        
        public void Subscribe(InventoryUpdate inventoryUpdate,OpenInventory openInventory);
    }
}