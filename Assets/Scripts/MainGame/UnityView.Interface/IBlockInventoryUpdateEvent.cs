namespace MainGame.UnityView.Interface
{
    public interface IBlockInventoryUpdateEvent
    {
        public delegate void OpenInventory();
        public delegate void InventoryUpdate(int slot,int itemId,int count);
        
        public void Subscribe(InventoryUpdate inventoryUpdate,OpenInventory openInventory);
    }
}