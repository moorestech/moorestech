using Core.Item;

namespace Core.Inventory
{
    public interface IInventory
    {
        public IItemStack GetItem(int slot);
        void SetItem(int slot, IItemStack itemStack);
        public IItemStack ReplaceItem(int slot, IItemStack itemStack);
    }
}