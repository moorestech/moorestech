namespace MainGame.UnityView.Interface
{
    public interface IPlayerInventoryViewUpdateEvent
    {
        public delegate void InventoryUpdate(int slot,int itemId,int count);
        public void Subscribe(InventoryUpdate inventoryUpdate);
    }
}