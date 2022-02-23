using Core.Item;

namespace Core.Inventory
{
    public interface IOpenableInventory
    {
        public IItemStack GetItem(int slot);
        void SetItem(int slot, IItemStack itemStack);
        void SetItem(int slot, int itemId,int count);
        public IItemStack ReplaceItem(int slot, IItemStack itemStack);
        public IItemStack ReplaceItem(int slot, int itemId,int count);
        
        public IItemStack InsertItem(IItemStack itemStack);
        public IItemStack InsertItem(int itemId,int count);
        public int GetSlotSize();
    }
}